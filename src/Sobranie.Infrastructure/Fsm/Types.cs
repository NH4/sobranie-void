using Sobranie.Domain;

namespace Sobranie.Infrastructure.Fsm;

public readonly record struct UtilityScore(string MPId, double Score, string Rationale);

public readonly record struct SessionContext(
    Proposal? CurrentProposal,
    IReadOnlyList<Speech> RecentSpeeches);
