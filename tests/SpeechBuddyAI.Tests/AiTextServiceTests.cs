using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class AiTextServiceTests
{
    [Fact]
    public async Task GenerateHomeAssignmentAsync_NoHistory_ReturnsDefaultPlan()
    {
        var service = new AiTextService(new PhonemeWordBankService());

        var assignment = await service.GenerateHomeAssignmentAsync(Array.Empty<ProgressEntry>());

        Assert.Equal("Home Practice Plan", assignment.Title);
        Assert.Empty(assignment.FocusTargets);
        Assert.NotEmpty(assignment.SuggestedWords);
    }

    [Fact]
    public async Task GenerateHomeAssignmentAsync_PrioritizesDecliningAndUnstableTargets()
    {
        var now = DateTime.UtcNow;
        var history = new[]
        {
            // /r/ declines and becomes unstable.
            Entry("r", 0.88, now.AddDays(-7), "phoneme_mismatch"),
            Entry("r", 0.84, now.AddDays(-6), "phoneme_mismatch"),
            Entry("r", 0.61, now.AddDays(-3), "phoneme_mismatch"),
            Entry("r", 0.47, now.AddDays(-2), "phoneme_mismatch"),
            Entry("r", 0.56, now.AddDays(-1), "phoneme_mismatch"),

            // /l/ is weaker but stable and not declining.
            Entry("l", 0.58, now.AddDays(-7), "fluency_instability"),
            Entry("l", 0.57, now.AddDays(-5), "fluency_instability"),
            Entry("l", 0.56, now.AddDays(-3), "fluency_instability"),
            Entry("l", 0.56, now.AddDays(-1), "fluency_instability"),

            // /s/ is improving.
            Entry("s", 0.40, now.AddDays(-8), "inconsistent_attempts"),
            Entry("s", 0.50, now.AddDays(-4), "inconsistent_attempts"),
            Entry("s", 0.64, now.AddDays(-1), "none")
        };

        var service = new AiTextService(new PhonemeWordBankService());
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.Equal("r", assignment.FocusTargets[0]);
        Assert.Contains("priority", assignment.Rationale, StringComparison.OrdinalIgnoreCase);
    }

    private static ProgressEntry Entry(string target, double overall, DateTime timestamp, string pattern)
    {
        return new ProgressEntry
        {
            TargetSound = target,
            OverallScore = overall,
            Timestamp = timestamp,
            ErrorPattern = pattern,
            Transcript = "sample",
            ConfidenceScore = 0.6,
            ConfidenceBand = "Moderate"
        };
    }
}
