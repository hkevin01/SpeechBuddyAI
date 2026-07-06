using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;
using SpeechBuddyAI.Models;
using SpeechBuddyAI.Views;

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
            ComparisonSmoothingPicker.SelectedIndex = (int)_settingsService.GetSessionComparisonSmoothingStrength();
            var assignmentSettings = _settingsService.GetAssignmentPrioritySettings();
            AssignmentSeveritySlider.Value = assignmentSettings.SeverityWeight;
            AssignmentInstabilitySlider.Value = assignmentSettings.InstabilityWeight;
            AssignmentDeclineSlider.Value = assignmentSettings.DeclineWeight;
            AssignmentFrequencySlider.Value = assignmentSettings.FrequencyWeight;
            AssignmentConfidencePenaltySlider.Value = assignmentSettings.ConfidencePenaltyStrength;
            AssignmentConfidenceVarianceGateSlider.Value = _settingsService.GetAssignmentConfidenceVarianceGate();
            AssignmentCiMinSamplesSlider.Value = _settingsService.GetAssignmentConfidenceIntervalMinSamples();
            AssignmentSuppressionBehaviorPicker.SelectedIndex = (int)_settingsService.GetAssignmentSuppressionBehavior();
            AssignmentUncertaintyBudgetCapSlider.Value = _settingsService.GetAssignmentUncertaintyBudgetCap();
            AssignmentPositionInitialSlider.Value = assignmentSettings.PositionInitialWeight;
            AssignmentPositionMedialSlider.Value = assignmentSettings.PositionMedialWeight;
            AssignmentPositionFinalSlider.Value = assignmentSettings.PositionFinalWeight;
            var calibrationCoefficients = _settingsService.GetCalibrationPositionSupportCoefficients();
            CalibrationInitialSupportSlider.Value = calibrationCoefficients.InitialCoefficient;
            CalibrationMedialSupportSlider.Value = calibrationCoefficients.MedialCoefficient;
            CalibrationFinalSupportSlider.Value = calibrationCoefficients.FinalCoefficient;
            CalibrationTableActivationSamplesSlider.Value = _settingsService.GetCalibrationTableActivationMinSamples();
            CalibrationQualityThresholdSlider.Value = _settingsService.GetCalibrationQualityActivationThreshold();
            ShareBehaviorPicker.SelectedIndex =
                _reportExportSettingsService.GetDefaultShareBehavior() == ReportShareBehavior.ExportOnly ? 0 : 1;
            RefreshLabels();
            StatusLabel.Text = "Current clinician settings loaded.";
            SettingsStatusBanner.Message = StatusLabel.Text;
            SettingsStatusBanner.Tone = StatusBannerTone.Info;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
            SettingsStatusBanner.Message = ex.Message;
            SettingsStatusBanner.Tone = StatusBannerTone.Warning;
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
            SettingsStatusBanner.Message = StatusLabel.Text;
            SettingsStatusBanner.Tone = StatusBannerTone.Warning;
            return;
        }

        try
        {
            _settingsService.SaveThresholds(moderate, high);
            var normalizationMode = ComparisonNormalizationPicker.SelectedIndex == 1
                ? Models.SessionComparisonNormalizationMode.DayWeighted
                : Models.SessionComparisonNormalizationMode.AttemptWeighted;
            _settingsService.SaveSessionComparisonNormalizationMode(normalizationMode);
            _settingsService.SaveSessionComparisonSmoothingStrength((Models.SessionComparisonSmoothingStrength)Math.Clamp(ComparisonSmoothingPicker.SelectedIndex, 0, 2));
            var assignmentSettings = new AssignmentPrioritySettings
            {
                SeverityWeight = AssignmentSeveritySlider.Value,
                InstabilityWeight = AssignmentInstabilitySlider.Value,
                DeclineWeight = AssignmentDeclineSlider.Value,
                FrequencyWeight = AssignmentFrequencySlider.Value,
                ConfidencePenaltyStrength = AssignmentConfidencePenaltySlider.Value,
                PositionInitialWeight = AssignmentPositionInitialSlider.Value,
                PositionMedialWeight = AssignmentPositionMedialSlider.Value,
                PositionFinalWeight = AssignmentPositionFinalSlider.Value
            };
            _settingsService.SaveAssignmentPrioritySettings(assignmentSettings);
            _settingsService.SaveAssignmentConfidenceVarianceGate(AssignmentConfidenceVarianceGateSlider.Value);
            _settingsService.SaveAssignmentConfidenceIntervalMinSamples((int)Math.Round(AssignmentCiMinSamplesSlider.Value));
            _settingsService.SaveAssignmentSuppressionBehavior((AssignmentSuppressionBehavior)Math.Clamp(AssignmentSuppressionBehaviorPicker.SelectedIndex, 0, 2));
            _settingsService.SaveAssignmentUncertaintyBudgetCap(AssignmentUncertaintyBudgetCapSlider.Value);
            _settingsService.SaveCalibrationPositionSupportCoefficients(new PositionSupportCoefficients(
                CalibrationInitialSupportSlider.Value,
                CalibrationMedialSupportSlider.Value,
                CalibrationFinalSupportSlider.Value));
            _settingsService.SaveCalibrationTableActivationMinSamples((int)Math.Round(CalibrationTableActivationSamplesSlider.Value));
            _settingsService.SaveCalibrationQualityActivationThreshold(CalibrationQualityThresholdSlider.Value);
            StatusLabel.Text = "Clinician settings saved.";
            SettingsStatusBanner.Message = StatusLabel.Text;
            SettingsStatusBanner.Tone = StatusBannerTone.Success;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
            SettingsStatusBanner.Message = ex.Message;
            SettingsStatusBanner.Tone = StatusBannerTone.Warning;
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
            ComparisonSmoothingPicker.SelectedIndex = 1;
            var assignmentDefaults = _settingsService.GetAssignmentPrioritySettings();
            AssignmentSeveritySlider.Value = assignmentDefaults.SeverityWeight;
            AssignmentInstabilitySlider.Value = assignmentDefaults.InstabilityWeight;
            AssignmentDeclineSlider.Value = assignmentDefaults.DeclineWeight;
            AssignmentFrequencySlider.Value = assignmentDefaults.FrequencyWeight;
            AssignmentConfidencePenaltySlider.Value = assignmentDefaults.ConfidencePenaltyStrength;
            AssignmentConfidenceVarianceGateSlider.Value = _settingsService.GetAssignmentConfidenceVarianceGate();
            AssignmentCiMinSamplesSlider.Value = _settingsService.GetAssignmentConfidenceIntervalMinSamples();
            AssignmentSuppressionBehaviorPicker.SelectedIndex = (int)_settingsService.GetAssignmentSuppressionBehavior();
            AssignmentUncertaintyBudgetCapSlider.Value = _settingsService.GetAssignmentUncertaintyBudgetCap();
            AssignmentPositionInitialSlider.Value = assignmentDefaults.PositionInitialWeight;
            AssignmentPositionMedialSlider.Value = assignmentDefaults.PositionMedialWeight;
            AssignmentPositionFinalSlider.Value = assignmentDefaults.PositionFinalWeight;
            var calibrationDefaults = _settingsService.GetCalibrationPositionSupportCoefficients();
            CalibrationInitialSupportSlider.Value = calibrationDefaults.InitialCoefficient;
            CalibrationMedialSupportSlider.Value = calibrationDefaults.MedialCoefficient;
            CalibrationFinalSupportSlider.Value = calibrationDefaults.FinalCoefficient;
            CalibrationTableActivationSamplesSlider.Value = _settingsService.GetCalibrationTableActivationMinSamples();
            CalibrationQualityThresholdSlider.Value = _settingsService.GetCalibrationQualityActivationThreshold();
            ShareBehaviorPicker.SelectedIndex = 1;
            RefreshLabels();
            StatusLabel.Text = "Clinician settings and share behavior reset to defaults.";
            SettingsStatusBanner.Message = StatusLabel.Text;
            SettingsStatusBanner.Tone = StatusBannerTone.Info;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
            SettingsStatusBanner.Message = ex.Message;
            SettingsStatusBanner.Tone = StatusBannerTone.Warning;
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
            SettingsStatusBanner.Message = StatusLabel.Text;
            SettingsStatusBanner.Tone = StatusBannerTone.Success;
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
            SettingsStatusBanner.Message = ex.Message;
            SettingsStatusBanner.Tone = StatusBannerTone.Warning;
        }
    }

    private void RefreshLabels()
    {
        ModerateValueLabel.Text = $"{ModerateSlider.Value:P0}";
        HighValueLabel.Text = $"{HighSlider.Value:P0}";
        AssignmentSeverityValueLabel.Text = $"{AssignmentSeveritySlider.Value:P0}";
        AssignmentInstabilityValueLabel.Text = $"{AssignmentInstabilitySlider.Value:P0}";
        AssignmentDeclineValueLabel.Text = $"{AssignmentDeclineSlider.Value:P0}";
        AssignmentFrequencyValueLabel.Text = $"{AssignmentFrequencySlider.Value:P0}";
        AssignmentConfidencePenaltyValueLabel.Text = $"{AssignmentConfidencePenaltySlider.Value:P0}";
        AssignmentConfidenceVarianceGateValueLabel.Text = $"{AssignmentConfidenceVarianceGateSlider.Value:P1}";
        AssignmentCiMinSamplesValueLabel.Text = $"{Math.Round(AssignmentCiMinSamplesSlider.Value)}";
        AssignmentUncertaintyBudgetCapValueLabel.Text = $"{AssignmentUncertaintyBudgetCapSlider.Value:P0}";
        AssignmentPositionInitialValueLabel.Text = $"{AssignmentPositionInitialSlider.Value:P0}";
        AssignmentPositionMedialValueLabel.Text = $"{AssignmentPositionMedialSlider.Value:P0}";
        AssignmentPositionFinalValueLabel.Text = $"{AssignmentPositionFinalSlider.Value:P0}";
        CalibrationInitialSupportValueLabel.Text = $"{CalibrationInitialSupportSlider.Value:0.00}x";
        CalibrationMedialSupportValueLabel.Text = $"{CalibrationMedialSupportSlider.Value:0.00}x";
        CalibrationFinalSupportValueLabel.Text = $"{CalibrationFinalSupportSlider.Value:0.00}x";
        CalibrationTableActivationSamplesValueLabel.Text = $"{Math.Round(CalibrationTableActivationSamplesSlider.Value)} samples";
        CalibrationQualityThresholdValueLabel.Text = $"{CalibrationQualityThresholdSlider.Value:0.00}";
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
