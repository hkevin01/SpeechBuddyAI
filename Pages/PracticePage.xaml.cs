using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Pages;

public partial class PracticePage : ContentPage
{
    private readonly AiSpeechService _aiSpeechService;
    private readonly AiTextService _aiTextService;

    public PracticePage()
    {
        InitializeComponent();
        _aiSpeechService = ResolveService<AiSpeechService>();
        _aiTextService = ResolveService<AiTextService>();
    }

    private async void OnScoreAttemptClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await _aiSpeechService.EvaluateAndPersistAttemptAsync(
                TargetSoundEntry.Text ?? string.Empty,
                TranscriptEditor.Text ?? string.Empty);

            PhonemeScoreLabel.Text = $"Phoneme: {result.Scores.PhonemeScore:P0}";
            FluencyScoreLabel.Text = $"Fluency: {result.Scores.FluencyScore:P0}";
            ConsistencyScoreLabel.Text = $"Consistency: {result.Scores.ConsistencyScore:P0}";
            OverallScoreLabel.Text = $"Overall: {result.Scores.OverallScore:P0}";

            StatusLabel.Text =
                $"Saved trial {result.Entry.TrialCount} for '{result.Entry.TargetSound}' (pattern: {result.Entry.ErrorPattern}).";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }

    private async void OnGeneratePracticeListClicked(object? sender, EventArgs e)
    {
        var target = TargetSoundEntry.Text ?? string.Empty;
        var words = await _aiTextService.GeneratePracticeWordsAsync(target);
        PracticeWordsLabel.Text = string.Join(", ", words);
    }

    private static T ResolveService<T>() where T : notnull
    {
        var provider = Application.Current?.Handler?.MauiContext?.Services;
        var service = provider?.GetService(typeof(T));

        if (service is T typedService)
        {
            return typedService;
        }

        throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}
