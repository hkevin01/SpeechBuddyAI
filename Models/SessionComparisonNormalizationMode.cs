namespace SpeechBuddyAI.Models;

public enum SessionComparisonNormalizationMode
{
    AttemptWeighted = 0,
    DayWeighted = 1
}

public static class SessionComparisonNormalizationModeExtensions
{
    public static string ToDisplayLabel(this SessionComparisonNormalizationMode mode)
    {
        return mode switch
        {
            SessionComparisonNormalizationMode.DayWeighted => "Day-weighted averages",
            _ => "Attempt-weighted averages"
        };
    }
}
