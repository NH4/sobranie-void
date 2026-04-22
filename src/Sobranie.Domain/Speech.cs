namespace Sobranie.Domain;

public enum SpeechKind
{
    MainCastSpeech = 0,
    Interjection = 1,
    ChorusReaction = 2,
    SpeakerAction = 3,
}

public sealed class Speech
{
    public long Id { get; init; }
    public required string MPId { get; set; }
    public MPProfile? MP { get; set; }

    public int? ProposalId { get; set; }
    public Proposal? Proposal { get; set; }

    public SpeechKind Kind { get; set; } = SpeechKind.MainCastSpeech;
    public required string Content { get; set; }
    public DateTimeOffset UttereredAt { get; set; }

    public int TokenCount { get; set; }
    public double GenerationSeconds { get; set; }
    public double? UtilityAtSelection { get; set; }
    public bool OutputFilterRejected { get; set; }
    public string? RejectReason { get; set; }
}
