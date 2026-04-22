using Microsoft.Extensions.Options;
using Sobranie.Domain;

namespace Sobranie.Infrastructure.Fsm;

public sealed class UtilityCalculator(IOptions<SobranieOptions> options)
{
    private readonly FsmOptions fsm = options.Value.Fsm;

    public IReadOnlyList<UtilityScore> Score(
        IReadOnlyList<MPProfile> mainCast,
        SessionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(mainCast);

        var recencyByMP = BuildRecencyMap(ctx.RecentSpeeches);
        var result = new List<UtilityScore>(mainCast.Count);

        foreach (var mp in mainCast)
        {
            var (score, rationale) = ScoreOne(mp, ctx, recencyByMP);
            result.Add(new UtilityScore(mp.MPId, score, rationale));
        }

        return result;
    }

    private (double Score, string Rationale) ScoreOne(
        MPProfile mp,
        SessionContext ctx,
        Dictionary<string, int> recencyByMP)
    {
        var baseScore = fsm.BaseUtility;

        var traitComponent = 0.0;
        if (ctx.CurrentProposal is { } proposal)
        {
            traitComponent = TraitMatchForProposal(mp, proposal) * fsm.TraitMatchWeight;
        }

        var recencyPenalty = 0.0;
        if (recencyByMP.TryGetValue(mp.MPId, out var turnsAgo))
        {
            var halfLife = Math.Max(0.1, fsm.RecencyPenaltyHalfLifeSpeeches);
            recencyPenalty = fsm.RecencyPenaltyMagnitude * Math.Pow(0.5, turnsAgo / halfLife);
        }

        var score = Math.Max(0.0, baseScore + traitComponent - recencyPenalty);
        var rationale = $"base={baseScore:F2} trait={traitComponent:+0.00;-0.00;0.00} recency={-recencyPenalty:+0.00;-0.00;0.00}";
        return (score, rationale);
    }

    private static double TraitMatchForProposal(MPProfile mp, Proposal proposal)
    {
        var headline = (proposal.Headline ?? string.Empty) + " " + (proposal.RewrittenAsProposal ?? string.Empty);
        headline = headline.ToLowerInvariant();

        var populistKeywords = new[] { "народ", "пензи", "плат", "работни", "семејст" };
        var legalistKeywords = new[] { "закон", "устав", "член", "постапк", "суд" };
        var aggressionKeywords = new[] { "корупц", "криминал", "скандал", "афер", "злоупотреб" };

        var populistHit = populistKeywords.Any(k => headline.Contains(k, StringComparison.Ordinal));
        var legalistHit = legalistKeywords.Any(k => headline.Contains(k, StringComparison.Ordinal));
        var aggressionHit = aggressionKeywords.Any(k => headline.Contains(k, StringComparison.Ordinal));

        var match = 0.0;
        if (populistHit) match += mp.Populism;
        if (legalistHit) match += mp.Legalism;
        if (aggressionHit) match += mp.Aggression;

        return match;
    }

    private static Dictionary<string, int> BuildRecencyMap(IReadOnlyList<Speech> recent)
    {
        var map = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < recent.Count; i++)
        {
            var turnsAgo = i;
            var mpId = recent[i].MPId;
            if (mpId is null)
            {
                continue;
            }

            if (!map.ContainsKey(mpId))
            {
                map[mpId] = turnsAgo;
            }
        }

        return map;
    }
}
