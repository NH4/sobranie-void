namespace Sobranie.Domain;

public enum CastTier
{
    Chorus = 0,
    MainCast = 1,
}

public sealed class MPProfile
{
    public required string MPId { get; init; }
    public required string PartyId { get; set; }
    public Party Party { get; set; } = null!;

    public required string DisplayName { get; set; }

    /// <summary>
    /// Optional electoral coalition label (e.g. "ВЛЕН", "Твоја Македонија").
    /// Distinct from <see cref="Party"/>: coalitions are run-time alliances
    /// that several parties join for a single election cycle, whereas a
    /// member's <see cref="Party"/> is their formal political organization.
    /// </summary>
    public string? Coalition { get; set; }

    public CastTier Tier { get; set; } = CastTier.Chorus;

    public double Aggression { get; set; }
    public double Legalism { get; set; }
    public double Populism { get; set; }

    public int SeatIndex { get; set; }

    public string? PersonaSystemPrompt { get; set; }

    public List<SignatureMove> SignatureMoves { get; init; } = [];
}

public sealed class SignatureMove
{
    public int Id { get; init; }
    public required string MPId { get; set; }
    public MPProfile MP { get; set; } = null!;

    public required string Label { get; set; }
    public required string Exemplar { get; set; }
    public double TriggerWeight { get; set; } = 1.0;
}
