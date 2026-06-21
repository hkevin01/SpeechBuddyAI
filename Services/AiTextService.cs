using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class AiTextService
{
    public Task<string[]> GeneratePracticeWordsAsync(string target)
    {
        var normalizedTarget = (target ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return Task.FromResult(new[] { "rabbit", "rain", "ring", "rocket" });
        }

        try
        {
            var words = normalizedTarget.Contains("s", StringComparison.Ordinal)
                ? new[] { "sun", "soup", "sand", "sail" }
                : normalizedTarget.Contains("l", StringComparison.Ordinal)
                    ? new[] { "leaf", "lamp", "light", "lemon" }
                    : normalizedTarget.Contains("r", StringComparison.Ordinal)
                        ? new[] { "rabbit", "rain", "ring", "rocket" }
                        : new[] { "cat", "cup", "kite", "coat" };

            return Task.FromResult(words);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate practice words.", ex);
        }
    }

    public async Task<HomeAssignment> GenerateHomeAssignmentAsync(IReadOnlyList<ProgressEntry> weakEntries)
    {
        var sourceEntries = weakEntries ?? Array.Empty<ProgressEntry>();

        try
        {
            var targets = sourceEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
                .OrderBy(e => e.OverallScore)
                .Select(e => e.TargetSound.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
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

            var rationale = $"Focus on {string.Join(", ", targets)} because these targets show lower recent scores. " +
                            $"Most frequent challenge pattern was '{commonPattern}', so drills should emphasize slow, repeatable production before speed.";

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
}
