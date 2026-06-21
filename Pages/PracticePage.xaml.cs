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
        var target = (TargetSoundEntry.Text ?? string.Empty).Trim();
        var transcript = (TranscriptEditor.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(target) || string.IsNullOrWhiteSpace(transcript))
        {
            StatusLabel.Text = "Enter both target sound and transcript before scoring.";
            return;
        }

        StatusLabel.Text = "Scoring attempt...";

        try
        {
            var result = await _aiSpeechService.EvaluateAndPersistAttemptAsync(target, transcript);

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
        var target = (TargetSoundEntry.Text ?? string.Empty).Trim();
        PracticeWordsLabel.Text = "Generating list...";

        try
        {
            var words = await _aiTextService.GeneratePracticeWordsAsync(target);
            PracticeWordsLabel.Text = string.Join(", ", words);
        }
        catch (Exception ex)
        {
            PracticeWordsLabel.Text = "Could not generate words.";
            StatusLabel.Text = ex.Message;
        }
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
