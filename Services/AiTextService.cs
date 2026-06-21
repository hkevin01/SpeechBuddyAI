using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class AiTextService
{
    public Task<string[]> GeneratePracticeWordsAsync(string target)
    {
        var normalized = (target ?? string.Empty).Trim().ToLowerInvariant();
        var words = normalized.Contains("s")
            ? new[] { "sun", "soup", "sand", "sail" }
            : normalized.Contains("l")
                ? new[] { "leaf", "lamp", "light", "lemon" }
                : new[] { "rabbit", "rain", "ring", "rocket" };

        return Task.FromResult(words);
    }

    public async Task<HomeAssignment> GenerateHomeAssignmentAsync(IReadOnlyList<ProgressEntry> weakEntries)
    {
        var targets = weakEntries
            .OrderBy(e => e.OverallScore)
            .Select(e => e.TargetSound)
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

        var commonPattern = weakEntries
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
}
