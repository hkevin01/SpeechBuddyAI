using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using SpeechBuddyAI.Pages.ViewModels;

namespace SpeechBuddyAI.Views;

public sealed class StatusBannerView : ContentView
{
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message),
        typeof(string),
        typeof(StatusBannerView),
        string.Empty,
        propertyChanged: OnVisualPropertyChanged);

    public static readonly BindableProperty ToneProperty = BindableProperty.Create(
        nameof(Tone),
        typeof(StatusBannerTone),
        typeof(StatusBannerView),
        StatusBannerTone.Info,
        propertyChanged: OnVisualPropertyChanged);

    private readonly Frame _frame;
    private readonly Label _label;

    public StatusBannerView()
    {
        _label = new Label
        {
            LineBreakMode = LineBreakMode.WordWrap,
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center
        };

        _frame = new Frame
        {
            CornerRadius = 14,
            Padding = new Thickness(12, 10),
            HasShadow = false,
            IsClippedToBounds = true,
            Content = _label
        };

        Content = _frame;
        UpdateVisualState();
    }

    public string Message
    {
        get => (string)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public StatusBannerTone Tone
    {
        get => (StatusBannerTone)GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    private static void OnVisualPropertyChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is StatusBannerView banner)
        {
            banner.UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        var message = (Message ?? string.Empty).Trim();
        _label.Text = message;
        IsVisible = !string.IsNullOrWhiteSpace(message);

        SemanticProperties.SetDescription(this, message);
        SemanticProperties.SetHint(this, "Status message for the current screen action.");

        var (backgroundKey, borderKey, textKey) = Tone switch
        {
            StatusBannerTone.Success => ("SuccessContainer", "SuccessOutline", "OnSuccessContainer"),
            StatusBannerTone.Warning => ("WarningContainer", "WarningOutline", "OnWarningContainer"),
            StatusBannerTone.ReviewRequired => ("ReviewContainer", "ReviewOutline", "OnReviewContainer"),
            _ => ("InfoContainer", "InfoOutline", "OnInfoContainer")
        };

        _frame.BackgroundColor = ResolveColor(backgroundKey, Colors.LightGray);
        _frame.BorderColor = ResolveColor(borderKey, Colors.Gray);
        _label.TextColor = ResolveColor(textKey, Colors.Black);
    }

    private static Color ResolveColor(string resourceKey, Color fallback)
    {
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var resource) == true && resource is Color color)
        {
            return color;
        }

        return fallback;
    }
}
