namespace Sobranie.Domain;

public sealed class LlmCallLog
{
    public long Id { get; init; }
    public required string Model { get; set; }
    public required string Purpose { get; set; }
    public required string PromptHash { get; set; }
    public required string PromptPreview { get; set; }
    public string? Output { get; set; }
    public int PromptTokens { get; set; }
    public int OutputTokens { get; set; }
    public double PrefillSeconds { get; set; }
    public double GenerationSeconds { get; set; }
    public bool Rejected { get; set; }
    public string? RejectReason { get; set; }
    public DateTimeOffset CalledAt { get; set; }
}
