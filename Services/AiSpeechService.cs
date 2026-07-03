using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.SpeechScoring;
using System.Text.Json;

namespace SpeechBuddyAI.Services;

public class AiSpeechService
{
    public const string ScoringFormulaVersion = "score-v2.0-position-aware-consistency";

    private readonly ProgressTrackingService _progressTrackingService;
    private readonly IReadOnlyList<ISpeechScoringAdapter> _scoringAdapters;
    private readonly ConfidenceCalculator _confidenceCalculator;
    private readonly ConsistencyEstimator _consistencyEstimator;
    private readonly ConfidenceSettingsService? _confidenceSettingsService;

    public AiSpeechService(
        ProgressTrackingService progressTrackingService,
        IEnumerable<ISpeechScoringAdapter> scoringAdapters,
        ConfidenceCalculator confidenceCalculator,
        ConsistencyEstimator? consistencyEstimator = null,
        ConfidenceSettingsService? confidenceSettingsService = null)
    {
        _progressTrackingService = progressTrackingService;
        _scoringAdapters = scoringAdapters
            .OrderBy(a => a.Priority)
            .ToArray();
        _confidenceCalculator = confidenceCalculator ?? throw new ArgumentNullException(nameof(confidenceCalculator));
        _consistencyEstimator = consistencyEstimator ?? new ConsistencyEstimator();
        _confidenceSettingsService = confidenceSettingsService;

        if (_scoringAdapters.Count == 0)
        {
            throw new InvalidOperationException("No speech scoring adapters were registered.");
        }
    }

    public async Task<double> ScorePhonemeAsync(string expectedPhoneme, string transcript)
    {
        var normalizedPhoneme = (expectedPhoneme ?? string.Empty).Trim();
        var normalizedTranscript = (transcript ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedPhoneme) || string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            throw new ArgumentException("Expected phoneme and transcript are required.");
        }

