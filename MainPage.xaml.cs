namespace SpeechBuddyAI;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnPracticeClicked(object? sender, EventArgs e)
    {
        var shell = Shell.Current;
        if (shell is null)
        {
            await DisplayAlert("Navigation Error", "App shell is not available.", "OK");
            return;
        }

        try
        {
            await shell.GoToAsync("//Practice");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Navigation Error", ex.Message, "OK");
        }
    }
}
