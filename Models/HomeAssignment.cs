namespace SpeechBuddyAI.Models;

public sealed class HomeAssignment
{
    public string Title { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public string ScoringFormulaVersion { get; init; } = string.Empty;
    public IReadOnlyList<string> FocusTargets { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> SuggestedWords { get; init; } = Array.Empty<string>();
    public IReadOnlyList<AssignmentFocusTargetReason> FocusTargetReasons { get; init; } = Array.Empty<AssignmentFocusTargetReason>();
    public bool ReviewRequired { get; init; }
    public double UncertaintyBudgetScore { get; init; }
    public double UncertaintyBudgetCap { get; init; }
    public string UncertaintyBudgetSummary { get; init; } = string.Empty;
}
