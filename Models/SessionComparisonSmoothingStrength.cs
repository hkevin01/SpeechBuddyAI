namespace SpeechBuddyAI.Models;

public enum SessionComparisonSmoothingStrength
{
    Conservative = 0,
    Balanced = 1,
    Responsive = 2
}

public static class SessionComparisonSmoothingStrengthExtensions
{
    public static string ToDisplayLabel(this SessionComparisonSmoothingStrength strength)
    {
        return strength switch
        {
            SessionComparisonSmoothingStrength.Conservative => "Conservative smoothing",
            SessionComparisonSmoothingStrength.Responsive => "Responsive smoothing",
            _ => "Balanced smoothing"
        };
    }
}
