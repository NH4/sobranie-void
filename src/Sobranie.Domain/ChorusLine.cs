namespace Sobranie.Domain;

public sealed class ChorusLine
{
    public int Id { get; init; }
    public required string PartyId { get; set; }
    public Party Party { get; set; } = null!;

    public required string Text { get; set; }
    public required string TopicTag { get; set; }
    public double Weight { get; set; } = 1.0;
}