        try
        {
            var result = await TryScoreWithFallbackAsync(normalizedPhoneme, normalizedTranscript, Array.Empty<ProgressEntry>());
            return result.PhonemeScore;
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException("Failed to score phoneme.", ex);
        }
    }

    public async Task<PracticeAttemptResult> EvaluateAndPersistAttemptAsync(string targetSound, string transcript)
    {
        var normalizedTarget = (targetSound ?? string.Empty).Trim();
        var normalizedTranscript = (transcript ?? string.Empty).Trim();
        var (baseTarget, positionTag) = ParseTargetMetadata(normalizedTarget);

        if (string.IsNullOrWhiteSpace(normalizedTarget) || string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            throw new ArgumentException("Target sound and transcript are required for scoring.");
        }

        try
        {
            var priorEntries = await _progressTrackingService.GetEntriesForSoundAsync(baseTarget);
            var consistencyProfile = _consistencyEstimator.EstimateProfile(priorEntries, positionTag);
            var consistency = consistencyProfile.Score;
            var adapterResult = await TryScoreWithFallbackAsync(baseTarget, normalizedTranscript, priorEntries);
            var scores = ComposeScoreComponents(adapterResult.PhonemeScore, adapterResult.FluencyScore, consistency);
            var calibrationContext = BuildCalibrationContext(priorEntries, positionTag);
            var calibrationTable = _confidenceCalculator.BuildCalibrationTable(calibrationContext.RecentPool);
            var rawConfidenceScore = _confidenceCalculator.ComputeRawScore(
                scores,
                normalizedTranscript,
                priorEntries.Count,
                adapterResult.Provider,
                consistencyProfile.Uncertainty,
                consistencyProfile.UncertaintyBand);
            var adaptiveThresholds = _confidenceCalculator.ComputeAdaptiveThresholds(priorEntries, consistencyProfile.Uncertainty);
            var confidenceScore = _confidenceCalculator.ComputeScore(
                scores,
                normalizedTranscript,
                priorEntries.Count,
                adapterResult.Provider,
                consistencyProfile.Uncertainty,
                consistencyProfile.UncertaintyBand,
                calibrationContext.OutcomeMean,
                calibrationContext.Support,
                calibrationTable);
            var confidenceBand = _confidenceCalculator.ComputeBand(confidenceScore, adaptiveThresholds);
            var drift = DetectHistoricalDrift(priorEntries, scores.OverallScore);
            var uncertainty = _confidenceCalculator.ComputeUncertaintyDecomposition(calibrationContext.RecentPool, consistencyProfile.Uncertainty);
            var calibrationResidual = Math.Abs(rawConfidenceScore - calibrationContext.OutcomeMean);

            var trialCount = priorEntries.Count + 1;
            var entry = new ProgressEntry
            {
                Timestamp = DateTime.UtcNow,
                TargetSound = baseTarget,
                BaseTargetSound = baseTarget,
                PositionTag = positionTag,
                Transcript = normalizedTranscript,
                AccuracyScore = scores.OverallScore,
                PhonemeScore = scores.PhonemeScore,
                FluencyScore = scores.FluencyScore,
                ConsistencyScore = scores.ConsistencyScore,
                OverallScore = scores.OverallScore,
                TrialCount = trialCount,
                ErrorPattern = InferErrorPattern(scores),
                ScoringProvider = adapterResult.Provider,
                ConfidenceScore = confidenceScore,
                ConfidenceBand = confidenceBand,
                ScoringFormulaVersion = ScoringFormulaVersion,
                HistoricalDriftDetected = drift.Detected,
                HistoricalDriftZScore = drift.ZScore,
                HistoricalDriftSummary = drift.Summary,
                AdaptiveModerateThreshold = adaptiveThresholds.ModerateThreshold,
                AdaptiveHighThreshold = adaptiveThresholds.HighThreshold,
                RawConfidenceScore = rawConfidenceScore,
                EmpiricalOutcomeMean = calibrationContext.OutcomeMean,
                EmpiricalOutcomeSupport = calibrationContext.Support,
                CalibrationResidual = calibrationResidual,
                CalibrationMethod = calibrationTable.IsActive ? "isotonic-binned+empirical-shrinkage" : "empirical-shrinkage",
                CalibrationTableJson = SerializeCalibrationTable(calibrationTable),
                VarianceUncertaintyComponent = uncertainty.VarianceDriven,
                SparsityUncertaintyComponent = uncertainty.SparsityDriven
            };

            await _progressTrackingService.AddEntryAsync(entry);

            return new PracticeAttemptResult
            {
                Scores = scores,
                Entry = entry,
                Provider = adapterResult.Provider,
                ConfidenceScore = confidenceScore,
                ConfidenceBand = confidenceBand,
                ScoringFormulaVersion = ScoringFormulaVersion,
                HistoricalDriftDetected = drift.Detected,
                HistoricalDriftZScore = drift.ZScore,
                HistoricalDriftSummary = drift.Summary
            };
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException("Failed to evaluate and persist practice attempt.", ex);
        }
    }

    private async Task<AdapterScoreResult> TryScoreWithFallbackAsync(
        string targetSound,
        string transcript,
        IReadOnlyList<ProgressEntry> priorEntries)
    {
        if (string.IsNullOrWhiteSpace(targetSound) || string.IsNullOrWhiteSpace(transcript))
        {
            throw new ArgumentException("Target sound and transcript are required for adapter scoring.");
        }

        Exception? lastException = null;

        foreach (var adapter in _scoringAdapters)
        {
            try
            {
                return await adapter.ScoreAsync(targetSound, transcript, priorEntries);
            }
            catch (Exception ex)
            {
                lastException = ex;
            }
        }

        throw new InvalidOperationException(
            "No scoring adapter produced a valid score.",
            lastException);
    }

    private static ScoreComponents ComposeScoreComponents(double phonemeScore, double fluencyScore, double consistency)
    {
        try
        {
            var phoneme = Clamp(phonemeScore);
            var fluency = Clamp(fluencyScore);
            var consistencyScore = Clamp(consistency);
            var overall = Clamp(0.6 * phoneme + 0.25 * fluency + 0.15 * consistencyScore);

            return new ScoreComponents
            {
                PhonemeScore = phoneme,
                FluencyScore = fluency,
                ConsistencyScore = consistencyScore,
                OverallScore = overall
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to compose score components.", ex);
        }
    }

    private static string InferErrorPattern(ScoreComponents scores)
    {
        if (scores.PhonemeScore < 0.5)
        {
            return "phoneme_mismatch";
        }

        if (scores.FluencyScore < 0.55)
        {
            return "fluency_instability";
        }

        if (scores.ConsistencyScore < 0.55)
        {
            return "inconsistent_attempts";
        }

        return "none";
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }

    private static HistoricalDriftState DetectHistoricalDrift(
        IReadOnlyList<ProgressEntry> priorEntries,
        double currentOverallScore)
    {
        var source = priorEntries ?? Array.Empty<ProgressEntry>();
        if (source.Count < 6)
        {
            return new HistoricalDriftState(false, 0.0, "Insufficient history for drift detection.");
        }

        var baseline = source
            .OrderByDescending(entry => entry.Timestamp)
            .Take(12)
            .Select(entry => Clamp(entry.OverallScore))
            .ToArray();

        var mean = baseline.Average();
        var variance = baseline.Average(value => Math.Pow(value - mean, 2));
        var stdDev = Math.Sqrt(Math.Max(variance, 1e-6));
        var zScore = (Clamp(currentOverallScore) - mean) / stdDev;
        var boundedZ = Math.Clamp(zScore, -3.0, 3.0);
        var detected = Math.Abs(boundedZ) >= 2.0;

        var summary = detected
            ? $"Drift flagged between legacy scoring profile and {ScoringFormulaVersion}; z={boundedZ:+0.00;-0.00;0.00}."
            : $"No abrupt drift detected against historical baseline; z={boundedZ:+0.00;-0.00;0.00}.";

        return new HistoricalDriftState(detected, boundedZ, summary);
    }

    private static (string BaseTarget, string PositionTag) ParseTargetMetadata(string target)
    {
        var normalized = (target ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (string.Empty, string.Empty);
        }

        var parts = normalized.Split(':', 2);
        var baseTarget = parts[0].Trim();
        var position = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return (baseTarget, position);
    }

    private ConfidenceCalibrationContext BuildCalibrationContext(
        IReadOnlyList<ProgressEntry> priorEntries,
        string positionTag)
    {
        var source = priorEntries ?? Array.Empty<ProgressEntry>();
        if (source.Count == 0)
        {
            return new ConfidenceCalibrationContext(0.5, 0.0, Array.Empty<ProgressEntry>());
        }

        var coefficients = _confidenceSettingsService?.GetCalibrationPositionSupportCoefficients()
            ?? ConfidenceSettingsService.DefaultCalibrationPositionSupportCoefficients;
        var normalizedPosition = (positionTag ?? string.Empty).Trim().ToLowerInvariant();
        var positionScoped = source
            .Where(entry => string.Equals((entry.PositionTag ?? string.Empty).Trim(), normalizedPosition, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(entry => entry.Timestamp)
            .Take(12)
            .ToArray();

        var pool = positionScoped.Length >= 4
            ? positionScoped
            : source
                .OrderByDescending(entry => entry.Timestamp)
                .Take(12)
                .ToArray();

        var outcomeMean = pool.Average(entry => Clamp(entry.OverallScore));
        var weightedSupport = pool.Sum(entry => ResolvePositionSupportWeight((entry.PositionTag ?? string.Empty).Trim(), coefficients));
        var maxCoefficient = Math.Max(0.001, Math.Max(coefficients.InitialCoefficient, Math.Max(coefficients.MedialCoefficient, coefficients.FinalCoefficient)));
        var support = Clamp(weightedSupport / (12.0 * maxCoefficient));

        return new ConfidenceCalibrationContext(outcomeMean, support, pool);
    }

    private static double ResolvePositionSupportWeight(string positionTag, PositionSupportCoefficients coefficients)
    {
        var normalized = (positionTag ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "initial" => coefficients.InitialCoefficient,
            "medial" => coefficients.MedialCoefficient,
            "final" => coefficients.FinalCoefficient,
            _ => Math.Max(coefficients.InitialCoefficient, Math.Max(coefficients.MedialCoefficient, coefficients.FinalCoefficient))
        };
    }

    private static string SerializeCalibrationTable(ConfidenceCalibrationTable table)
    {
        if (table is null || !table.IsActive || table.Bins.Count == 0)
        {
            return string.Empty;
        }

        var payload = new
        {
            support = table.Support,
            bins = table.Bins.Select(bin => new
            {
                lower = bin.LowerBound,
                upper = bin.UpperBound,
                calibrated = bin.CalibratedOutcome,
                count = bin.SampleCount
            })
        };

        return JsonSerializer.Serialize(payload);
    }

    private sealed record HistoricalDriftState(bool Detected, double ZScore, string Summary);
    private sealed record ConfidenceCalibrationContext(double OutcomeMean, double Support, IReadOnlyList<ProgressEntry> RecentPool);
}
