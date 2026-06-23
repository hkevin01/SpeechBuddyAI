namespace SpeechBuddyAI.Models;

public sealed class AssignmentPrioritySettings
{
    public const double DefaultSeverityWeight = 0.45;
    public const double DefaultInstabilityWeight = 0.20;
    public const double DefaultDeclineWeight = 0.20;
    public const double DefaultFrequencyWeight = 0.15;
    public const double DefaultConfidencePenaltyStrength = 0.60;

    public double SeverityWeight { get; init; } = DefaultSeverityWeight;
    public double InstabilityWeight { get; init; } = DefaultInstabilityWeight;
    public double DeclineWeight { get; init; } = DefaultDeclineWeight;
    public double FrequencyWeight { get; init; } = DefaultFrequencyWeight;
    public double ConfidencePenaltyStrength { get; init; } = DefaultConfidencePenaltyStrength;

    public AssignmentPrioritySettings Normalize()
    {
        var severity = Clamp(SeverityWeight);
        var instability = Clamp(InstabilityWeight);
        var decline = Clamp(DeclineWeight);
        var frequency = Clamp(FrequencyWeight);
        var penalty = Clamp(ConfidencePenaltyStrength);
        var sum = severity + instability + decline + frequency;

        if (sum <= 0)
        {
            return new AssignmentPrioritySettings
            {
                SeverityWeight = DefaultSeverityWeight,
                InstabilityWeight = DefaultInstabilityWeight,
                DeclineWeight = DefaultDeclineWeight,
                FrequencyWeight = DefaultFrequencyWeight,
                ConfidencePenaltyStrength = penalty
            };
        }

        return new AssignmentPrioritySettings
        {
            SeverityWeight = severity / sum,
            InstabilityWeight = instability / sum,
            DeclineWeight = decline / sum,
            FrequencyWeight = frequency / sum,
            ConfidencePenaltyStrength = penalty
        };
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }
}
