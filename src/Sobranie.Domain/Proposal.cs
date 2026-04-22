namespace Sobranie.Domain;

public enum ProposalStatus
{
    Queued = 0,
    InDebate = 1,
    Concluded = 2,
    Discarded = 3,
}

public sealed class Proposal
{
    public int Id { get; init; }
    public required string SourceUrl { get; set; }
    public required string Source { get; set; }
    public required string Headline { get; set; }
    public string? RewrittenAsProposal { get; set; }
    public DateTimeOffset FetchedAt { get; set; }
    public DateTimeOffset? IntroducedAt { get; set; }
    public ProposalStatus Status { get; set; } = ProposalStatus.Queued;
}
