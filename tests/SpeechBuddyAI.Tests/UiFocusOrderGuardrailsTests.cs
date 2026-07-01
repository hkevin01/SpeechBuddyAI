using System.Xml.Linq;

namespace SpeechBuddyAI.Tests;

public sealed class UiFocusOrderGuardrailsTests
{
    private static readonly string[] ScreenFiles =
    {
        "MainPage.xaml",
        "Pages/HomePage.xaml",
        "Pages/PracticePage.xaml",
        "Pages/ProgressPage.xaml",
        "Pages/NotesPage.xaml",
        "Pages/SettingsPage.xaml"
    };

    [Fact]
    public void InteractiveControls_HaveTabIndexAndAscendingOrderPerPage()
    {
        var violations = new List<string>();

        foreach (var relativePath in ScreenFiles)
        {
            var page = LoadXaml(relativePath);
            var controls = page
                .Descendants()
                .Where(element => element.Name.LocalName is "Button" or "Picker" or "Slider" or "Entry" or "Editor" or "DatePicker")
                .ToList();

            if (controls.Count == 0)
            {
                continue;
            }

            var lastTabIndex = -1;
            foreach (var control in controls)
            {
                var tabIndexText = control.Attribute("TabIndex")?.Value;
                if (!int.TryParse(tabIndexText, out var tabIndex))
                {
                    violations.Add($"{relativePath}::{control.Name.LocalName} missing TabIndex");
                    continue;
                }

                if (tabIndex < lastTabIndex)
                {
                    var name = control.Attribute("x:Name")?.Value ?? control.Attribute("Text")?.Value ?? control.Name.LocalName;
                    violations.Add($"{relativePath}::{name} has non-ascending TabIndex {tabIndex} after {lastTabIndex}");
                }

                lastTabIndex = tabIndex;
            }
        }

        Assert.True(
            violations.Count == 0,
            "Focus-order violations found:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
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
