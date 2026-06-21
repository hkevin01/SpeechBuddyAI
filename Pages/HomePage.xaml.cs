using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Pages;

public partial class HomePage : ContentPage
{
    private readonly ProgressTrackingService _progressTrackingService;
    private readonly AiTextService _aiTextService;
    private readonly DashboardStatsService _dashboardStatsService;

    public HomePage()
    {
        InitializeComponent();
        _progressTrackingService = ResolveService<ProgressTrackingService>();
        _aiTextService = ResolveService<AiTextService>();
        _dashboardStatsService = ResolveService<DashboardStatsService>();
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

        try
        {
            var weakEntries = await _progressTrackingService.GetWeakestEntriesAsync(5);
            var assignment = await _aiTextService.GenerateHomeAssignmentAsync(weakEntries);

            AssignmentTitleLabel.Text = assignment.Title;
            AssignmentRationaleLabel.Text = assignment.Rationale;
            AssignmentTargetsLabel.Text = "Focus Targets: " +
                                          (assignment.FocusTargets.Count == 0
                                              ? "none"
                                              : string.Join(", ", assignment.FocusTargets));
            AssignmentWordsLabel.Text = "Suggested Words: " + string.Join(", ", assignment.SuggestedWords);
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
}
