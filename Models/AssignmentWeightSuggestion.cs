namespace SpeechBuddyAI.Models;

public sealed class AssignmentWeightSuggestion
{
    public double SuggestedSeverityWeight { get; init; }
    public double SuggestedInstabilityWeight { get; init; }
    public double SuggestedDeclineWeight { get; init; }
    public double SuggestedFrequencyWeight { get; init; }
    public double SuggestedConfidencePenaltyStrength { get; init; }
    public string Summary { get; init; } = string.Empty;
    public bool AdvisoryOnly { get; init; } = true;
}
