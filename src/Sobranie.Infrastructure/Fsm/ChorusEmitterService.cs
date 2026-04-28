using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sobranie.Domain;
using Sobranie.Infrastructure.Fsm;
using Sobranie.Infrastructure.Persistence;

namespace Sobranie.Infrastructure.Fsm;

public sealed partial class ChorusEmitterService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SessionState _sessionState;
    private readonly ChorusOptions _chorus;
    private readonly Random _rng = Random.Shared;
    private readonly ILogger<ChorusEmitterService> _logger;

    public ChorusEmitterService(
        IServiceScopeFactory scopeFactory,
        SessionState sessionState,
        IOptions<SobranieOptions> options,
        ILogger<ChorusEmitterService> logger)
    {
        _scopeFactory = scopeFactory;
        _sessionState = sessionState;
        _chorus = options.Value.Chorus;
        _logger = logger;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Chorus burst: {Count} reactions for proposal {ProposalId}.")]
    private partial void LogBurst(int count, int proposalId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Chorus burst failed: {Reason}.")]
    private partial void LogBurstError(string reason);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (!_sessionState.IsRunning)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);
                continue;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(SampleDelayMs()), stoppingToken).ConfigureAwait(false);

            if (!_sessionState.IsRunning)
            {
                continue;
            }

            try
            {
                await EmitBurstAsync(stoppingToken).ConfigureAwait(false);
            }
#pragma warning disable CA1031
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogBurstError(ex.Message);
            }
        }
    }

    private async Task EmitBurstAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SobranieDbContext>();

        var currentProposal = await db.Proposals
            .AsNoTracking()
            .Where(p => p.Status == ProposalStatus.InDebate)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var parties = await db.Parties
            .AsNoTracking()
            .Include(p => p.ChorusLines)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var burstSize = _chorus.MinReactionsPerBurst
            + _rng.Next(_chorus.MaxReactionsPerBurst - _chorus.MinReactionsPerBurst + 1);

        var allLines = parties
            .SelectMany(p => p.ChorusLines.Select(l => (Line: l, Party: p)))
            .ToList();

        if (allLines.Count == 0)
        {
            return;
        }

        var totalWeight = allLines.Sum(x => x.Line.Weight);
        var selected = new List<(ChorusLine Line, Party Party)>();

        for (var i = 0; i < burstSize; i++)
        {
            var r = _rng.NextDouble() * totalWeight;
            var cumulative = 0.0;
            foreach (var item in allLines)
            {
                cumulative += item.Line.Weight;
                if (r <= cumulative)
                {
                    selected.Add((item.Line, item.Party));
                    break;
                }
            }

            if (selected.Count == i)
            {
                selected.Add(allLines[_rng.Next(allLines.Count)]);
            }
        }

        foreach (var (line, party) in selected)
        {
            var speech = new Speech
            {
                MPId = null,
                ProposalId = currentProposal?.Id,
                Kind = SpeechKind.ChorusReaction,
                Content = line.Text,
                UtteredAt = DateTimeOffset.UtcNow,
            };

            db.Speeches.Add(speech);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        if (currentProposal is not null)
        {
            LogBurst(selected.Count, currentProposal.Id);
        }
    }

    private double SampleDelayMs()
    {
        var mean = _chorus.MeanReactionsPerTurn * 25_000;
        var lambda = 1.0 / mean;
        var u = _rng.NextDouble();
        return -Math.Log(1 - u) / lambda;
    }
}