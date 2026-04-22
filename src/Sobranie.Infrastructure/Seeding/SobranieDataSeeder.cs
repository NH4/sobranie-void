using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sobranie.Domain;
using Sobranie.Infrastructure.Persistence;

namespace Sobranie.Infrastructure.Seeding;

/// <summary>
/// Idempotent seeder. Reads seed-data.json and populates Parties / MPs /
/// SignatureMoves / ChorusLines only when the DB is empty. Safe to run on
/// every boot.
/// </summary>
public sealed partial class SobranieDataSeeder(
    SobranieDbContext db,
    IHostEnvironment env,
    ILogger<SobranieDataSeeder> logger)
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Seed skipped: Parties table already populated.")]
    private partial void LogSeedSkipped();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed file not found at {SeedPath}; DB will remain empty.")]
    private partial void LogSeedFileMissing(string seedPath);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed complete: {Parties} parties, {MPs} MPs, {Chorus} chorus lines, {Rows} total rows written.")]
    private partial void LogSeedComplete(int parties, int mPs, int chorus, int rows);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await db.Parties.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            LogSeedSkipped();
            return;
        }

        var seedPath = Path.Combine(env.ContentRootPath, "seed-data.json");
        if (!File.Exists(seedPath))
        {
            LogSeedFileMissing(seedPath);
            return;
        }

        await using var stream = File.OpenRead(seedPath);
        var seed = await JsonSerializer.DeserializeAsync<SeedDocument>(stream, JsonOptions, cancellationToken)
                       .ConfigureAwait(false)
                   ?? throw new InvalidOperationException("seed-data.json deserialized to null.");

        foreach (var p in seed.Parties)
        {
            db.Parties.Add(new Party
            {
                PartyId = p.PartyId,
                DisplayName = p.DisplayName,
                ShortName = p.ShortName,
                ColorHex = p.ColorHex,
                SeatCount = p.SeatCount,
            });
        }

        foreach (var m in seed.MPs)
        {
            var mp = new MPProfile
            {
                MPId = m.MPId,
                PartyId = m.PartyId,
                DisplayName = m.DisplayName,
                Coalition = m.Coalition,
                Tier = Enum.Parse<CastTier>(m.Tier),
                Aggression = m.Aggression,
                Legalism = m.Legalism,
                Populism = m.Populism,
                SeatIndex = m.SeatIndex,
                PersonaCore = m.PersonaCore,
                PersonaOverlayGentle = m.PersonaOverlayGentle,
                PersonaOverlaySharp = m.PersonaOverlaySharp,
                PersonaOverlayAbsurd = m.PersonaOverlayAbsurd,
            };
            db.MPs.Add(mp);

            foreach (var sm in m.SignatureMoves)
            {
                db.SignatureMoves.Add(new SignatureMove
                {
                    MPId = m.MPId,
                    Label = sm.Label,
                    Exemplar = sm.Exemplar,
                    TriggerWeight = sm.TriggerWeight ?? 1.0,
                });
            }
        }

        foreach (var c in seed.ChorusLines)
        {
            db.ChorusLines.Add(new ChorusLine
            {
                PartyId = c.PartyId,
                Text = c.Text,
                TopicTag = c.TopicTag,
                Weight = c.Weight ?? 1.0,
            });
        }

        var rows = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        LogSeedComplete(seed.Parties.Count, seed.MPs.Count, seed.ChorusLines.Count, rows);
    }

    private sealed record SeedDocument(
        List<PartyDto> Parties,
        List<MPDto> MPs,
        List<ChorusDto> ChorusLines);

    private sealed record PartyDto(
        string PartyId,
        string DisplayName,
        string ShortName,
        string ColorHex,
        int SeatCount);

    private sealed record MPDto(
        string MPId,
        string PartyId,
        string DisplayName,
        string? Coalition,
        string Tier,
        double Aggression,
        double Legalism,
        double Populism,
        int SeatIndex,
        string? PersonaCore,
        string? PersonaOverlayGentle,
        string? PersonaOverlaySharp,
        string? PersonaOverlayAbsurd,
        List<SignatureMoveDto> SignatureMoves);

    private sealed record SignatureMoveDto(
        string Label,
        string Exemplar,
        double? TriggerWeight);

    private sealed record ChorusDto(
        string PartyId,
        string TopicTag,
        string Text,
        double? Weight);
}
