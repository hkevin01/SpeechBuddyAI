using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Pages.ViewModels;

public enum StatusBannerTone
{
    Info,
    Success,
    Warning,
    ReviewRequired
}

public static class UiThemeTokens
{
    public const string NeutralContainer = "#FAFBFC";
    public const string NeutralOutline = "#D7E0E7";
    public const string SuccessContainer = "#E7F6EA";
    public const string SuccessOutline = "#B5D9BC";
    public const string WarningContainer = "#FFF4E5";
    public const string WarningOutline = "#E9C58B";
    public const string HighConfidenceContainer = "#E8F7EE";
    public const string HighConfidenceOutline = "#7BC496";
    public const string LowConfidenceContainer = "#FFF1F0";
    public const string LowConfidenceOutline = "#E59B92";

    public const string SparklineSeverity = "#2563EB";
    public const string SparklineInstability = "#F59E0B";
    public const string SparklineDecline = "#DC2626";
    public const string SparklineFrequency = "#7C3AED";
    public const string SparklineConfidence = "#059669";
    public const string SparklineCalibrationNext1 = "#0EA5E9";
    public const string SparklineCalibrationNext3 = "#EC4899";
    public const string SparklineTraceDrift = "#334155";
}

public sealed record StatusBannerState(string Message, StatusBannerTone Tone)
{
    public static StatusBannerState Info(string message) => new(message, StatusBannerTone.Info);
    public static StatusBannerState Success(string message) => new(message, StatusBannerTone.Success);
    public static StatusBannerState Warning(string message) => new(message, StatusBannerTone.Warning);
    public static StatusBannerState ReviewRequired(string message) => new(message, StatusBannerTone.ReviewRequired);

    public static StatusBannerState FromAssignment(HomeAssignment assignment)
    {
        if (assignment is null)
        {
            throw new ArgumentNullException(nameof(assignment));
        }

        return assignment.ReviewRequired
            ? ReviewRequired(assignment.UncertaintyBudgetSummary)
            : Success("Assignment generated successfully.");
    }
}
