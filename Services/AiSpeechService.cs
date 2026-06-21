using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.SpeechScoring;

namespace SpeechBuddyAI.Services;

public class AiSpeechService
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly IReadOnlyList<ISpeechScoringAdapter> _scoringAdapters;

    public AiSpeechService(
        ProgressTrackingService progressTrackingService,
        IEnumerable<ISpeechScoringAdapter> scoringAdapters)
    {
        _progressTrackingService = progressTrackingService;
        _scoringAdapters = scoringAdapters
            .OrderBy(a => a.Priority)
            .ToArray();

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

        if (string.IsNullOrWhiteSpace(normalizedTarget) || string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            throw new ArgumentException("Target sound and transcript are required for scoring.");
        }

        try
        {
            var priorEntries = await _progressTrackingService.GetEntriesForSoundAsync(normalizedTarget);
            var consistency = ComputeConsistencyScore(priorEntries);
            var adapterResult = await TryScoreWithFallbackAsync(normalizedTarget, normalizedTranscript, priorEntries);
            var scores = ComposeScoreComponents(adapterResult.PhonemeScore, adapterResult.FluencyScore, consistency);
            var confidenceScore = ComputeConfidenceScore(scores, normalizedTranscript, priorEntries, adapterResult.Provider);
            var confidenceBand = ComputeConfidenceBand(confidenceScore);

            var trialCount = priorEntries.Count + 1;
            var entry = new ProgressEntry
            {
                Timestamp = DateTime.UtcNow,
                TargetSound = normalizedTarget,
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
                ConfidenceBand = confidenceBand
            };

            await _progressTrackingService.AddEntryAsync(entry);

            return new PracticeAttemptResult
            {
                Scores = scores,
                Entry = entry,
                Provider = adapterResult.Provider,
                ConfidenceScore = confidenceScore,
                ConfidenceBand = confidenceBand
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

    private static double ComputeConsistencyScore(IReadOnlyList<ProgressEntry> entries)
    {
        var sourceEntries = entries ?? Array.Empty<ProgressEntry>();

        if (sourceEntries.Count < 2)
        {
            return 0.5;
        }

        try
        {
            var recent = sourceEntries
                .OrderByDescending(e => e.Timestamp)
                .Take(5)
                .Select(e => e.OverallScore)
                .ToArray();

            var mean = recent.Average();
            var variance = recent.Average(s => Math.Pow(s - mean, 2));
            var normalizedVariance = Math.Min(variance / 0.08, 1.0);
            return Clamp(1.0 - normalizedVariance);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to compute consistency score.", ex);
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

    private static double ComputeConfidenceScore(
        ScoreComponents scores,
        string transcript,
        IReadOnlyList<ProgressEntry> priorEntries,
        string provider)
    {
        var normalizedTranscript = (transcript ?? string.Empty).Trim();
        var tokenCount = normalizedTranscript
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Length;

        var transcriptSignal = Math.Min(tokenCount, 8) / 8.0;
        var providerBonus = provider.Contains("offline", StringComparison.OrdinalIgnoreCase) ? 0.08 : 0.03;
        var historySignal = Math.Min(priorEntries.Count, 10) / 10.0;
        var scoreSpread = Math.Abs(scores.PhonemeScore - scores.FluencyScore);
        var spreadPenalty = Math.Min(scoreSpread, 0.45);

        var rawScore =
            (0.45 * scores.OverallScore) +
            (0.2 * scores.ConsistencyScore) +
            (0.2 * transcriptSignal) +
            (0.1 * historySignal) +
            providerBonus -
            (0.15 * spreadPenalty);

        return Clamp(rawScore);
    }

    private static string ComputeConfidenceBand(double confidenceScore)
    {
        var score = Clamp(confidenceScore);
        if (score >= 0.8)
        {
            return "High";
        }

        if (score >= 0.6)
        {
            return "Moderate";
        }

        return "Low";
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
