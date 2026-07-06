using SpeechBuddyAI.Services;
using SpeechBuddyAI.Views;

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
            PracticeStatusBanner.Message = "Enter both target sound and transcript before scoring.";
            PracticeStatusBanner.Tone = StatusBannerTone.Warning;
            return;
        }

        PracticeStatusBanner.Message = "Scoring attempt...";
        PracticeStatusBanner.Tone = StatusBannerTone.Info;

        try
        {
            var result = await _aiSpeechService.EvaluateAndPersistAttemptAsync(target, transcript);

            PhonemeScoreLabel.Text = $"Phoneme: {result.Scores.PhonemeScore:P0}";
            FluencyScoreLabel.Text = $"Fluency: {result.Scores.FluencyScore:P0}";
            ConsistencyScoreLabel.Text = $"Consistency: {result.Scores.ConsistencyScore:P0}";
            OverallScoreLabel.Text = $"Overall: {result.Scores.OverallScore:P0}";
            ProviderLabel.Text = $"Provider: {result.Provider}";
            ConfidenceLabel.Text = $"Confidence: {result.ConfidenceBand} ({result.ConfidenceScore:P0})";

            PracticeStatusBanner.Message =
                $"Saved trial {result.Entry.TrialCount} for '{result.Entry.TargetSound}' (pattern: {result.Entry.ErrorPattern}).";
            PracticeStatusBanner.Tone = result.HistoricalDriftDetected ? StatusBannerTone.Warning : StatusBannerTone.Success;

            if (result.HistoricalDriftDetected)
            {
                PracticeStatusBanner.Message += $" {result.HistoricalDriftSummary}";
            }
        }
        catch (Exception ex)
        {
            PracticeStatusBanner.Message = ex.Message;
            PracticeStatusBanner.Tone = StatusBannerTone.Warning;
        }
    }

    private async void OnGeneratePracticeListClicked(object? sender, EventArgs e)
    {
        var target = (TargetSoundEntry.Text ?? string.Empty).Trim();
        var selectedPosition = PositionPicker.SelectedItem as string;
        var position = selectedPosition is "Any" or null ? null : selectedPosition?.ToLowerInvariant();

        var key = position is null ? target : $"{target}:{position}";
        PracticeWordsLabel.Text = "Generating list...";

        try
        {
            var words = await _aiTextService.GeneratePracticeWordsAsync(key);
            PracticeWordsLabel.Text = string.Join(", ", words);
            PracticeStatusBanner.Message = "Practice words generated.";
            PracticeStatusBanner.Tone = StatusBannerTone.Success;
        }
        catch (Exception ex)
        {
            PracticeWordsLabel.Text = "Could not generate words.";
            PracticeStatusBanner.Message = ex.Message;
            PracticeStatusBanner.Tone = StatusBannerTone.Warning;
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
