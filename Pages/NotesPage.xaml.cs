using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Pages;

public partial class NotesPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly ReportService _reportService;

    public NotesPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _reportService = ResolveService<ReportService>();
    }

    private async void OnGenerateSummariesClicked(object? sender, EventArgs e)
    {
        SoapSummaryLabel.Text = "Generating clinician summary...";
        ParentSummaryLabel.Text = "Generating parent summary...";

        try
        {
            var rawNote = (RawNoteEditor.Text ?? string.Empty).Trim();
            var recentEntries = await _progressTrackingService.GetRecentEntriesAsync(10);
            var report = await _reportService.GenerateReportsAsync(rawNote, recentEntries);

            SoapSummaryLabel.Text = report.SoapSummary;
            ParentSummaryLabel.Text = report.ParentSummary;
        }
        catch (Exception ex)
        {
            SoapSummaryLabel.Text = "Unable to generate summaries.";
            ParentSummaryLabel.Text = ex.Message;
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
