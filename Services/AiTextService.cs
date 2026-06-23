using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

// ID: SVC-TEXT-001
// Purpose: Generates practice word lists and home assignment plans.
public class AiTextService
{
    private const int MaxFocusTargets = 3;
    private const int RecentWindow = 5;

    private readonly PhonemeWordBankService _wordBank;

    public AiTextService(PhonemeWordBankService wordBank)
    {
        _wordBank = wordBank ?? throw new ArgumentNullException(nameof(wordBank));
    }

    // ID: SVC-TEXT-002
    // Purpose: Returns position-aware practice words for a given target sound.
    // Inputs: target ("r", "sh", "r:initial", etc.), null position falls back to all positions.
    public Task<string[]> GeneratePracticeWordsAsync(string target)
    {
        var normalizedTarget = (target ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedTarget))
            return Task.FromResult(_wordBank.GetWords("r", "initial"));

        try
        {
            // Support "target:position" shorthand from callers
            var parts = normalizedTarget.Split(':', 2);
            var phoneme = parts[0].Trim();
            var position = parts.Length > 1 ? parts[1].Trim() : null;

            return Task.FromResult(_wordBank.GetWords(phoneme, position));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate practice words.", ex);
        }
    }

    // ID: SVC-TEXT-003
    // Purpose: Builds a formula-driven assignment plan from persisted attempt history.
    // Formula: priority = recency * (0.45*severity + 0.20*instability + 0.20*decline + 0.15*frequency)
    // where severity = 1 - recent mean overall, instability = sqrt(recent variance),
    // decline = max(0, baseline mean - recent mean), and frequency favors repeated challenges.
    public async Task<HomeAssignment> GenerateHomeAssignmentAsync(IReadOnlyList<ProgressEntry> history)
    {
        var sourceEntries = history ?? Array.Empty<ProgressEntry>();

        try
        {
            var candidates = BuildTargetCandidates(sourceEntries)
                .OrderByDescending(c => c.Priority)
                .Take(MaxFocusTargets)
                .ToArray();

            var targets = candidates
                .Select(c => c.Target)
                .ToArray();

            if (targets.Length == 0)
            {
                return new HomeAssignment
                {
                    Title = "Home Practice Plan",
                    Rationale = "No weak patterns found yet. Continue with mixed articulation drills for consistency.",
                    FocusTargets = Array.Empty<string>(),
                    SuggestedWords = new[] { "rabbit", "lamp", "sun" }
                };
            }

            var suggestedWords = new List<string>();
            foreach (var target in targets)
            {
                var words = await GeneratePracticeWordsAsync(target);
                suggestedWords.AddRange(words.Take(2));
            }

            var commonPattern = sourceEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.ErrorPattern))
                .GroupBy(e => e.ErrorPattern)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "mixed_patterns";

            var rationale = BuildRationale(candidates, commonPattern);

            return new HomeAssignment
            {
                Title = "Home Practice Plan",
                Rationale = rationale,
                FocusTargets = targets,
                SuggestedWords = suggestedWords.Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate a home assignment from weak-pattern history.", ex);
        }
    }

    private static IReadOnlyList<TargetAssignmentCandidate> BuildTargetCandidates(IReadOnlyList<ProgressEntry> entries)
    {
        var now = DateTime.UtcNow;

        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
            .GroupBy(e => e.TargetSound.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildTargetCandidate(group.Key, group.ToArray(), now))
            .OrderByDescending(candidate => candidate.Priority)
            .ToArray();
    }

    private static TargetAssignmentCandidate BuildTargetCandidate(string target, IReadOnlyList<ProgressEntry> entries, DateTime now)
    {
        var ordered = entries.OrderBy(e => e.Timestamp).ToArray();
        var recent = ordered.TakeLast(Math.Min(RecentWindow, ordered.Length)).ToArray();
        var baseline = ordered.Take(Math.Min(RecentWindow, ordered.Length)).ToArray();

        var recentMean = recent.Length > 0 ? recent.Average(e => Clamp(e.OverallScore)) : 0.0;
        var baselineMean = baseline.Length > 0 ? baseline.Average(e => Clamp(e.OverallScore)) : recentMean;
        var severity = Clamp(1.0 - recentMean);
        var instability = Math.Sqrt(ComputeVariance(recent.Select(e => Clamp(e.OverallScore)).ToArray()));
        var decline = Clamp(Math.Max(0.0, baselineMean - recentMean));
        var frequency = Clamp(Math.Log(entries.Count + 1, 2) / 4.0);
        var daysSinceLast = Math.Max(0.0, (now - ordered[^1].Timestamp).TotalDays);
        var recency = Math.Exp(-daysSinceLast / 14.0);

        var basePriority = (0.45 * severity) + (0.20 * instability) + (0.20 * decline) + (0.15 * frequency);
        var priority = Clamp(basePriority * recency);

        return new TargetAssignmentCandidate(
            target,
            priority,
            severity,
            instability,
            decline,
            entries.Count,
            ordered[^1].Timestamp);
    }

    private static string BuildRationale(IReadOnlyList<TargetAssignmentCandidate> candidates, string commonPattern)
    {
        if (candidates.Count == 0)
        {
            return "No weak patterns found yet. Continue with mixed articulation drills for consistency.";
        }

        var top = candidates[0];
        var targetList = string.Join(", ", candidates.Select(c => c.Target));
        return $"Focus on {targetList} based on weighted priority from recent severity, instability, trend decline, and repetition frequency. " +
               $"Highest-priority target is {top.Target} (priority {top.Priority:0.00}, severity {top.Severity:0.00}, instability {top.Instability:0.00}). " +
               $"Most frequent challenge pattern was '{commonPattern}', so drills should emphasize slow, repeatable production before speed.";
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

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }

    private sealed record TargetAssignmentCandidate(
        string Target,
        double Priority,
        double Severity,
        double Instability,
        double Decline,
        int Attempts,
        DateTime LastAttemptAt);
}
