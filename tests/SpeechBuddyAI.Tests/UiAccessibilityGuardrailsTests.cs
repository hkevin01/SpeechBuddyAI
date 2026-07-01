using System.Globalization;
using System.Xml.Linq;

namespace SpeechBuddyAI.Tests;

public sealed class UiAccessibilityGuardrailsTests
{
    private static readonly string[] InteractiveControlXamlFiles =
    {
        "MainPage.xaml",
        "Pages/HomePage.xaml",
        "Pages/PracticePage.xaml",
        "Pages/ProgressPage.xaml",
        "Pages/NotesPage.xaml",
        "Pages/SettingsPage.xaml"
    };

    [Fact]
    public void HighValueInteractiveControls_HaveSemanticDescriptions()
    {
        var missingDescriptions = new List<string>();

        foreach (var relativePath in InteractiveControlXamlFiles)
        {
            var document = LoadXaml(relativePath);
            var interactiveElements = document
                .Descendants()
                .Where(element => element.Name.LocalName is "Button" or "Picker" or "Slider");

            foreach (var element in interactiveElements)
            {
                var semanticDescription = element.Attribute("SemanticProperties.Description")?.Value;
                if (!string.IsNullOrWhiteSpace(semanticDescription))
                {
                    continue;
                }

                var displayName = element.Attribute("Text")?.Value
                    ?? element.Attribute("Title")?.Value
                    ?? element.Attribute("x:Name")?.Value
                    ?? element.Name.LocalName;

                missingDescriptions.Add($"{relativePath}::{element.Name.LocalName}::{displayName}");
            }
        }

        Assert.True(
            missingDescriptions.Count == 0,
            "Missing SemanticProperties.Description on: " + Environment.NewLine + string.Join(Environment.NewLine, missingDescriptions));
    }

    [Fact]
    public void ButtonStyle_EnforcesMinimumTouchTargetOf48()
    {
        var controls = LoadXaml("Resources/Styles/Controls.xaml");
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");

        var touchTargetValue = controls
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Double" && element.Attribute(xNamespace + "Key")?.Value == "TouchTargetMin")
            ?.Value;

        Assert.False(string.IsNullOrWhiteSpace(touchTargetValue));
        Assert.True(double.TryParse(touchTargetValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedTouchTarget));
        Assert.True(parsedTouchTarget >= 48d, $"Touch target minimum should be >= 48 but was {parsedTouchTarget}.");

        var buttonStyle = controls
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Style" && element.Attribute("TargetType")?.Value == "Button");

        Assert.NotNull(buttonStyle);

        var minHeightSetter = buttonStyle!
            .Descendants()
            .FirstOrDefault(element => element.Name.LocalName == "Setter" && element.Attribute("Property")?.Value == "MinimumHeightRequest");

        Assert.NotNull(minHeightSetter);

        var setterValue = minHeightSetter!.Attribute("Value")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(setterValue));

        var referencesTouchTarget = string.Equals(setterValue, "{StaticResource TouchTargetMin}", StringComparison.Ordinal);
        var numericValueIsValid = double.TryParse(setterValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedSetterValue) && parsedSetterValue >= 48d;

        Assert.True(
            referencesTouchTarget || numericValueIsValid,
            $"Button MinimumHeightRequest should reference TouchTargetMin or be >= 48. Value: {setterValue}");
    }

    [Fact]
    public void KeyColorRolePairs_MeetWcagContrastThreshold()
    {
        var colors = LoadColorDictionary();

        AssertContrastAtLeast(colors, "OnBackground", "Background", 4.5);
        AssertContrastAtLeast(colors, "OnPrimary", "Primary", 4.5);
        AssertContrastAtLeast(colors, "OnSecondary", "Secondary", 4.5);
    }

    private static Dictionary<string, string> LoadColorDictionary()
    {
        var document = LoadXaml("Resources/Styles/Colors.xaml");
        var xNamespace = XNamespace.Get("http://schemas.microsoft.com/winfx/2009/xaml");

        return document
            .Descendants()
            .Where(element => element.Name.LocalName == "Color")
            .Select(element => new
            {
                Key = element.Attribute(xNamespace + "Key")?.Value,
                Value = element.Value?.Trim()
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key) && !string.IsNullOrWhiteSpace(item.Value))
            .ToDictionary(item => item.Key!, item => item.Value!, StringComparer.Ordinal);
    }

    private static void AssertContrastAtLeast(IReadOnlyDictionary<string, string> colors, string foregroundKey, string backgroundKey, double minimumContrast)
    {
        Assert.True(colors.TryGetValue(foregroundKey, out var foregroundHex), $"Missing color key: {foregroundKey}");
        Assert.True(colors.TryGetValue(backgroundKey, out var backgroundHex), $"Missing color key: {backgroundKey}");

        var ratio = ComputeContrastRatio(foregroundHex!, backgroundHex!);
        Assert.True(
            ratio >= minimumContrast,
            $"Contrast ratio for {foregroundKey} on {backgroundKey} must be >= {minimumContrast:0.0}, actual {ratio:0.00}.");
    }

    private static double ComputeContrastRatio(string foregroundHex, string backgroundHex)
    {
        var foreground = ParseColorHex(foregroundHex);
        var background = ParseColorHex(backgroundHex);

        var foregroundLuminance = RelativeLuminance(foreground.r, foreground.g, foreground.b);
        var backgroundLuminance = RelativeLuminance(background.r, background.g, background.b);

        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static (byte r, byte g, byte b) ParseColorHex(string hex)
    {
        var normalized = hex.Trim();
        if (normalized.StartsWith("#", StringComparison.Ordinal))
        {
            normalized = normalized[1..];
        }

        if (normalized.Length != 6)
        {
            throw new InvalidOperationException($"Expected 6-digit RGB hex color but got '{hex}'.");
        }

        return (
            byte.Parse(normalized[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            byte.Parse(normalized.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static double RelativeLuminance(byte r, byte g, byte b)
    {
        static double Linearize(byte channel)
        {
            var c = channel / 255d;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var red = Linearize(r);
        var green = Linearize(g);
        var blue = Linearize(b);
        return 0.2126 * red + 0.7152 * green + 0.0722 * blue;
    }

    private static XDocument LoadXaml(string relativePath)
    {
        var rootPath = ResolveRepositoryRoot();
        var fullPath = Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return XDocument.Load(fullPath);
    }

    private static string ResolveRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && current is not null; i++)
        {
            var candidate = Path.Combine(current.FullName, "SpeechBuddyAI.csproj");
            if (File.Exists(candidate))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root from test output path.");
    }
}
