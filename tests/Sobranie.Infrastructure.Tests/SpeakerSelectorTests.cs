using Microsoft.Extensions.Options;
using Sobranie.Infrastructure;
using Sobranie.Infrastructure.Fsm;

namespace Sobranie.Infrastructure.Tests;

public class SpeakerSelectorTests
{
    private static SpeakerSelector MakeSelector(double temperature = 0.6)
    {
        var opts = new SobranieOptions { Fsm = new FsmOptions { SoftmaxTemperature = temperature } };
        return new SpeakerSelector(Options.Create(opts));
    }

    [Fact]
    public void Softmax_WeightsSumToOne()
    {
        var scores = new[]
        {
            new UtilityScore("a", 1.0, ""),
            new UtilityScore("b", 2.0, ""),
            new UtilityScore("c", 0.5, ""),
        };

        var weights = SpeakerSelector.Softmax(scores, 0.6);

        Assert.Equal(1.0, weights.Sum(), precision: 6);
        Assert.All(weights, w => Assert.InRange(w, 0.0, 1.0));
    }

    [Fact]
    public void Softmax_HigherScoreYieldsHigherWeight()
    {
        var scores = new[]
        {
            new UtilityScore("low", 0.1, ""),
            new UtilityScore("high", 3.0, ""),
        };

        var weights = SpeakerSelector.Softmax(scores, 0.6);

        Assert.True(weights[1] > weights[0]);
    }

    [Fact]
    public void SelectSpeaker_WithSeededRng_IsDeterministic()
    {
        var selector = MakeSelector();
        var scores = new[]
        {
            new UtilityScore("a", 1.0, ""),
            new UtilityScore("b", 2.0, ""),
            new UtilityScore("c", 0.5, ""),
        };

        var first = selector.SelectSpeaker(scores, new Random(42));
        var second = selector.SelectSpeaker(scores, new Random(42));

        Assert.Equal(first, second);
    }

    [Fact]
    public void SelectSpeaker_EmptyScores_Throws()
    {
        var selector = MakeSelector();
        Assert.Throws<ArgumentException>(() => selector.SelectSpeaker(Array.Empty<UtilityScore>(), new Random(1)));
    }

    [Fact]
    public void Softmax_AllZeroScores_ReturnsUniform()
    {
        var scores = new[]
        {
            new UtilityScore("a", 0.0, ""),
            new UtilityScore("b", 0.0, ""),
            new UtilityScore("c", 0.0, ""),
        };

        var weights = SpeakerSelector.Softmax(scores, 0.6);

        Assert.All(weights, w => Assert.Equal(1.0 / 3.0, w, precision: 6));
    }
}
