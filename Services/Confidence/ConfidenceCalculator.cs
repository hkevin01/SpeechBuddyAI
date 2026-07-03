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
        string consistencyUncertaintyBand = "ModerateSupport",
        double empiricalOutcomeMean = 0.5,
        double empiricalOutcomeSupport = 0.0,
        ConfidenceCalibrationTable? calibrationTable = null)
    {
        var provisional = ComputeRawScore(
            scores,
            transcript,
            priorEntryCount,
            provider,
            consistencyUncertainty,
            consistencyUncertaintyBand);

        var tableAdjusted = provisional;
        if (calibrationTable is not null && calibrationTable.IsActive)
        {
            var mapped = MapConfidenceThroughTable(calibrationTable, provisional);
            var tableBlendWeight = 0.45 * Clamp(calibrationTable.Support);
            tableAdjusted = Clamp(tableAdjusted + (tableBlendWeight * (mapped - tableAdjusted)));
        }

        var outcomeMean = Clamp(empiricalOutcomeMean);
        var support = Clamp(empiricalOutcomeSupport);

        if (support <= 0.0)
        {
            return tableAdjusted;
        }

        var shrinkWeight = 0.35 * support;
        var shrunk = tableAdjusted + (shrinkWeight * (outcomeMean - tableAdjusted));
        var calibrationPenalty = Math.Abs(tableAdjusted - outcomeMean) * 0.08 * support;

        return Clamp(shrunk - calibrationPenalty);
    }

    public double ComputeRawScore(
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

    public ConfidenceCalibrationTable BuildCalibrationTable(
        IReadOnlyList<ProgressEntry> history,
        int minSamples = 8,
        int binCount = 6)
    {
        var source = history ?? Array.Empty<ProgressEntry>();
        var samples = source
            .Select(entry => new
            {
                Predictor = Clamp(entry.RawConfidenceScore > 0 ? entry.RawConfidenceScore : entry.ConfidenceScore),
                Outcome = Clamp(entry.OverallScore)
            })
            .ToArray();

        if (samples.Length < Math.Max(4, minSamples))
        {
            return ConfidenceCalibrationTable.Inactive;
        }

        var effectiveBinCount = Math.Clamp(binCount, 3, 12);
        var binWidth = 1.0 / effectiveBinCount;
        var binMeans = new double[effectiveBinCount];
        var binCounts = new int[effectiveBinCount];

        foreach (var sample in samples)
        {
            var index = Math.Min((int)(sample.Predictor / binWidth), effectiveBinCount - 1);
            binMeans[index] += sample.Outcome;
            binCounts[index]++;
        }

        for (var i = 0; i < effectiveBinCount; i++)
        {
            binMeans[i] = binCounts[i] > 0 ? binMeans[i] / binCounts[i] : double.NaN;
        }

        for (var i = 0; i < effectiveBinCount; i++)
        {
            if (!double.IsNaN(binMeans[i]))
            {
                continue;
            }

            var left = i - 1;
            while (left >= 0 && double.IsNaN(binMeans[left]))
            {
                left--;
            }

            var right = i + 1;
            while (right < effectiveBinCount && double.IsNaN(binMeans[right]))
            {
                right++;
            }

            if (left >= 0 && right < effectiveBinCount)
            {
                var leftDistance = i - left;
                var rightDistance = right - i;
                var leftWeight = 1.0 / leftDistance;
                var rightWeight = 1.0 / rightDistance;
                binMeans[i] = ((binMeans[left] * leftWeight) + (binMeans[right] * rightWeight)) / (leftWeight + rightWeight);
            }
            else if (left >= 0)
            {
                binMeans[i] = binMeans[left];
            }
            else if (right < effectiveBinCount)
            {
                binMeans[i] = binMeans[right];
            }
            else
            {
                binMeans[i] = 0.5;
            }
        }

        var levelValues = new List<double>();
        var levelWeights = new List<double>();
        var levelStarts = new List<int>();
        var levelEnds = new List<int>();

        for (var i = 0; i < effectiveBinCount; i++)
        {
            levelValues.Add(binMeans[i]);
            levelWeights.Add(Math.Max(1.0, binCounts[i]));
            levelStarts.Add(i);
            levelEnds.Add(i);

            while (levelValues.Count >= 2 && levelValues[^2] > levelValues[^1])
            {
                var last = levelValues.Count - 1;
                var mergedWeight = levelWeights[last - 1] + levelWeights[last];
                var mergedValue = ((levelValues[last - 1] * levelWeights[last - 1]) + (levelValues[last] * levelWeights[last])) / mergedWeight;

                levelValues[last - 1] = mergedValue;
                levelWeights[last - 1] = mergedWeight;
                levelEnds[last - 1] = levelEnds[last];

                levelValues.RemoveAt(last);
                levelWeights.RemoveAt(last);
                levelStarts.RemoveAt(last);
                levelEnds.RemoveAt(last);
            }
        }

        var monotonicMeans = new double[effectiveBinCount];
        for (var i = 0; i < levelValues.Count; i++)
        {
            for (var index = levelStarts[i]; index <= levelEnds[i]; index++)
            {
                monotonicMeans[index] = Clamp(levelValues[i]);
            }
        }

        var bins = new List<ConfidenceCalibrationBin>(effectiveBinCount);
        for (var i = 0; i < effectiveBinCount; i++)
        {
            var lower = i * binWidth;
            var upper = i == effectiveBinCount - 1 ? 1.0 : (i + 1) * binWidth;
            bins.Add(new ConfidenceCalibrationBin(lower, upper, monotonicMeans[i], binCounts[i]));
        }

        var support = Math.Min(samples.Length, 24) / 24.0;
        return new ConfidenceCalibrationTable(bins, support, true);
    }

    public UncertaintyDecomposition ComputeUncertaintyDecomposition(
        IReadOnlyList<ProgressEntry> targetHistory,
        double consistencyUncertainty)
    {
        var history = targetHistory ?? Array.Empty<ProgressEntry>();
        if (history.Count == 0)
        {
            return new UncertaintyDecomposition(0.0, 1.0);
        }

        var recent = history
            .OrderByDescending(entry => entry.Timestamp)
            .Take(12)
            .ToArray();
        var values = recent
            .Select(entry => Clamp(entry.OverallScore))
            .ToArray();

        var variance = ComputeVariance(values);
        var normalizedVariance = Math.Min(variance / 0.05, 1.0);
        var supportFactor = Math.Min(recent.Length, 12) / 12.0;
        var sparsity = 1.0 - supportFactor;
        var consistency = Clamp(consistencyUncertainty);

        var varianceDriven = Clamp((0.65 * normalizedVariance) + (0.35 * consistency));
        var sparsityDriven = Clamp((0.70 * sparsity) + (0.30 * consistency));
        return new UncertaintyDecomposition(varianceDriven, sparsityDriven);
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

    private static double MapConfidenceThroughTable(ConfidenceCalibrationTable table, double provisionalScore)
    {
        var score = Clamp(provisionalScore);
        var bins = table.Bins;
        if (bins.Count == 0)
        {
            return score;
        }

        foreach (var bin in bins)
        {
            if (score >= bin.LowerBound && score <= bin.UpperBound)
            {
                return Clamp(bin.CalibratedOutcome);
            }
        }

        return Clamp(bins[^1].CalibratedOutcome);
    }
}

public sealed record ConfidenceCalibrationBin(
    double LowerBound,
    double UpperBound,
    double CalibratedOutcome,
    int SampleCount);

public sealed record ConfidenceCalibrationTable(
    IReadOnlyList<ConfidenceCalibrationBin> Bins,
    double Support,
    bool IsActive)
{
    public static ConfidenceCalibrationTable Inactive { get; } = new(Array.Empty<ConfidenceCalibrationBin>(), 0.0, false);
}

public sealed record UncertaintyDecomposition(
    double VarianceDriven,
    double SparsityDriven);
