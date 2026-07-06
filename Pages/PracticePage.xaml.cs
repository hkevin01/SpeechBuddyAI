using SpeechBuddyAI.Services;
using SpeechBuddyAI.Pages.ViewModels;
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
            ApplyBanner(StatusBannerState.Warning("Enter both target sound and transcript before scoring."));
            return;
        }

        ApplyBanner(StatusBannerState.Info("Scoring attempt..."));

        try
        {
            var result = await _aiSpeechService.EvaluateAndPersistAttemptAsync(target, transcript);

            PhonemeScoreLabel.Text = $"Phoneme: {result.Scores.PhonemeScore:P0}";
            FluencyScoreLabel.Text = $"Fluency: {result.Scores.FluencyScore:P0}";
            ConsistencyScoreLabel.Text = $"Consistency: {result.Scores.ConsistencyScore:P0}";
            OverallScoreLabel.Text = $"Overall: {result.Scores.OverallScore:P0}";
            ProviderLabel.Text = $"Provider: {result.Provider}";
            ConfidenceLabel.Text = $"Confidence: {result.ConfidenceBand} ({result.ConfidenceScore:P0})";

            var banner = StatusBannerState.Success(
                $"Saved trial {result.Entry.TrialCount} for '{result.Entry.TargetSound}' (pattern: {result.Entry.ErrorPattern}).");
            if (result.HistoricalDriftDetected)
            {
                banner = StatusBannerState.Warning(
                    $"Saved trial {result.Entry.TrialCount} for '{result.Entry.TargetSound}' (pattern: {result.Entry.ErrorPattern}). {result.HistoricalDriftSummary}");
            }

            ApplyBanner(banner);
        }
        catch (Exception ex)
        {
            ApplyBanner(StatusBannerState.Warning(ex.Message));
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
            ApplyBanner(StatusBannerState.Success("Practice words generated."));
        }
        catch (Exception ex)
        {
            PracticeWordsLabel.Text = "Could not generate words.";
            ApplyBanner(StatusBannerState.Warning(ex.Message));
        }
    }

    private void ApplyBanner(StatusBannerState state)
    {
        PracticeStatusBanner.Message = state.Message;
        PracticeStatusBanner.Tone = state.Tone;
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
