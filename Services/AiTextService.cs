namespace SpeechBuddyAI.Services;

public class AiTextService
{
    public Task<string[]> GeneratePracticeWordsAsync(string target)
    {
        var words = new[] { "rabbit", "rain", "ring", "rocket" };
        return Task.FromResult(words);
    }
}
