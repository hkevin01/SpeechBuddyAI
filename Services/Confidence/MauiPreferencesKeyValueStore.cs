using Microsoft.Maui.Storage;

namespace SpeechBuddyAI.Services.Confidence;

public sealed class MauiPreferencesKeyValueStore : IKeyValueStore
{
    public double Get(string key, double defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    public void Set(string key, double value)
    {
        Preferences.Default.Set(key, value);
    }

    public string Get(string key, string defaultValue)
    {
        return Preferences.Default.Get(key, defaultValue);
    }

    public void Set(string key, string value)
    {
        Preferences.Default.Set(key, value ?? string.Empty);
    }
}
