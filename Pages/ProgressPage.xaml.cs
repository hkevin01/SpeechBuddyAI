using SpeechBuddyAI.Services;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Pages.ViewModels;

namespace SpeechBuddyAI.Pages;

public partial class ProgressPage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly TrendAnalysisService _trendAnalysisService;
    private readonly ConfidenceSettingsService _confidenceSettingsService;
    private readonly ComparisonSnapshotCacheService _comparisonSnapshotCacheService;
    private readonly ProgressPageViewModel _viewModel;

    private IReadOnlyList<Models.ProgressEntry> _allEntries = Array.Empty<Models.ProgressEntry>();

    public ProgressPage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _trendAnalysisService = ResolveService<TrendAnalysisService>();
        _confidenceSettingsService = ResolveService<ConfidenceSettingsService>();
        _comparisonSnapshotCacheService = ResolveService<ComparisonSnapshotCacheService>();
        _viewModel = new ProgressPageViewModel();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        TrajectoryLabel.Text = "Loading progress...";

        try
        {
            _allEntries = await _progressTrackingService.GetEntriesAsync();
            var savedRange = _confidenceSettingsService.GetProgressDateRange(DateTime.Now);
            var filterState = _viewModel.BuildFilterState(savedRange.StartDateLocal, savedRange.EndDateLocal, DateTime.Now);
            FilterEntry.Text = _confidenceSettingsService.GetDefaultProgressTargetFilter();
            ProgressStartDatePicker.Date = filterState.StartDateLocal;
            ProgressEndDatePicker.Date = filterState.EndDateLocal;
            DateRangeSummaryLabel.Text = filterState.DateRangeSummaryText;
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
        _confidenceSettingsService.SaveDefaultProgressTargetFilter(e.NewTextValue);
        ApplyFilter((e.NewTextValue ?? string.Empty).Trim());
    }

    private void OnFilterClearClicked(object? sender, EventArgs e)
    {
        FilterEntry.Text = string.Empty;
        ApplyFilter(string.Empty);
    }

    private void OnDateRangeChanged(object? sender, DateChangedEventArgs e)
    {
        _confidenceSettingsService.SaveProgressDateRange(ProgressStartDatePicker.Date, ProgressEndDatePicker.Date);
        ApplyFilter((FilterEntry.Text ?? string.Empty).Trim());
    }

    private void ApplyFilter(string filterText)
    {
        var filterState = _viewModel.BuildFilterState(ProgressStartDatePicker.Date, ProgressEndDatePicker.Date, DateTime.Now);
        DateRangeSummaryLabel.Text = filterState.DateRangeSummaryText;

        var dateFiltered = _viewModel.FilterEntriesByDateRange(_allEntries, filterState.StartDateLocal, filterState.EndDateLocal);
        var filtered = _viewModel.FilterEntries(dateFiltered, filterText);

        ProgressCollection.ItemsSource = filtered;

        var thresholds = _confidenceSettingsService.GetThresholds();
        var trendState = _viewModel.BuildTrendViewState(filtered, _trendAnalysisService, thresholds, 12);
        ModerateThresholdLabel.Text = trendState.ModerateThresholdText;
        HighThresholdLabel.Text = trendState.HighThresholdText;
        TrendCollection.ItemsSource = trendState.TrendPoints;
        TrajectoryLabel.Text = trendState.TrajectoryText;
        ApplySessionComparison(filtered);
    }

    private void ApplySessionComparison(IReadOnlyList<Models.ProgressEntry> entries)
    {
        var normalizationMode = _confidenceSettingsService.GetSessionComparisonNormalizationMode();
        var smoothingStrength = _confidenceSettingsService.GetSessionComparisonSmoothingStrength();
        var snapshot = _comparisonSnapshotCacheService.GetOrBuild(entries, normalizationMode, smoothingStrength);
        ApplyComparisonState(_viewModel.BuildComparisonViewState(snapshot, snapshot.ComparisonNarrative));
    }

    private void ApplyComparisonState(ProgressComparisonViewState state)
    {
        CurrentSessionDateLabel.Text = state.CurrentSessionDateText;
        CurrentSessionAttemptsLabel.Text = state.CurrentSessionAttemptsText;
        CurrentSessionOverallLabel.Text = state.CurrentSessionOverallText;
        CurrentSessionConfidenceLabel.Text = state.CurrentSessionConfidenceText;
        PreviousSessionDateLabel.Text = state.PreviousSessionDateText;
        PreviousSessionAttemptsLabel.Text = state.PreviousSessionAttemptsText;
        PreviousSessionOverallLabel.Text = state.PreviousSessionOverallText;
        PreviousSessionConfidenceLabel.Text = state.PreviousSessionConfidenceText;
        OverallDeltaLabel.Text = state.OverallDeltaText;
        ConfidenceDeltaLabel.Text = state.ConfidenceDeltaText;
        ConfidenceMovementSummaryLabel.Text = state.ConfidenceMovementText;
        ComparisonNarrativeLabel.Text = state.ComparisonNarrativeText;
        NormalizationModeLabel.Text = state.NormalizationModeText;
        SummaryBadgesCollection.ItemsSource = state.SummaryBadges;
        TargetComparisonChips.ItemsSource = state.TargetComparisons;
        ConfidenceLegendCollection.ItemsSource = state.ConfidenceLegendItems;
        SessionTimelineCollection.ItemsSource = state.TimelineRows;
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
