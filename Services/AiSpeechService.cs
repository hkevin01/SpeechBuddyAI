using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class AiSpeechService
{
    private readonly ProgressTrackingService _progressTrackingService;

    public AiSpeechService(ProgressTrackingService progressTrackingService)
    {
        _progressTrackingService = progressTrackingService;
    }

    public Task<double> ScorePhonemeAsync(string expectedPhoneme, string transcript)
    {
        var scores = ComputeScoreComponents(expectedPhoneme, transcript, 0.5);
        return Task.FromResult(scores.PhonemeScore);
    }

    public async Task<PracticeAttemptResult> EvaluateAndPersistAttemptAsync(string targetSound, string transcript)
    {
        var normalizedTarget = (targetSound ?? string.Empty).Trim();
        var normalizedTranscript = (transcript ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedTarget) || string.IsNullOrWhiteSpace(normalizedTranscript))
        {
            throw new ArgumentException("Target sound and transcript are required for scoring.");
        }

        var priorEntries = await _progressTrackingService.GetEntriesForSoundAsync(normalizedTarget);
        var consistency = ComputeConsistencyScore(priorEntries);
        var scores = ComputeScoreComponents(normalizedTarget, normalizedTranscript, consistency);

        var trialCount = priorEntries.Count + 1;
        var entry = new ProgressEntry
        {
            Timestamp = DateTimeOffset.UtcNow,
            TargetSound = normalizedTarget,
            Transcript = normalizedTranscript,
            AccuracyScore = scores.OverallScore,
            PhonemeScore = scores.PhonemeScore,
            FluencyScore = scores.FluencyScore,
            ConsistencyScore = scores.ConsistencyScore,
            OverallScore = scores.OverallScore,
            TrialCount = trialCount,
            ErrorPattern = InferErrorPattern(scores)
        };

        await _progressTrackingService.AddEntryAsync(entry);

        return new PracticeAttemptResult
        {
            Scores = scores,
            Entry = entry
        };
    }

    private static ScoreComponents ComputeScoreComponents(string expectedPhoneme, string transcript, double consistency)
    {
        var normalizedExpected = expectedPhoneme.Trim().ToLowerInvariant();
        var normalizedTranscript = transcript.Trim().ToLowerInvariant();

        var phoneme = normalizedTranscript.Contains(normalizedExpected, StringComparison.Ordinal)
            ? 0.9
            : 0.35;

        var expectedLength = Math.Max(1, normalizedExpected.Length);
        var transcriptLength = Math.Max(1, normalizedTranscript.Length);
        var ratio = Math.Min(expectedLength, transcriptLength) / (double)Math.Max(expectedLength, transcriptLength);
        var fluency = Clamp(0.45 + 0.5 * ratio);

        var overall = Clamp(0.6 * phoneme + 0.25 * fluency + 0.15 * consistency);

        return new ScoreComponents
        {
            PhonemeScore = phoneme,
            FluencyScore = fluency,
            ConsistencyScore = consistency,
            OverallScore = overall
        };
    }

    private static double ComputeConsistencyScore(IReadOnlyList<ProgressEntry> entries)
    {
        if (entries.Count < 2)
        {
            return 0.5;
        }

        var recent = entries
            .OrderByDescending(e => e.Timestamp)
            .Take(5)
            .Select(e => e.OverallScore)
            .ToArray();

        var mean = recent.Average();
        var variance = recent.Average(s => Math.Pow(s - mean, 2));
        var normalizedVariance = Math.Min(variance / 0.08, 1.0);
        return Clamp(1.0 - normalizedVariance);
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
}
