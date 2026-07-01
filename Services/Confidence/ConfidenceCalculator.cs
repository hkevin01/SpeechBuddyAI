using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services.Confidence;

public sealed class ConfidenceCalculator
{
    private readonly IConfidenceThresholdProvider _thresholdProvider;

    public ConfidenceCalculator(IConfidenceThresholdProvider thresholdProvider)
    {
        _thresholdProvider = thresholdProvider ?? throw new ArgumentNullException(nameof(thresholdProvider));
    }

    public double ComputeScore(
        ScoreComponents scores,
        string transcript,
        int priorEntryCount,
        string provider,
        double consistencyUncertainty = 0.5,
        string consistencyUncertaintyBand = "ModerateSupport")
    {
        if (scores is null)
        {
            throw new ArgumentNullException(nameof(scores));
        }

        var normalizedTranscript = (transcript ?? string.Empty).Trim();
        var normalizedProvider = (provider ?? string.Empty).Trim();
        var historyCount = Math.Max(0, priorEntryCount);

        var tokenCount = normalizedTranscript
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        var transcriptSignal = Math.Min(tokenCount, 8) / 8.0;
        var providerBonus = normalizedProvider.Contains("offline", StringComparison.OrdinalIgnoreCase) ? 0.08 : 0.03;
        var historySignal = Math.Min(historyCount, 10) / 10.0;
        var scoreSpread = Math.Abs(scores.PhonemeScore - scores.FluencyScore);
        var spreadPenalty = Math.Min(scoreSpread, 0.45);
        var uncertainty = Clamp(consistencyUncertainty);

        var consistencyCalibration = ComputeConsistencyCalibrationTerm(
            historyCount,
            uncertainty,
            consistencyUncertaintyBand);

        var rawScore =
            (0.45 * Clamp(scores.OverallScore)) +
            (0.2 * Clamp(scores.ConsistencyScore)) +
            (0.2 * transcriptSignal) +
            (0.1 * historySignal) +
            consistencyCalibration +
            providerBonus -
            (0.15 * spreadPenalty);

        return Clamp(rawScore);
    }

    public string ComputeBand(double confidenceScore)
    {
        var thresholds = _thresholdProvider.GetThresholds();
        return ComputeBand(confidenceScore, thresholds);
    }

    public string ComputeBand(double confidenceScore, ConfidenceThresholds thresholds)
    {
        var score = Clamp(confidenceScore);

        if (score >= thresholds.HighThreshold)
        {
            return "High";
        }

        if (score >= thresholds.ModerateThreshold)
        {
            return "Moderate";
        }

        return "Low";
    }

    public ConfidenceThresholds ComputeAdaptiveThresholds(
        IReadOnlyList<ProgressEntry> targetHistory,
        double consistencyUncertainty)
    {
        var baseline = _thresholdProvider.GetThresholds();
        var history = targetHistory ?? Array.Empty<ProgressEntry>();

        if (history.Count == 0)
        {
            return baseline;
        }

        var recent = history
            .OrderByDescending(entry => entry.Timestamp)
            .Take(12)
            .ToArray();

        var confidenceValues = recent
            .Select(entry => Clamp(entry.ConfidenceScore <= 0 ? 0.5 : entry.ConfidenceScore))
            .ToArray();
        var variance = ComputeVariance(confidenceValues);
        var variancePenalty = Math.Min(variance / 0.05, 1.0) * 0.06;

        var supportFactor = Math.Min(recent.Length, 12) / 12.0;
        var supportAdjustment = (1.0 - supportFactor) * 0.04;
        var uncertaintyPenalty = Clamp(consistencyUncertainty) * 0.07;

        var moderate = Clamp(baseline.ModerateThreshold + variancePenalty + supportAdjustment + uncertaintyPenalty);
        var high = Clamp(baseline.HighThreshold + (0.9 * variancePenalty) + (0.8 * supportAdjustment) + (0.85 * uncertaintyPenalty));

        if (high <= moderate)
        {
            high = Math.Min(1.0, moderate + 0.08);
        }

        if (high <= moderate)
        {
            moderate = Math.Max(0.0, high - 0.05);
        }

        return new ConfidenceThresholds(moderate, high);
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }

    private static double ComputeConsistencyCalibrationTerm(
        int historyCount,
        double uncertainty,
        string uncertaintyBand)
    {
        var supportFactor = Math.Min(historyCount, 12) / 12.0;
        var baseTerm = (1.0 - uncertainty) * 0.07;
        var supportPenalty = (1.0 - supportFactor) * 0.05;

        var bandAdjustment = uncertaintyBand switch
        {
            "LowSupport" => -0.04,
            "ModerateSupport" => -0.01,
            "HighSupport" => 0.01,
            _ => 0.0
        };

        return baseTerm - supportPenalty + bandAdjustment;
    }

    private static double ComputeVariance(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = values.Average();
        return values.Average(value => Math.Pow(value - mean, 2));
    }
}
