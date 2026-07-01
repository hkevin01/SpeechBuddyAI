using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.SpeechScoring;

public sealed class ConsistencyEstimator
{
    private const int MinimumWindow = 3;
    private const int MaximumWindow = 10;

    public double Estimate(IReadOnlyList<ProgressEntry> entries, string? positionTag)
    {
        return EstimateProfile(entries, positionTag).Score;
    }

    public ConsistencyEstimateProfile EstimateProfile(IReadOnlyList<ProgressEntry> entries, string? positionTag)
    {
        var sourceEntries = entries ?? Array.Empty<ProgressEntry>();
        if (sourceEntries.Count < 2)
        {
            return new ConsistencyEstimateProfile(0.5, 1.0, "LowSupport", sourceEntries.Count);
        }

        try
        {
            var scoped = FilterByPositionWithFallback(sourceEntries, positionTag);
            if (scoped.Count < 2)
            {
                return new ConsistencyEstimateProfile(0.5, 1.0, "LowSupport", scoped.Count);
            }

            var ordered = scoped
                .OrderBy(entry => entry.Timestamp)
                .TakeLast(ResolveAdaptiveWindow(scoped.Count))
                .ToArray();

            var overall = ordered
                .Select(entry => Clamp(entry.OverallScore))
                .ToArray();

            var variance = ComputeExponentiallyWeightedVariance(overall);
            var normalizedVariance = Math.Min(variance / 0.06, 1.0);

            var divergence = ordered
                .Select(entry =>
                {
                    var overallScore = Clamp(entry.OverallScore);
                    var phonemeGap = Math.Abs(Clamp(entry.PhonemeScore) - overallScore);
                    var fluencyGap = Math.Abs(Clamp(entry.FluencyScore) - overallScore);
                    return (phonemeGap + fluencyGap) / 2.0;
                })
                .DefaultIfEmpty(0.0)
                .Average();

            var divergencePenalty = Math.Min(divergence / 0.35, 1.0) * 0.25;
            var trendBonus = ComputeTrendBonus(overall);

            var score = Clamp(1.0 - normalizedVariance - divergencePenalty + trendBonus);
            var supportScore = Clamp(scoped.Count / 10.0);
            var uncertainty = Clamp((1.0 - supportScore) * 0.7 + normalizedVariance * 0.3);
            var band = uncertainty > 0.66
                ? "LowSupport"
                : uncertainty > 0.33
                    ? "ModerateSupport"
                    : "HighSupport";

            return new ConsistencyEstimateProfile(score, uncertainty, band, scoped.Count);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to estimate attempt consistency.", ex);
        }
    }

    private static IReadOnlyList<ProgressEntry> FilterByPositionWithFallback(
        IReadOnlyList<ProgressEntry> entries,
        string? positionTag)
    {
        var normalizedPosition = Normalize(positionTag);
        if (string.IsNullOrWhiteSpace(normalizedPosition))
        {
            return entries;
        }

        var scoped = entries
            .Where(entry => string.Equals(Normalize(entry.PositionTag), normalizedPosition, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return scoped.Length >= 3 ? scoped : entries;
    }

    private static int ResolveAdaptiveWindow(int sampleCount)
    {
        var proposed = Math.Max(MinimumWindow, sampleCount / 2);
        return Math.Min(MaximumWindow, proposed);
    }

    private static double ComputeExponentiallyWeightedVariance(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
        {
            return 0.0;
        }

        var tau = Math.Max(2.0, values.Count / 2.0);
        var weighted = values
            .Select((value, index) => new
            {
                Value = value,
                Weight = Math.Exp(-(values.Count - 1 - index) / tau)
            })
            .ToArray();

        var weightTotal = weighted.Sum(item => item.Weight);
        if (weightTotal <= 0)
        {
            return 0.0;
        }

        var mean = weighted.Sum(item => item.Value * item.Weight) / weightTotal;
        return weighted.Sum(item => item.Weight * Math.Pow(item.Value - mean, 2)) / weightTotal;
    }

    private static double ComputeTrendBonus(IReadOnlyList<double> values)
    {
        if (values.Count < 3)
        {
            return 0.0;
        }

        var recent = values.TakeLast(3).ToArray();
        return recent[2] >= recent[1] && recent[1] >= recent[0] ? 0.05 : 0.0;
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty).Trim().ToLowerInvariant();
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }

    public sealed record ConsistencyEstimateProfile(
        double Score,
        double Uncertainty,
        string UncertaintyBand,
        int EffectiveSampleCount);
}
