namespace SpeechBuddyAI;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
    }

    private async void OnPracticeClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Practice");
    }
}
