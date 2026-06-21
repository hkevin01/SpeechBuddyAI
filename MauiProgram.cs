namespace SpeechBuddyAI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        try
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

            builder.Services.AddSingleton<Services.SpeechScoring.ISpeechScoringAdapter, Services.SpeechScoring.OfflineSpeechScoringAdapter>();
            builder.Services.AddSingleton<Services.SpeechScoring.ISpeechScoringAdapter, Services.SpeechScoring.FallbackCloudSpeechScoringAdapter>();
            builder.Services.AddSingleton<Services.Confidence.IKeyValueStore, Services.Confidence.MauiPreferencesKeyValueStore>();
            builder.Services.AddSingleton<Services.Confidence.ConfidenceSettingsService>();
            builder.Services.AddSingleton<Services.Confidence.IConfidenceThresholdProvider>(sp =>
                sp.GetRequiredService<Services.Confidence.ConfidenceSettingsService>());
            builder.Services.AddSingleton<Services.Confidence.ConfidenceCalculator>();
            builder.Services.AddSingleton<Services.AiSpeechService>();
            builder.Services.AddSingleton<Services.AiTextService>();
            builder.Services.AddSingleton<Services.ProgressTrackingService>();
            builder.Services.AddSingleton<Services.TrendAnalysisService>();
            builder.Services.AddSingleton<Services.ReportService>();

            return builder.Build();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize MAUI application services.", ex);
        }
    }
}
