using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Pages;

public partial class NotesPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly ReportService _reportService;
    private readonly NoteStorageService _noteStorageService;
    private readonly ReportExportSettingsService _reportExportSettingsService;

    private SessionNote? _pendingNote;
    private SessionNote? _selectedHistoryNote;

    public NotesPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _reportService = ResolveService<ReportService>();
        _noteStorageService = ResolveService<NoteStorageService>();
        _reportExportSettingsService = ResolveService<ReportExportSettingsService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshHistoryAsync();
    }

    private async void OnGenerateSummariesClicked(object? sender, EventArgs e)
    {
        SoapSummaryLabel.Text = "Generating clinician summary...";
        ParentSummaryLabel.Text = "Generating parent summary...";
        NoteStatusLabel.Text = string.Empty;
        _pendingNote = null;

        try
        {
            var rawNote = (RawNoteEditor.Text ?? string.Empty).Trim();
            var recentEntries = await _progressTrackingService.GetRecentEntriesAsync(10);
            var report = await _reportService.GenerateReportsAsync(rawNote, recentEntries);

            SoapSummaryLabel.Text = report.SoapSummary;
            ParentSummaryLabel.Text = report.ParentSummary;
            _pendingNote = report;
            _selectedHistoryNote = null;
            NotesHistoryCollection.SelectedItem = null;
            NoteStatusLabel.Text = "Summaries ready. Press Save Note to persist.";
        }
        catch (Exception ex)
        {
            SoapSummaryLabel.Text = "Unable to generate summaries.";
            ParentSummaryLabel.Text = ex.Message;
        }
    }

    private async void OnSaveNoteClicked(object? sender, EventArgs e)
    {
        if (_pendingNote is null)
        {
            NoteStatusLabel.Text = "Generate summaries first before saving.";
            return;
        }

        try
        {
            await _noteStorageService.SaveNoteAsync(_pendingNote);
            _pendingNote = null;
            NoteStatusLabel.Text = "Note saved.";
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            NoteStatusLabel.Text = ex.Message;
        }
    }

    private async void OnExportLatestClicked(object? sender, EventArgs e)
    {
        NoteStatusLabel.Text = "Exporting latest report...";

        try
        {
            var note = await ResolveLatestExportCandidateAsync();
            if (note is null)
            {
                NoteStatusLabel.Text = "No report available to export. Generate and save a note first.";
                return;
            }

            var metadataEntries = await _progressTrackingService.GetRecentEntriesAsync(30);
            var format = GetSelectedExportFormat();
            var filePath = await _reportService.ExportReportAsync(note, metadataEntries, format);
            NoteStatusLabel.Text = $"Report exported: {Path.GetFileName(filePath)}";
        }
        catch (Exception ex)
        {
            NoteStatusLabel.Text = ex.Message;
        }
    }

    private async void OnShareLatestClicked(object? sender, EventArgs e)
    {
        NoteStatusLabel.Text = "Preparing report for sharing...";

        try
        {
            var note = await ResolveLatestExportCandidateAsync();
            if (note is null)
            {
                NoteStatusLabel.Text = "No report available to share. Generate and save a note first.";
                return;
            }

            var metadataEntries = await _progressTrackingService.GetRecentEntriesAsync(30);
            var format = GetSelectedExportFormat();
            var behavior = _reportExportSettingsService.GetDefaultShareBehavior();

            if (behavior == ReportShareBehavior.ExportOnly)
            {
                var filePath = await _reportService.ExportReportAsync(note, metadataEntries, format);
                NoteStatusLabel.Text = $"Report exported only (per settings): {Path.GetFileName(filePath)}";
                return;
            }

            await _reportService.ShareReportAsync(note, metadataEntries, format);
            NoteStatusLabel.Text = "Share flow opened.";
        }
        catch (Exception ex)
        {
            NoteStatusLabel.Text = ex.Message;
        }
    }

    private void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection?.FirstOrDefault() as SessionNote;
        if (selected is null)
        {
            return;
        }

        _selectedHistoryNote = selected;
        _pendingNote = null;

        RawNoteEditor.Text = selected.RawNote;
        SoapSummaryLabel.Text = string.IsNullOrWhiteSpace(selected.SoapSummary)
            ? "No summary generated yet."
            : selected.SoapSummary;
        ParentSummaryLabel.Text = string.IsNullOrWhiteSpace(selected.ParentSummary)
            ? "No summary generated yet."
            : selected.ParentSummary;

        NoteStatusLabel.Text = $"Loaded note from {selected.SessionDate:yyyy-MM-dd HH:mm}.";
    }

    private async Task RefreshHistoryAsync()
    {
        try
        {
            var notes = await _noteStorageService.GetAllNotesAsync();
            NotesHistoryCollection.ItemsSource = notes;
        }
        catch
        {
            // history refresh failure is non-critical; keep current list
        }
    }

    private async Task<SessionNote?> ResolveLatestExportCandidateAsync()
    {
        if (_pendingNote is not null)
        {
            return _pendingNote;
        }

        if (_selectedHistoryNote is not null)
        {
            return _selectedHistoryNote;
        }

        var recent = await _noteStorageService.GetRecentNotesAsync(1);
        return recent.FirstOrDefault();
    }

    private ReportExportFormat GetSelectedExportFormat()
    {
        return ExportFormatPicker.SelectedIndex switch
        {
            1 => ReportExportFormat.Markdown,
            2 => ReportExportFormat.CsvSummary,
            _ => ReportExportFormat.PlainText
        };
    }

    private static T ResolveService<T>() where T : notnull
    {
        var provider = Application.Current?.Handler?.MauiContext?.Services;
        var service = provider?.GetService(typeof(T));

        if (service is T typedService)
            return typedService;

        throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }
}
