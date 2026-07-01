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
}
