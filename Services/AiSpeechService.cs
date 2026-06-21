namespace SpeechBuddyAI.Services;

public class AiSpeechService
{
    public Task<double> ScorePhonemeAsync(string expectedPhoneme, string transcript)
    {
        if (string.IsNullOrWhiteSpace(expectedPhoneme) || string.IsNullOrWhiteSpace(transcript))
        {
            return Task.FromResult(0d);
        }

        var normalizedExpected = expectedPhoneme.Trim().ToLowerInvariant();
        var normalizedTranscript = transcript.Trim().ToLowerInvariant();
        var score = normalizedTranscript.Contains(normalizedExpected) ? 0.9 : 0.4;
        return Task.FromResult(score);
    }
}
