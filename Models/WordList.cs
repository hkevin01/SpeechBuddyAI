namespace SpeechBuddyAI.Models;

public class WordList
{
    public string TargetSound { get; set; } = string.Empty;
    public List<string> Words { get; set; } = new();
    public List<string> Phrases { get; set; } = new();
    public List<string> Sentences { get; set; } = new();
    public List<string> MinimalPairs { get; set; } = new();
}
