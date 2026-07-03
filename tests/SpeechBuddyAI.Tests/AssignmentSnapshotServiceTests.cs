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
                CalibrationQualityScore = 0.57,
                CalibrationConfidenceAdjustment = 0.87,
                EvidenceStrength = 0.77,
                ConfidenceVariance = 0.019,
                OverallScoreMean = 0.58,
                OverallScoreCiLower = 0.52,
                OverallScoreCiUpper = 0.64,
                InstabilityWindowSize = 6,
                DeclineWindowSize = 8,
                AssignmentChangeSuppressed = true,
                PositionSequence = "final -> medial -> initial",
                PositionDeltaSummary = "initial +0.01 | medial -0.04 | final -0.08"
            }
        };

        var details = AssignmentSnapshotService.BuildSelectionDetails(reasons);

        Assert.Contains("priority 0.72", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("CI95", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("evidence", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("calibration quality", details, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("adjustment", details, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void ParseCalibrationMetrics_InvalidJson_ReturnsFallbackSummary()
    {
        var calibration = AssignmentSnapshotService.ParseCalibrationMetrics("not-json");

        Assert.Contains("Calibration", calibration.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildModelAuditSummary_IncludesCalibrationAndComponentTraceDetails()
    {
        var tracesJson = JsonSerializer.Serialize(new[]
        {
            new AssignmentComponentTracePoint
            {
                TargetSound = "r",
                PriorityScore = 0.72,
                SeverityScore = 0.61,
                InstabilityScore = 0.33,
                DeclineScore = 0.22,
                OverallScoreCiLower = 0.52,
                OverallScoreCiUpper = 0.64
            }
        });
        var calibrationJson = JsonSerializer.Serialize(new AssignmentCalibrationMetrics
        {
            MatchedTargetCountNext1 = 2,
            MeanAbsoluteErrorNext1 = 0.12,
            MeanSquaredErrorNext1 = 0.03,
            RankAgreementNext1 = 1.0,
            TopTargetHitRateNext1 = 1.0,
            MatchedTargetCountNext3 = 4,
            MeanAbsoluteErrorNext3 = 0.18,
            MeanSquaredErrorNext3 = 0.05,
            RankAgreementNext3 = 0.75,
            TopTargetHitRateNext3 = 0.50,
            Summary = "matched targets 2, MAE 0.120, MSE 0.030, rank agreement 100%, top-target hit 100%."
        });
        var suggestionJson = JsonSerializer.Serialize(new AssignmentWeightSuggestion
        {
            SuggestedSeverityWeight = 0.49,
            SuggestedInstabilityWeight = 0.22,
            SuggestedDeclineWeight = 0.20,
            SuggestedFrequencyWeight = 0.09,
            SuggestedConfidencePenaltyStrength = 0.70,
            Summary = "advisory only - suggested weights ...",
            AdvisoryOnly = true
        });
        var snapshots = new[]
        {
            new AssignmentSnapshot
            {
                SnapshotDate = new DateTimeOffset(2026, 6, 30, 10, 0, 0, TimeSpan.Zero),
                ComponentTracesJson = tracesJson,
                CalibrationMetricsJson = calibrationJson,
                AdvisoryWeightSuggestionJson = suggestionJson,
                ScoringFormulaVersion = "assign-v3.0-adaptive-calibrated",
                ReviewRequired = true,
                UncertaintyBudgetScore = 0.52,
                UncertaintyBudgetCap = 0.40
            }
        };

        var summary = AssignmentSnapshotService.BuildModelAuditSummary(snapshots);

        Assert.Contains("calibration", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("next-1", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("next-3", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("formula versions", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("advisory", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("component trace", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ci95", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uncertainty budget", summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("review-required", summary, StringComparison.OrdinalIgnoreCase);
    }
}
