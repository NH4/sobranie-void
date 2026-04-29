using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sobranie.Domain;
using Sobranie.Infrastructure.Persistence;

namespace Sobranie.Infrastructure.Fsm;

/// <summary>
/// Singleton that exposes the current session state. The background
/// orchestrator reads <see cref="IsRunning"/> on each tick; endpoints flip
/// it via <see cref="Start"/> / <see cref="Stop"/>.
/// </summary>
public sealed class SessionState
{
    private int running;
    public bool IsRunning => Volatile.Read(ref running) == 1;
    public DateTimeOffset? StartedAt { get; private set; }
    public long TurnsCompleted { get; private set; }
    public string? LastError { get; private set; }

    public bool Start()
    {
        if (Interlocked.CompareExchange(ref running, 1, 0) != 0)
        {
            return false;
        }

        StartedAt = DateTimeOffset.UtcNow;
        TurnsCompleted = 0;
        LastError = null;
        return true;
    }

    public bool Stop()
    {
        return Interlocked.CompareExchange(ref running, 0, 1) == 1;
    }

    public void RecordTurnCompleted() => TurnsCompleted++;
    public void RecordError(string message) => LastError = message;
}

public sealed partial class SessionOrchestrator(
    SessionState state,
    IServiceScopeFactory scopeFactory,
    IOptions<SobranieOptions> options,
    ILogger<SessionOrchestrator> logger) : BackgroundService
{
    private readonly FsmOptions fsm = options.Value.Fsm;
    private readonly Random rng = options.Value.Fsm.RandomSeed is { } seed
        ? new Random(seed)
        : Random.Shared;

    private int turnsOnCurrentProposal;
    private int? currentProposalId;

    [LoggerMessage(Level = LogLevel.Information, Message = "SessionOrchestrator started. AutoStart={AutoStart}.")]
    private partial void LogStarted(bool autoStart);

    [LoggerMessage(Level = LogLevel.Information, Message = "Turn {Turn}: selected {MPId} ({Display}) score={Score:F3} — {Rationale}")]
    private partial void LogTurn(long turn, string mPId, string display, double score, string rationale);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Session tick failed: {Reason}")]
    private partial void LogTickFailure(string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "No MainCast MPs in DB; session idle.")]
    private partial void LogNoCast();

    [LoggerMessage(Level = LogLevel.Information, Message = "Proposal {ProposalId} promoted to InDebate: {Headline}.")]
    private partial void LogProposalPromoted(int proposalId, string headline);

    [LoggerMessage(Level = LogLevel.Information, Message = "Proposal {ProposalId} concluded after {Turns} turns.")]
    private partial void LogProposalConcluded(int proposalId, int turns);

    [LoggerMessage(Level = LogLevel.Warning, Message = "No Queued proposals available; debate will use null proposal.")]
    private partial void LogNoQueuedProposals();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(fsm.AutoStartSession);

        if (fsm.AutoStartSession)
        {
            state.Start();
        }

        turnsOnCurrentProposal = 0;
        currentProposalId = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            if (!state.IsRunning)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
                continue;
            }

            try
            {
                await RunOneTurnAsync(stoppingToken).ConfigureAwait(false);
                state.RecordTurnCompleted();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                state.RecordError(ex.Message);
                LogTickFailure(ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
                continue;
            }

            var pause = SampleCadenceSeconds();
            await Task.Delay(TimeSpan.FromSeconds(pause), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunOneTurnAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<SobranieDbContext>();
        var utility = sp.GetRequiredService<UtilityCalculator>();
        var selector = sp.GetRequiredService<SpeakerSelector>();
        var generator = sp.GetRequiredService<SpeechGenerator>();
        var broadcaster = sp.GetRequiredService<ISpeechBroadcaster>();

        var mainCast = await db.MPs
            .AsNoTracking()
            .Where(m => m.Tier == CastTier.MainCast)
            .Include(m => m.SignatureMoves)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        if (mainCast.Count == 0)
        {
            LogNoCast();
            await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
            return;
        }

        var currentProposal = await EnsureCurrentProposalAsync(db, ct).ConfigureAwait(false);

        var recent = await db.Speeches
            .AsNoTracking()
            .Include(s => s.MP)
            .OrderByDescending(s => s.Id)
            .Take(fsm.RecentSpeechWindow)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var scores = utility.Score(mainCast, new SessionContext(currentProposal, recent));
        var selectedId = selector.SelectSpeaker(scores, rng);
        var selectedScore = scores.First(s => s.MPId == selectedId);
        var selectedMp = mainCast.First(m => m.MPId == selectedId);

        LogTurn(state.TurnsCompleted + 1, selectedId, selectedMp.DisplayName, selectedScore.Score, selectedScore.Rationale);
        await broadcaster.BroadcastStateChangeAsync($"selecting:{selectedId}", ct).ConfigureAwait(false);

        var generated = await generator.GenerateAsync(
            selectedMp,
            currentProposal,
            recent,
            async chunk => await broadcaster.BroadcastChunkAsync(selectedId, chunk, ct).ConfigureAwait(false),
            ct).ConfigureAwait(false);

        var speech = new Speech
        {
            MPId = selectedId,
            ProposalId = currentProposal?.Id,
            Kind = SpeechKind.MainCastSpeech,
            Content = generated.Content,
            UtteredAt = DateTimeOffset.UtcNow,
            TokenCount = generated.TokenCount,
            GenerationSeconds = generated.ElapsedSeconds,
            UtilityAtSelection = selectedScore.Score,
            OutputFilterRejected = generated.WasFiltered,
        };

        db.Speeches.Add(speech);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await broadcaster.BroadcastSpeechCompleteAsync(
            new SpeechCompletedEvent(
                SpeechId: speech.Id,
                MPId: selectedId,
                MPDisplayName: selectedMp.DisplayName,
                PartyId: selectedMp.PartyId,
                Content: generated.Content,
                TokenCount: generated.TokenCount,
                ElapsedSeconds: generated.ElapsedSeconds,
                UtteredAt: speech.UtteredAt),
            ct).ConfigureAwait(false);

        turnsOnCurrentProposal++;

        if (turnsOnCurrentProposal >= fsm.TurnsPerProposal)
        {
            await ConcludeAndPromoteAsync(db, ct).ConfigureAwait(false);
        }
    }

    private async Task<Proposal?> EnsureCurrentProposalAsync(SobranieDbContext db, CancellationToken ct)
    {
        var existing = await db.Proposals
            .AsNoTracking()
            .Where(p => p.Status == ProposalStatus.InDebate)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (existing is not null && currentProposalId == existing.Id)
        {
            return existing;
        }

        if (existing is not null)
        {
            currentProposalId = existing.Id;
            turnsOnCurrentProposal = 0;
            return existing;
        }

        var next = await db.Proposals
            .Where(p => p.Status == ProposalStatus.Queued)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (next is null)
        {
            currentProposalId = null;
            turnsOnCurrentProposal = 0;
            LogNoQueuedProposals();
            return null;
        }

        next.Status = ProposalStatus.InDebate;
        next.IntroducedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        currentProposalId = next.Id;
        turnsOnCurrentProposal = 0;
        LogProposalPromoted(next.Id, next.Headline);

        return next;
    }

    private async Task ConcludeAndPromoteAsync(SobranieDbContext db, CancellationToken ct)
    {
        if (currentProposalId.HasValue)
        {
            var current = await db.Proposals.FindAsync([currentProposalId.Value], ct).ConfigureAwait(false);
            if (current is not null)
            {
                current.Status = ProposalStatus.Concluded;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                LogProposalConcluded(current.Id, turnsOnCurrentProposal);
            }
        }

        turnsOnCurrentProposal = 0;
        currentProposalId = null;

        var next = await db.Proposals
            .Where(p => p.Status == ProposalStatus.Queued)
            .OrderBy(p => p.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (next is null)
        {
            LogNoQueuedProposals();
            return;
        }

        next.Status = ProposalStatus.InDebate;
        next.IntroducedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        currentProposalId = next.Id;
        LogProposalPromoted(next.Id, next.Headline);
    }

    private double SampleCadenceSeconds()
    {
        var min = Math.Max(1, fsm.MinCadenceSeconds);
        var max = Math.Max(min, fsm.MaxCadenceSeconds);
        return min + (rng.NextDouble() * (max - min));
    }
}
