namespace Sobranie.Domain;

public sealed class BeefEdge
{
    public int Id { get; init; }
    public required string FromMPId { get; set; }
    public required string ToMPId { get; set; }
    public double Score { get; set; }
    public DateTimeOffset LastUpdatedAt { get; set; }
}
