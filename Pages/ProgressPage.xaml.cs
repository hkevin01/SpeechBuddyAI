using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Pages;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly TrendAnalysisService _trendAnalysisService;

    public ProgressPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _trendAnalysisService = ResolveService<TrendAnalysisService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        TrajectoryLabel.Text = "Loading progress...";

        try
        {
            var entries = await _progressTrackingService.GetEntriesAsync();
            ProgressCollection.ItemsSource = entries;

            var trendPoints = _trendAnalysisService.BuildTrendPoints(entries, 12);
            TrendCollection.ItemsSource = trendPoints;
            TrajectoryLabel.Text = _trendAnalysisService.InterpretTrajectory(entries);
        }
        catch (Exception ex)
        {
            ProgressCollection.ItemsSource = Array.Empty<object>();
            TrendCollection.ItemsSource = Array.Empty<object>();
            TrajectoryLabel.Text = ex.Message;
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
