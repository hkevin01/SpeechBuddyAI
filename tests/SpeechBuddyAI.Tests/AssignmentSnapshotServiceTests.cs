using System.Text.Json;
using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class AssignmentSnapshotServiceTests
{
    [Fact]
    public void ParseReasons_InvalidJson_ReturnsEmpty()
    {
        var result = AssignmentSnapshotService.ParseReasons("not-json");

        Assert.Empty(result);
    }

    [Fact]
    public void BuildSelectionDetails_IncludesPriorityAndPosition()
    {
        var reasons = new[]
        {
            new AssignmentFocusTargetReason
            {
                TargetSound = "r",
                PriorityScore = 0.72,
                SeverityScore = 0.62,
                InstabilityScore = 0.31,
                DeclineScore = 0.19,
                FrequencyScore = 0.44,
                ConfidenceFactor = 0.81,
                ConfidenceVariance = 0.019,
                AssignmentChangeSuppressed = true,
                PositionSequence = "final -> medial -> initial",
                PositionDeltaSummary = "initial +0.01 | medial -0.04 | final -0.08"
            }
        };

        var details = AssignmentSnapshotService.BuildSelectionDetails(reasons);

        Assert.Contains("priority 0.72", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("position order final -> medial -> initial", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("[suppressed]", details, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseReasons_ValidJson_ReturnsReasons()
    {
        var reasons = new[]
        {
            new AssignmentFocusTargetReason
            {
                TargetSound = "s",
                PriorityScore = 0.64,
                SeverityScore = 0.58,
                InstabilityScore = 0.26,
                DeclineScore = 0.14,
                FrequencyScore = 0.39,
                ConfidenceFactor = 0.74,
                ConfidenceVariance = 0.011,
                PositionSequence = "initial -> final -> medial",
                PositionDeltaSummary = "initial -0.06 | medial +0.01 | final -0.03"
            }
        };

        var json = JsonSerializer.Serialize(reasons);
        var parsed = AssignmentSnapshotService.ParseReasons(json);

        Assert.Single(parsed);
        Assert.Equal("s", parsed[0].TargetSound);
    }

    [Fact]
    public void BuildRationaleDriftSummary_ReportsOverlapAndFocusChangeCount()
    {
        var summary = AssignmentSnapshotService.BuildRationaleDriftSummary(
            "Focus on r blends with slow repetitions.",
            "Focus on r and s carryover with phrase-level repetitions.",
            new[] { "r" },
            new[] { "r", "s" },
            changeSuppressed: false);

        Assert.Contains("Rationale overlap", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("focus target changes", summary, StringComparison.OrdinalIgnoreCase);
    }
}
