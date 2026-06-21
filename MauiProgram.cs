namespace SpeechBuddyAI;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        builder.Services.AddSingleton<Services.AiSpeechService>();
        builder.Services.AddSingleton<Services.AiTextService>();
        builder.Services.AddSingleton<Services.ProgressTrackingService>();

        return builder.Build();
    }
}
