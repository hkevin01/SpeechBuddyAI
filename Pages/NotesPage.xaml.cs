using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SpeechBuddyAI.Pages.ViewModels;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;
using SpeechBuddyAI.Views;

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
    private int? _selectedSnapshotId;
    private string? _selectedTarget;
    private LayoutBucket _currentLayoutBucket = LayoutBucket.Unknown;

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
        ApplyResponsiveLayout(Width);
        LoadExportPreferences();
        InitializeDateRangeIfNeeded();
        await RefreshHistoryAsync();
        await RefreshComparisonPreviewAsync();
        await RefreshAssignmentAnalyticsAsync();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        ApplyResponsiveLayout(width);
    }

    private async void OnGenerateSummariesClicked(object? sender, EventArgs e)
    {
        SoapSummaryLabel.Text = "Generating clinician summary...";
        ParentSummaryLabel.Text = "Generating parent summary...";
        NoteStatusLabel.Text = string.Empty;
        ApplyBanner(StatusBannerState.Info("Generating clinician and parent summaries."));

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
            ApplyBanner(StatusBannerState.Success("Summaries ready. Save the note to persist this report."));
        }
        catch (Exception ex)
        {
            SoapSummaryLabel.Text = "Unable to generate summaries.";
            ParentSummaryLabel.Text = ex.Message;
            ApplyBanner(StatusBannerState.Warning(ex.Message));
        }
    }

    private async void OnSaveNoteClicked(object? sender, EventArgs e)
    {
        if (_viewModel.PendingNote is null)
        {
            NoteStatusLabel.Text = "Generate summaries first before saving.";
            ApplyBanner(StatusBannerState.Warning("Generate summaries first before saving."));
            return;
        }

        try
        {
            await _noteStorageService.SaveNoteAsync(_viewModel.PendingNote);
            _viewModel.MarkSaved();
            NoteStatusLabel.Text = "Note saved.";
            ApplyBanner(StatusBannerState.Success("Note saved successfully."));
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            NoteStatusLabel.Text = ex.Message;
            ApplyBanner(StatusBannerState.Warning(ex.Message));
        }
    }

    private async void OnExportLatestClicked(object? sender, EventArgs e)
    {
        NoteStatusLabel.Text = "Exporting latest report...";
        ApplyBanner(StatusBannerState.Info("Exporting latest report."));

        try
        {
            var note = await ResolveLatestExportCandidateAsync();
            if (note is null)
            {
                NoteStatusLabel.Text = "No report available to export. Generate and save a note first.";
                ApplyBanner(StatusBannerState.Warning(NoteStatusLabel.Text));
                return;
            }

            var metadataEntries = await GetMetadataEntriesForSelectedRangeAsync();
            var format = GetSelectedExportFormat();
            var filePath = await _reportService.ExportReportAsync(note, metadataEntries, format);
            NoteStatusLabel.Text = $"Report exported: {Path.GetFileName(filePath)}";
            ApplyBanner(StatusBannerState.Success(NoteStatusLabel.Text));
        }
        catch (Exception ex)
        {
            NoteStatusLabel.Text = ex.Message;
            ApplyBanner(StatusBannerState.Warning(ex.Message));
        }
    }

    private async void OnShareLatestClicked(object? sender, EventArgs e)
    {
        NoteStatusLabel.Text = "Preparing report for sharing...";
        ApplyBanner(StatusBannerState.Info("Preparing report for sharing."));

        try
        {
            var note = await ResolveLatestExportCandidateAsync();
            if (note is null)
            {
                NoteStatusLabel.Text = "No report available to share. Generate and save a note first.";
                ApplyBanner(StatusBannerState.Warning(NoteStatusLabel.Text));
                return;
            }

            var metadataEntries = await GetMetadataEntriesForSelectedRangeAsync();
            var format = GetSelectedExportFormat();
            var behavior = _reportExportSettingsService.GetDefaultShareBehavior();

            if (behavior == ReportShareBehavior.ExportOnly)
            {
                var filePath = await _reportService.ExportReportAsync(note, metadataEntries, format);
                NoteStatusLabel.Text = $"Report exported only (per settings): {Path.GetFileName(filePath)}";
                ApplyBanner(StatusBannerState.Success(NoteStatusLabel.Text));
                return;
            }

            await _reportService.ShareReportAsync(note, metadataEntries, format);
            NoteStatusLabel.Text = "Share flow opened.";
            ApplyBanner(StatusBannerState.Success(NoteStatusLabel.Text));
        }
        catch (Exception ex)
        {
            NoteStatusLabel.Text = ex.Message;
            ApplyBanner(StatusBannerState.Warning(ex.Message));
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
        ApplyBanner(StatusBannerState.Info(NoteStatusLabel.Text));
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
            var state = _viewModel.BuildAssignmentAnalyticsState(_assignmentSnapshots, _selectedTarget, _selectedSnapshotId);
            ApplyAssignmentAnalyticsState(state);
            AssignmentSnapshotCollection.ItemsSource = state.SnapshotRows;
            AssignmentTargetPicker.ItemsSource = state.TargetOptions;
            if (state.TargetOptions.Count > 0)
            {
                var selectedIndex = state.TargetOptions
                    .Select((value, index) => new { value, index })
                    .FirstOrDefault(item => string.Equals(item.value, state.SelectedTarget, StringComparison.OrdinalIgnoreCase))?.index ?? 0;
                AssignmentTargetPicker.SelectedIndex = selectedIndex;
            }
        }
        catch (Exception ex)
        {
            AssignmentAnalyticsSummaryLabel.Text = ex.Message;
            AssignmentSnapshotCollection.ItemsSource = Array.Empty<object>();
            AssignmentTargetPicker.ItemsSource = Array.Empty<string>();
            SeverityTrendChartLabel.Text = "Severity trend: -";
            InstabilityTrendChartLabel.Text = "Instability trend: -";
            DeclineTrendChartLabel.Text = "Decline trend: -";
            FrequencyTrendChartLabel.Text = "Frequency trend: -";
            ConfidenceTrendChartLabel.Text = "Confidence trend: -";
            AssignmentModelAuditSummaryLabel.Text = "No assignment model-audit snapshots available.";
            AssignmentFormulaVersionSummaryLabel.Text = "Formula versions: n/a";
            AssignmentAdvisoryWeightLabel.Text = "No advisory weight suggestion available yet.";
            CalibrationNext1SparklineCollection.ItemsSource = Array.Empty<object>();
            CalibrationNext3SparklineCollection.ItemsSource = Array.Empty<object>();
            TraceDriftSparklineCollection.ItemsSource = Array.Empty<object>();
            AssignmentModelAuditCollection.ItemsSource = Array.Empty<object>();
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

        _selectedSnapshotId = snapshot.Id;
        var state = _viewModel.BuildAssignmentAnalyticsState(_assignmentSnapshots, _selectedTarget, _selectedSnapshotId);
        ApplyAssignmentAnalyticsState(state);
    }

    private async void OnAssignmentTargetChanged(object? sender, EventArgs e)
    {
        if (AssignmentTargetPicker.SelectedItem is not string selected || string.IsNullOrWhiteSpace(selected))
        {
            return;
        }

        _selectedTarget = selected;
        await RefreshAssignmentAnalyticsAsync();
    }

    private void ApplyAssignmentAnalyticsState(AssignmentAnalyticsState state)
    {
        AssignmentAnalyticsSummaryLabel.Text = state.SummaryText;
        SeverityTrendChartLabel.Text = state.SeverityTrendText;
        InstabilityTrendChartLabel.Text = state.InstabilityTrendText;
        DeclineTrendChartLabel.Text = state.DeclineTrendText;
        FrequencyTrendChartLabel.Text = state.FrequencyTrendText;
        ConfidenceTrendChartLabel.Text = state.ConfidenceTrendText;
        SeveritySparklineCollection.ItemsSource = state.SeverityPoints;
        InstabilitySparklineCollection.ItemsSource = state.InstabilityPoints;
        DeclineSparklineCollection.ItemsSource = state.DeclinePoints;
        FrequencySparklineCollection.ItemsSource = state.FrequencyPoints;
        ConfidenceSparklineCollection.ItemsSource = state.ConfidencePoints;
        AssignmentModelAuditSummaryLabel.Text = state.ModelAuditSummaryText;
        AssignmentFormulaVersionSummaryLabel.Text = state.FormulaVersionSummaryText;
        AssignmentAdvisoryWeightLabel.Text = state.AdvisoryWeightSuggestionText;
        CalibrationNext1SparklineCollection.ItemsSource = state.Next1CalibrationPoints;
        CalibrationNext3SparklineCollection.ItemsSource = state.Next3CalibrationPoints;
        TraceDriftSparklineCollection.ItemsSource = state.TraceDriftPoints;
        AssignmentModelAuditCollection.ItemsSource = state.ModelAuditRows;
    }

    private static T ResolveService<T>() where T : notnull
    {
        var provider = Application.Current?.Handler?.MauiContext?.Services;
        var service = provider?.GetService(typeof(T));

        if (service is T typedService)
            return typedService;

        throw new InvalidOperationException($"Service {typeof(T).Name} is not registered.");
    }

    private void ApplyResponsiveLayout(double width)
    {
        var bucket = width >= 768
            ? LayoutBucket.Tablet
            : width > 0 && width < 390
                ? LayoutBucket.CompactPhone
                : LayoutBucket.Phone;

        if (bucket == _currentLayoutBucket)
        {
            return;
        }

        _currentLayoutBucket = bucket;

        if (bucket == LayoutBucket.CompactPhone)
        {
            ExportFormatRow.Orientation = StackOrientation.Vertical;
            ExportFormatRow.Spacing = 6;
            ExportDateRangeRow.Orientation = StackOrientation.Vertical;
            ExportDateRangeRow.Spacing = 6;
            AssignmentTargetRow.Orientation = StackOrientation.Vertical;
            AssignmentTargetRow.Spacing = 6;
            NotesActionGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star)
            };
            Grid.SetColumnSpan(NotesActionGrid.Children[0], 1);
            Grid.SetColumn(NotesActionGrid.Children[0], 0);
            Grid.SetRow(NotesActionGrid.Children[0], 0);
            Grid.SetColumn(NotesActionGrid.Children[1], 0);
            Grid.SetRow(NotesActionGrid.Children[1], 1);
            NotesActionGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            };
            ReportActionGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star)
            };
            Grid.SetColumn(ReportActionGrid.Children[0], 0);
            Grid.SetRow(ReportActionGrid.Children[0], 0);
            Grid.SetColumn(ReportActionGrid.Children[1], 0);
            Grid.SetRow(ReportActionGrid.Children[1], 1);
            ReportActionGrid.RowDefinitions = new RowDefinitionCollection
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            };
            PrioritySparklineGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(96)),
                new ColumnDefinition(GridLength.Star)
            };
            ModelAuditSparklineGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(102)),
                new ColumnDefinition(GridLength.Star)
            };
            AssignmentSnapshotCollection.HeightRequest = 220;
            AssignmentModelAuditCollection.HeightRequest = 220;
        }
        else
        {
            ExportFormatRow.Orientation = StackOrientation.Horizontal;
            ExportFormatRow.Spacing = 10;
            ExportDateRangeRow.Orientation = StackOrientation.Horizontal;
            ExportDateRangeRow.Spacing = 10;
            AssignmentTargetRow.Orientation = StackOrientation.Horizontal;
            AssignmentTargetRow.Spacing = 8;
            NotesActionGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            };
            NotesActionGrid.RowDefinitions = new RowDefinitionCollection();
            Grid.SetColumn(NotesActionGrid.Children[0], 0);
            Grid.SetRow(NotesActionGrid.Children[0], 0);
            Grid.SetColumn(NotesActionGrid.Children[1], 1);
            Grid.SetRow(NotesActionGrid.Children[1], 0);
            ReportActionGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            };
            ReportActionGrid.RowDefinitions = new RowDefinitionCollection();
            Grid.SetColumn(ReportActionGrid.Children[0], 0);
            Grid.SetRow(ReportActionGrid.Children[0], 0);
            Grid.SetColumn(ReportActionGrid.Children[1], 1);
            Grid.SetRow(ReportActionGrid.Children[1], 0);
            PrioritySparklineGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(110)),
                new ColumnDefinition(GridLength.Star)
            };
            ModelAuditSparklineGrid.ColumnDefinitions = new ColumnDefinitionCollection
            {
                new ColumnDefinition(new GridLength(120)),
                new ColumnDefinition(GridLength.Star)
            };
            AssignmentSnapshotCollection.HeightRequest = bucket == LayoutBucket.Tablet ? 240 : 180;
            AssignmentModelAuditCollection.HeightRequest = bucket == LayoutBucket.Tablet ? 220 : 180;
        }
    }

    private void ApplyBanner(StatusBannerState state)
    {
        NotesStatusBanner.Message = state.Message;
        NotesStatusBanner.Tone = state.Tone;
    }

    private enum LayoutBucket
    {
        Unknown,
        CompactPhone,
        Phone,
        Tablet
    }
}
