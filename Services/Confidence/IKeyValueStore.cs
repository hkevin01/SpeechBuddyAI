namespace SpeechBuddyAI.Services.Confidence;

public interface IKeyValueStore
{
    double Get(string key, double defaultValue);
    void Set(string key, double value);
}
