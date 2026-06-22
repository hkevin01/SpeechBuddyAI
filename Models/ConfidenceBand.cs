namespace SpeechBuddyAI.Models;

public enum ConfidenceBand
{
    Low = 0,
    Moderate = 1,
    High = 2
}

public static class ConfidenceBandExtensions
{
    public static ConfidenceBand Parse(string? value)
    {
        if (string.Equals(value, "High", StringComparison.OrdinalIgnoreCase))
        {
            return ConfidenceBand.High;
        }

        if (string.Equals(value, "Moderate", StringComparison.OrdinalIgnoreCase))
        {
            return ConfidenceBand.Moderate;
        }

        return ConfidenceBand.Low;
    }

    public static string ToDisplayName(this ConfidenceBand band)
    {
        return band switch
        {
            ConfidenceBand.High => "High",
            ConfidenceBand.Moderate => "Moderate",
            _ => "Low"
        };
    }

    public static string ToChipBackgroundColor(this ConfidenceBand band)
    {
        return band switch
        {
            ConfidenceBand.High => "#E6F4EA",
            ConfidenceBand.Moderate => "#FFF4D6",
            _ => "#F1F3F5"
        };
    }

    public static string ToChipBorderColor(this ConfidenceBand band)
    {
        return band switch
        {
            ConfidenceBand.High => "#2F9E44",
            ConfidenceBand.Moderate => "#E0A800",
            _ => "#ADB5BD"
        };
    }
}
