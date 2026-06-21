using SpeechBuddyAI.Services;
using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Pages;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly TrendAnalysisService _trendAnalysisService;
    private readonly ConfidenceSettingsService _confidenceSettingsService;

    private IReadOnlyList<Models.ProgressEntry> _allEntries = Array.Empty<Models.ProgressEntry>();

    public ProgressPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _trendAnalysisService = ResolveService<TrendAnalysisService>();
        _confidenceSettingsService = ResolveService<ConfidenceSettingsService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TrajectoryLabel.Text = "Loading progress...";

        try
        {
            _allEntries = await _progressTrackingService.GetEntriesAsync();
            ApplyFilter((FilterEntry.Text ?? string.Empty).Trim());
        }
        catch (Exception ex)
        {
            ProgressCollection.ItemsSource = Array.Empty<object>();
            TrendCollection.ItemsSource = Array.Empty<object>();
            ModerateThresholdLabel.Text = "Moderate threshold: -";
            HighThresholdLabel.Text = "High threshold: -";
            TrajectoryLabel.Text = ex.Message;
        }
    }

    private void OnFilterTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyFilter((e.NewTextValue ?? string.Empty).Trim());
    }

    private void OnFilterClearClicked(object? sender, EventArgs e)
    {
        FilterEntry.Text = string.Empty;
        ApplyFilter(string.Empty);
    }

    private void ApplyFilter(string filterText)
    {
        var filtered = string.IsNullOrWhiteSpace(filterText)
            ? _allEntries
            : _allEntries
                .Where(e => e.TargetSound.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .ToArray();

        ProgressCollection.ItemsSource = filtered;

        var thresholds = _confidenceSettingsService.GetThresholds();
        ModerateThresholdLabel.Text = $"Moderate threshold: {thresholds.ModerateThreshold:P0}";
        HighThresholdLabel.Text = $"High threshold: {thresholds.HighThreshold:P0}";

        var trendPoints = _trendAnalysisService.BuildTrendPoints(filtered, 12);
        TrendCollection.ItemsSource = ApplyThresholdGuides(trendPoints, thresholds);
        TrajectoryLabel.Text = _trendAnalysisService.InterpretTrajectory(filtered);
    }

    private static IReadOnlyList<Models.TrendPoint> ApplyThresholdGuides(
        IReadOnlyList<Models.TrendPoint> trendPoints,
        ConfidenceThresholds thresholds)
    {
        var moderateOffset = 40 + (220 * thresholds.ModerateThreshold);
        var highOffset = 40 + (220 * thresholds.HighThreshold);

        return trendPoints
            .Select(p => new Models.TrendPoint
            {
                AttemptIndex = p.AttemptIndex,
                Score = p.Score,
                BarWidth = p.BarWidth,
                ConfidenceScore = p.ConfidenceScore,
                ConfidenceBarWidth = p.ConfidenceBarWidth,
                ModerateGuideOffset = moderateOffset,
                HighGuideOffset = highOffset
            })
            .ToArray();
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
