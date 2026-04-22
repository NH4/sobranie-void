using Microsoft.Extensions.Options;

namespace Sobranie.Infrastructure.Fsm;

public sealed class SpeakerSelector(IOptions<SobranieOptions> options)
{
    private readonly FsmOptions fsm = options.Value.Fsm;

    public string SelectSpeaker(IReadOnlyList<UtilityScore> scores, Random rng)
    {
        ArgumentNullException.ThrowIfNull(scores);
        ArgumentNullException.ThrowIfNull(rng);

        if (scores.Count == 0)
        {
            throw new ArgumentException("Cannot select from empty score list.", nameof(scores));
        }

        var weights = Softmax(scores, fsm.SoftmaxTemperature);
        var r = rng.NextDouble();
        var cumulative = 0.0;
        for (var i = 0; i < scores.Count; i++)
        {
            cumulative += weights[i];
            if (r <= cumulative)
            {
                return scores[i].MPId;
            }
        }

        return scores[^1].MPId;
    }

    internal static double[] Softmax(IReadOnlyList<UtilityScore> scores, double temperature)
    {
        var t = Math.Max(0.01, temperature);
        var max = double.NegativeInfinity;
        for (var i = 0; i < scores.Count; i++)
        {
            if (scores[i].Score > max)
            {
                max = scores[i].Score;
            }
        }

        var exps = new double[scores.Count];
        var sum = 0.0;
        for (var i = 0; i < scores.Count; i++)
        {
            var e = Math.Exp((scores[i].Score - max) / t);
            exps[i] = e;
            sum += e;
        }

        if (sum <= 0.0 || double.IsNaN(sum))
        {
            var uniform = 1.0 / scores.Count;
            for (var i = 0; i < scores.Count; i++)
            {
                exps[i] = uniform;
            }

            return exps;
        }

        for (var i = 0; i < scores.Count; i++)
        {
            exps[i] /= sum;
        }

        return exps;
    }
}
