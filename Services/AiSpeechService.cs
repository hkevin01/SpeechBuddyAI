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
        var result = await TryScoreWithFallbackAsync(expectedPhoneme, transcript, Array.Empty<ProgressEntry>());
        return result.PhonemeScore;
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
        var adapterResult = await TryScoreWithFallbackAsync(normalizedTarget, normalizedTranscript, priorEntries);
        var scores = ComposeScoreComponents(adapterResult.PhonemeScore, adapterResult.FluencyScore, consistency);

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
            ScoringProvider = adapterResult.Provider
        };

        await _progressTrackingService.AddEntryAsync(entry);

        return new PracticeAttemptResult
        {
            Scores = scores,
            Entry = entry
        };
    }

    private async Task<AdapterScoreResult> TryScoreWithFallbackAsync(
        string targetSound,
        string transcript,
        IReadOnlyList<ProgressEntry> priorEntries)
    {
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
