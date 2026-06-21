namespace SpeechBuddyAI.Services.Confidence;

public interface IConfidenceThresholdProvider
{
    ConfidenceThresholds GetThresholds();
}
