using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ConfidenceSettingsService _settingsService;
    private readonly ReportExportSettingsService _reportExportSettingsService;

    public SettingsPage()
    {
        InitializeComponent();
        _settingsService = ResolveService<ConfidenceSettingsService>();
        _reportExportSettingsService = ResolveService<ReportExportSettingsService>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var thresholds = _settingsService.GetThresholds();
            ModerateSlider.Value = thresholds.ModerateThreshold;
            HighSlider.Value = thresholds.HighThreshold;
            ComparisonNormalizationPicker.SelectedIndex =
                _settingsService.GetSessionComparisonNormalizationMode() == Models.SessionComparisonNormalizationMode.DayWeighted ? 1 : 0;
            ShareBehaviorPicker.SelectedIndex =
                _reportExportSettingsService.GetDefaultShareBehavior() == ReportShareBehavior.ExportOnly ? 0 : 1;
            RefreshLabels();
            StatusLabel.Text = "Current clinician settings loaded.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }

    private void OnSliderValueChanged(object? sender, ValueChangedEventArgs e)
    {
        RefreshLabels();
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        var moderate = ModerateSlider.Value;
        var high = HighSlider.Value;

        if (high <= moderate)
        {
            StatusLabel.Text = "High threshold must be greater than moderate threshold.";
            return;
        }

        try
        {
            _settingsService.SaveThresholds(moderate, high);
            var normalizationMode = ComparisonNormalizationPicker.SelectedIndex == 1
                ? Models.SessionComparisonNormalizationMode.DayWeighted
                : Models.SessionComparisonNormalizationMode.AttemptWeighted;
            _settingsService.SaveSessionComparisonNormalizationMode(normalizationMode);
            StatusLabel.Text = "Clinician settings saved.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        try
        {
            _settingsService.ResetDefaults();
            _reportExportSettingsService.ResetDefaults();
            var thresholds = _settingsService.GetThresholds();
            ModerateSlider.Value = thresholds.ModerateThreshold;
            HighSlider.Value = thresholds.HighThreshold;
            ComparisonNormalizationPicker.SelectedIndex = 0;
            ShareBehaviorPicker.SelectedIndex = 1;
            RefreshLabels();
            StatusLabel.Text = "Clinician settings and share behavior reset to defaults.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }

    private void OnSaveShareBehaviorClicked(object? sender, EventArgs e)
    {
        try
        {
            var behavior = ShareBehaviorPicker.SelectedIndex == 0
                ? ReportShareBehavior.ExportOnly
                : ReportShareBehavior.ExportAndShare;
            _reportExportSettingsService.SaveDefaultShareBehavior(behavior);
            StatusLabel.Text = "Share behavior saved.";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }

    private void RefreshLabels()
    {
        ModerateValueLabel.Text = $"{ModerateSlider.Value:P0}";
        HighValueLabel.Text = $"{HighSlider.Value:P0}";
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
