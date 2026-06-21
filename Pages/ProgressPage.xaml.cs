using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Pages;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;

    public ProgressPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ProgressCollection.ItemsSource = await _progressTrackingService.GetEntriesAsync();
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
