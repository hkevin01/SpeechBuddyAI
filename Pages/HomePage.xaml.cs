using SpeechBuddyAI.Services;
using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Pages;

public partial class HomePage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly AiTextService _aiTextService;
    private readonly DashboardStatsService _dashboardStatsService;
    private readonly AssignmentSnapshotService _assignmentSnapshotService;

    public HomePage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _aiTextService = ResolveService<AiTextService>();
        _dashboardStatsService = ResolveService<DashboardStatsService>();
        _assignmentSnapshotService = ResolveService<AssignmentSnapshotService>();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await RefreshStatsAsync();
    }

    private async Task RefreshStatsAsync()
    {
        try
        {
            var entries = await _progressTrackingService.GetEntriesAsync();
            var stats = _dashboardStatsService.Compute(entries);
            TotalAttemptsLabel.Text = stats.TotalAttempts.ToString();
            AverageScoreLabel.Text = stats.TotalAttempts > 0 ? $"{stats.AverageScore:P0}" : "-";
            MostPracticedLabel.Text = string.IsNullOrEmpty(stats.MostPracticedTarget) ? "-" : stats.MostPracticedTarget;
            StreakLabel.Text = stats.CurrentPracticeStreakDays > 0 ? $"{stats.CurrentPracticeStreakDays}d" : "0";
            MostImprovedLabel.Text = $"Most improved: {(string.IsNullOrEmpty(stats.MostImprovedTarget) ? "-" : stats.MostImprovedTarget)}";
        }
        catch
        {
            // stats refresh is non-critical
        }
    }

    private async void OnGenerateAssignmentClicked(object? sender, EventArgs e)
    {
        AssignmentTitleLabel.Text = "Generating assignment...";
        AssignmentRationaleLabel.Text = string.Empty;
        AssignmentTargetsLabel.Text = string.Empty;
        AssignmentWordsLabel.Text = string.Empty;
        AssignmentReasonDetailsLabel.Text = string.Empty;

        try
        {
            var history = await _progressTrackingService.GetRecentEntriesAsync(120);
            var assignment = await _aiTextService.GenerateHomeAssignmentAsync(history);
            await _assignmentSnapshotService.SaveSnapshotAsync(assignment, history.Count);

            AssignmentTitleLabel.Text = assignment.Title;
            AssignmentRationaleLabel.Text = assignment.Rationale;
            AssignmentTargetsLabel.Text = "Focus Targets: " +
                                          (assignment.FocusTargets.Count == 0
                                              ? "none"
                                              : string.Join(", ", assignment.FocusTargets));
            AssignmentWordsLabel.Text = "Suggested Words: " + string.Join(", ", assignment.SuggestedWords);
            AssignmentReasonDetailsLabel.Text = BuildFocusReasonSummary(assignment.FocusTargetReasons);
        }
        catch (Exception ex)
        {
            AssignmentTitleLabel.Text = "Assignment generation failed";
            AssignmentRationaleLabel.Text = ex.Message;
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

    private static string BuildFocusReasonSummary(IReadOnlyList<AssignmentFocusTargetReason> reasons)
    {
        if (reasons is null || reasons.Count == 0)
        {
            return string.Empty;
        }

        return "Selection details: " + string.Join(
            " | ",
            reasons.Select(reason =>
                $"{reason.TargetSound} p={reason.PriorityScore:0.00}, seq {reason.PositionSequence}"));
    }
}
