using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Pages;

public partial class SettingsPage : ContentPage
{
    private readonly ConfidenceSettingsService _settingsService;

    public SettingsPage()
    {
        InitializeComponent();
        _settingsService = ResolveService<ConfidenceSettingsService>();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        try
        {
            var thresholds = _settingsService.GetThresholds();
            ModerateSlider.Value = thresholds.ModerateThreshold;
            HighSlider.Value = thresholds.HighThreshold;
            RefreshLabels();
            StatusLabel.Text = "Current thresholds loaded.";
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
            StatusLabel.Text = "Thresholds saved.";
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
            var thresholds = _settingsService.GetThresholds();
            ModerateSlider.Value = thresholds.ModerateThreshold;
            HighSlider.Value = thresholds.HighThreshold;
            RefreshLabels();
            StatusLabel.Text = "Thresholds reset to defaults.";
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
