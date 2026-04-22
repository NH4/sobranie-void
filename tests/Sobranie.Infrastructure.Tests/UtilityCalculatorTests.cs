using Microsoft.Extensions.Options;
using Sobranie.Domain;
using Sobranie.Infrastructure;
using Sobranie.Infrastructure.Fsm;

namespace Sobranie.Infrastructure.Tests;

public class UtilityCalculatorTests
{
    private static UtilityCalculator MakeCalculator(FsmOptions? overrides = null)
    {
        var opts = new SobranieOptions { Fsm = overrides ?? new FsmOptions() };
        return new UtilityCalculator(Options.Create(opts));
    }

    private static MPProfile MakeMp(string id, double populism = 0, double legalism = 0, double aggression = 0)
        => new()
        {
            MPId = id,
            PartyId = "p",
            DisplayName = id,
            Tier = CastTier.MainCast,
            Populism = populism,
            Legalism = legalism,
            Aggression = aggression,
        };

    [Fact]
    public void Score_NoProposalNoRecency_ReturnsBaseUtilityForAll()
    {
        var calc = MakeCalculator();
        var cast = new[] { MakeMp("a"), MakeMp("b"), MakeMp("c") };

        var scores = calc.Score(cast, new SessionContext(null, Array.Empty<Speech>()));

        Assert.Equal(3, scores.Count);
        Assert.All(scores, s => Assert.Equal(1.0, s.Score, precision: 6));
    }

    [Fact]
    public void Score_RecentSpeaker_GetsPenalized()
    {
        var calc = MakeCalculator();
        var cast = new[] { MakeMp("a"), MakeMp("b") };
        var recent = new[] { new Speech { MPId = "a", Content = "", UtteredAt = DateTimeOffset.UtcNow } };

        var scores = calc.Score(cast, new SessionContext(null, recent));

        var a = scores.First(s => s.MPId == "a");
        var b = scores.First(s => s.MPId == "b");
        Assert.True(a.Score < b.Score, $"a={a.Score} should be < b={b.Score} after recent speech");
        Assert.Equal(1.0, b.Score, precision: 6);
    }

    [Fact]
    public void Score_PopulistKeywordInProposal_BoostsPopulistMP()
    {
        var calc = MakeCalculator();
        var cast = new[]
        {
            MakeMp("populist", populism: 1.0),
            MakeMp("neutral"),
        };
        var proposal = new Proposal
        {
            Headline = "Зголемување на пензии за народот",
            RewrittenAsProposal = "",
            SourceUrl = "x",
            Source = "test",
            Status = ProposalStatus.InDebate,
        };

        var scores = calc.Score(cast, new SessionContext(proposal, Array.Empty<Speech>()));

        var populist = scores.First(s => s.MPId == "populist");
        var neutral = scores.First(s => s.MPId == "neutral");
        Assert.True(populist.Score > neutral.Score);
    }
}
