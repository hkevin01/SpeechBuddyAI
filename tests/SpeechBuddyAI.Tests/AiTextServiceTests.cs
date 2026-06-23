using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class AiTextServiceTests
{
    [Fact]
    public async Task GenerateHomeAssignmentAsync_NoHistory_ReturnsDefaultPlan()
    {
        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);

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
            Entry("r:initial", 0.88, now.AddDays(-7), "phoneme_mismatch", 0.9),
            Entry("r:medial", 0.84, now.AddDays(-6), "phoneme_mismatch", 0.8),
            Entry("r:initial", 0.61, now.AddDays(-3), "phoneme_mismatch", 0.6),
            Entry("r:final", 0.47, now.AddDays(-2), "phoneme_mismatch", 0.55),
            Entry("r:final", 0.56, now.AddDays(-1), "phoneme_mismatch", 0.52),

            // /l/ is weaker but stable and not declining.
            Entry("l:initial", 0.58, now.AddDays(-7), "fluency_instability", 0.75),
            Entry("l:medial", 0.57, now.AddDays(-5), "fluency_instability", 0.73),
            Entry("l:final", 0.56, now.AddDays(-3), "fluency_instability", 0.72),
            Entry("l:initial", 0.56, now.AddDays(-1), "fluency_instability", 0.71),

            // /s/ is improving.
            Entry("s:initial", 0.40, now.AddDays(-8), "inconsistent_attempts", 0.45),
            Entry("s:medial", 0.50, now.AddDays(-4), "inconsistent_attempts", 0.42),
            Entry("s:final", 0.64, now.AddDays(-1), "none", 0.40)
        };

        var snapshotService = new AssignmentSnapshotService();
        var service = new AiTextService(new PhonemeWordBankService(), new ConfidenceSettingsService(new InMemoryStore()), snapshotService);
        var assignment = await service.GenerateHomeAssignmentAsync(history);

        Assert.NotEmpty(assignment.FocusTargets);
        Assert.Equal("r", assignment.FocusTargets[0]);
        Assert.Contains("priority", assignment.Rationale, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(assignment.FocusTargetReasons);
        Assert.Contains("->", assignment.FocusTargetReasons[0].PositionSequence);
        Assert.Contains("initial", assignment.FocusTargetReasons[0].PositionDeltaSummary, StringComparison.OrdinalIgnoreCase);
    }

    private static ProgressEntry Entry(string target, double overall, DateTime timestamp, string pattern, double confidence)
    {
        return new ProgressEntry
        {
            TargetSound = target,
            BaseTargetSound = target.Split(':')[0],
            PositionTag = target.Contains(':') ? target.Split(':')[1] : string.Empty,
            OverallScore = overall,
            Timestamp = timestamp,
            ErrorPattern = pattern,
            Transcript = "sample",
            ConfidenceScore = confidence,
            ConfidenceBand = "Moderate"
        };
    }

    private sealed class InMemoryStore : IKeyValueStore
    {
        private readonly Dictionary<string, double> _values = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _stringValues = new(StringComparer.Ordinal);

        public double Get(string key, double defaultValue)
        {
            return _values.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, double value)
        {
            _values[key] = value;
        }

        public string Get(string key, string defaultValue)
        {
            return _stringValues.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public void Set(string key, string value)
        {
            _stringValues[key] = value;
        }
    }
}
