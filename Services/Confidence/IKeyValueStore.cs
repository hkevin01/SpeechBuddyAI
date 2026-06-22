namespace SpeechBuddyAI.Services.Confidence;

public interface IKeyValueStore
{
    double Get(string key, double defaultValue);
    void Set(string key, double value);

    string Get(string key, string defaultValue)
    {
        return defaultValue;
    }

    void Set(string key, string value)
    {
    }
}
