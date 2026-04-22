namespace Sobranie.Domain;

public sealed class Party
{
    public required string PartyId { get; init; }
    public required string DisplayName { get; set; }
    public required string ShortName { get; set; }
    public required string ColorHex { get; set; }
    public int SeatCount { get; set; }

    public List<MPProfile> Members { get; init; } = [];
    public List<ChorusLine> ChorusLines { get; init; } = [];
}
