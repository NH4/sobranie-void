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

    /// <summary>
    /// Base persona system prompt (Macedonian Cyrillic) describing who
    /// this MP is: biographical spine, rhetorical voice, policy obsessions,
    /// signature tics. Stable across satire-intensity settings. Composed
    /// at request time with one of the three overlays below; see
    /// <c>SpeechGenerator.BuildMessages</c>. Null for Chorus MPs and until
    /// a real persona is authored.
    /// </summary>
    public string? PersonaCore { get; set; }

    /// <summary>
    /// Gentle-intensity style overlay: the MP's dry / earnest / restrained
    /// register. Appended to <see cref="PersonaCore"/> when
    /// <c>SobranieOptions.SatireIntensity = "gentle"</c>. Nullable.
    /// </summary>
    public string? PersonaOverlayGentle { get; set; }

    /// <summary>
    /// Sharp-intensity style overlay: the MP's adversarial, pointed,
    /// openly partisan register. Default overlay (<c>"sharp"</c>).
    /// Appended to <see cref="PersonaCore"/> at request time. Nullable.
    /// </summary>
    public string? PersonaOverlaySharp { get; set; }

    /// <summary>
    /// Absurd-intensity style overlay: heightened, satirical, cartoonish
    /// register. Appended to <see cref="PersonaCore"/> when
    /// <c>SobranieOptions.SatireIntensity = "absurd"</c>. Nullable.
    /// </summary>
    public string? PersonaOverlayAbsurd { get; set; }

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
