namespace SpeechBuddyAI.Models;

public enum AssignmentSuppressionBehavior
{
    HardFreeze = 0,
    PartialUpdate = 1,
    WarningOnly = 2
}

public static class AssignmentSuppressionBehaviorExtensions
{
    public static string ToDisplayLabel(this AssignmentSuppressionBehavior behavior)
    {
        return behavior switch
        {
            AssignmentSuppressionBehavior.PartialUpdate => "Partial update",
            AssignmentSuppressionBehavior.WarningOnly => "Warning only",
            _ => "Hard freeze"
        };
    }
}
