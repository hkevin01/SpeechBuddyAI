using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SpeechBuddyAI.Pages.ViewModels;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Pages;

public partial class NotesPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly ReportService _reportService;
    private readonly NoteStorageService _noteStorageService;
    private readonly ReportExportSettingsService _reportExportSettingsService;
    private readonly ConfidenceSettingsService _confidenceSettingsService;
    private readonly ComparisonSnapshotCacheService _comparisonSnapshotCacheService;
    private readonly AssignmentSnapshotService _assignmentSnapshotService;
    private readonly NotesPageViewModel _viewModel = new();
    private bool _hasInitializedDateRange;
    private IReadOnlyList<AssignmentSnapshot> _assignmentSnapshots = Array.Empty<AssignmentSnapshot>();

    public NotesPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _reportService = ResolveService<ReportService>();
        _noteStorageService = ResolveService<NoteStorageService>();
        _reportExportSettingsService = ResolveService<ReportExportSettingsService>();
        _confidenceSettingsService = ResolveService<ConfidenceSettingsService>();
        _comparisonSnapshotCacheService = ResolveService<ComparisonSnapshotCacheService>();
        _assignmentSnapshotService = ResolveService<AssignmentSnapshotService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        LoadExportPreferences();
        InitializeDateRangeIfNeeded();
        await RefreshHistoryAsync();
        await RefreshComparisonPreviewAsync();
        await RefreshAssignmentAnalyticsAsync();
    }

    private async void OnGenerateSummariesClicked(object? sender, EventArgs e)
    {
        SoapSummaryLabel.Text = "Generating clinician summary...";
        ParentSummaryLabel.Text = "Generating parent summary...";
        NoteStatusLabel.Text = string.Empty;

        try
        {
            var rawNote = (RawNoteEditor.Text ?? string.Empty).Trim();
            var recentEntries = await _progressTrackingService.GetRecentEntriesAsync(10);
            var report = await _reportService.GenerateReportsAsync(rawNote, recentEntries);

            SoapSummaryLabel.Text = report.SoapSummary;
            ParentSummaryLabel.Text = report.ParentSummary;
            _viewModel.SetGeneratedNote(report);
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
        if (_viewModel.PendingNote is null)
        {
            NoteStatusLabel.Text = "Generate summaries first before saving.";
            return;
        }

        try
        {
            await _noteStorageService.SaveNoteAsync(_viewModel.PendingNote);
            _viewModel.MarkSaved();
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

            var metadataEntries = await GetMetadataEntriesForSelectedRangeAsync();
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

            var metadataEntries = await GetMetadataEntriesForSelectedRangeAsync();
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

    private void OnExportFormatChanged(object? sender, EventArgs e)
    {
        try
        {
            _reportExportSettingsService.SavePreferredExportFormat(GetSelectedExportFormat());
        }
        catch
        {
            // Preference persistence failure should not block page usage.
        }
    }

    private void OnHistorySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection?.FirstOrDefault() as SessionNote;
        if (selected is null)
        {
            return;
        }

        _viewModel.SetSelectedHistoryNote(selected);

        RawNoteEditor.Text = selected.RawNote;
        SoapSummaryLabel.Text = string.IsNullOrWhiteSpace(selected.SoapSummary)
            ? "No summary generated yet."
            : selected.SoapSummary;
        ParentSummaryLabel.Text = string.IsNullOrWhiteSpace(selected.ParentSummary)
            ? "No summary generated yet."
            : selected.ParentSummary;

        NoteStatusLabel.Text = $"Loaded note from {selected.SessionDate:yyyy-MM-dd HH:mm}.";
    }

    private async void OnExportPreviewWindowChanged(object? sender, DateChangedEventArgs e)
    {
        await RefreshComparisonPreviewAsync();
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

    private async Task RefreshAssignmentAnalyticsAsync()
    {
        try
        {
            _assignmentSnapshots = await _assignmentSnapshotService.GetRecentSnapshotsAsync(20);
            var state = _viewModel.BuildAssignmentAnalyticsState(_assignmentSnapshots);
            ApplyAssignmentAnalyticsState(state);
            AssignmentSnapshotCollection.ItemsSource = state.SnapshotRows;
        }
        catch (Exception ex)
        {
            AssignmentAnalyticsSummaryLabel.Text = ex.Message;
            AssignmentSnapshotCollection.ItemsSource = Array.Empty<object>();
            SeverityTrendChartLabel.Text = "Severity trend: -";
            InstabilityTrendChartLabel.Text = "Instability trend: -";
            DeclineTrendChartLabel.Text = "Decline trend: -";
            FrequencyTrendChartLabel.Text = "Frequency trend: -";
            ConfidenceTrendChartLabel.Text = "Confidence trend: -";
        }
    }

    private async Task<SessionNote?> ResolveLatestExportCandidateAsync()
    {
        var recent = await _noteStorageService.GetRecentNotesAsync(1);
        return _viewModel.ResolveExportCandidate(recent.FirstOrDefault());
    }

    private ReportExportFormat GetSelectedExportFormat()
    {
        return NotesPageViewModel.ExportFormatFromPickerIndex(ExportFormatPicker.SelectedIndex);
    }

    private void LoadExportPreferences()
    {
        var preferred = _reportExportSettingsService.GetPreferredExportFormat();
        ExportFormatPicker.SelectedIndex = NotesPageViewModel.PickerIndexFromExportFormat(preferred);
    }

    private void InitializeDateRangeIfNeeded()
    {
        if (_hasInitializedDateRange)
        {
            return;
        }

        var today = DateTime.Today;
        ExportEndDatePicker.Date = today;
        ExportStartDatePicker.Date = today.AddDays(-30);
        _hasInitializedDateRange = true;
    }

    private async Task<IReadOnlyList<ProgressEntry>> GetMetadataEntriesForSelectedRangeAsync()
    {
        var (startUtc, endUtc) = NotesPageViewModel.BuildUtcDateRange(ExportStartDatePicker.Date, ExportEndDatePicker.Date);
        return await _progressTrackingService.GetEntriesInDateRangeAsync(startUtc, endUtc);
    }

    private async Task RefreshComparisonPreviewAsync()
    {
        try
        {
            var metadataEntries = await GetMetadataEntriesForSelectedRangeAsync();
            var normalizationMode = _confidenceSettingsService.GetSessionComparisonNormalizationMode();
            var smoothingStrength = _confidenceSettingsService.GetSessionComparisonSmoothingStrength();
            var snapshot = _comparisonSnapshotCacheService.GetOrBuild(metadataEntries, normalizationMode, smoothingStrength);
            var state = _viewModel.BuildComparisonPreviewState(snapshot);

            ComparisonPreviewNarrativeLabel.Text = state.ComparisonNarrativeText;
            ComparisonPreviewNormalizationLabel.Text = state.NormalizationModeText;
            ComparisonPreviewBadgesCollection.ItemsSource = state.SummaryBadges;
            ComparisonPreviewTimelineCollection.ItemsSource = state.TimelineRows;
        }
        catch (Exception ex)
        {
            ComparisonPreviewNarrativeLabel.Text = $"Comparison narrative: {ex.Message}";
            ComparisonPreviewNormalizationLabel.Text = "Normalization: -";
            ComparisonPreviewBadgesCollection.ItemsSource = Array.Empty<object>();
            ComparisonPreviewTimelineCollection.ItemsSource = Array.Empty<object>();
        }
    }

    private void OnAssignmentSnapshotSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = e.CurrentSelection?.FirstOrDefault() as AssignmentSnapshotRow;
        if (selected is null)
        {
            return;
        }

        var snapshot = _assignmentSnapshots.FirstOrDefault(item => item.Id == selected.SnapshotId);
        if (snapshot is null)
        {
            return;
        }

        var state = _viewModel.BuildAssignmentAnalyticsStateForSnapshot(snapshot, _assignmentSnapshots);
        ApplyAssignmentAnalyticsState(state);
    }

    private void ApplyAssignmentAnalyticsState(AssignmentAnalyticsState state)
    {
        AssignmentAnalyticsSummaryLabel.Text = state.SummaryText;
        SeverityTrendChartLabel.Text = state.SeverityTrendText;
        InstabilityTrendChartLabel.Text = state.InstabilityTrendText;
        DeclineTrendChartLabel.Text = state.DeclineTrendText;
        FrequencyTrendChartLabel.Text = state.FrequencyTrendText;
        ConfidenceTrendChartLabel.Text = state.ConfidenceTrendText;
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
