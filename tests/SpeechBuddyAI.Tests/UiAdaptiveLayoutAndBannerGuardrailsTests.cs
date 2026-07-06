using System.Xml.Linq;

namespace SpeechBuddyAI.Tests;

public sealed class UiAdaptiveLayoutAndBannerGuardrailsTests
{
    [Fact]
    public void HighRiskPages_DefineNamedAdaptiveContainersAndStatusBanners()
    {
        XNamespace xNamespace = "http://schemas.microsoft.com/winfx/2009/xaml";
        var requirements = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["Pages/HomePage.xaml"] = ["OverviewMetricsGrid", "AssignmentStatusBanner"],
            ["Pages/ProgressPage.xaml"] = ["SessionComparisonGrid"],
            ["Pages/NotesPage.xaml"] = ["ExportFormatRow", "ExportDateRangeRow", "AssignmentTargetRow", "NotesActionGrid", "ReportActionGrid", "NotesStatusBanner"],
            ["Pages/SettingsPage.xaml"] = ["SettingsStatusBanner"],
            ["Pages/PracticePage.xaml"] = ["PracticeStatusBanner"]
        };

        var violations = new List<string>();
        foreach (var pair in requirements)
        {
            var document = LoadXaml(pair.Key);
            foreach (var expectedName in pair.Value)
            {
                var found = document.Descendants().Any(node => node.Attribute(xNamespace + "Name")?.Value == expectedName);
                if (!found)
                {
                    violations.Add($"{pair.Key} missing named element {expectedName}");
                }
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
    }

    [Fact]
    public void HighRiskPages_AvoidHardcodedHexColorsInInteractiveFramesAndCharts()
    {
        var files = new[]
        {
            "Pages/HomePage.xaml",
            "Pages/ProgressPage.xaml",
            "Pages/NotesPage.xaml"
        };

        var violations = new List<string>();
        foreach (var file in files)
        {
            var text = File.ReadAllText(Path.Combine(ResolveRepositoryRoot(), file.Replace('/', Path.DirectorySeparatorChar)));
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var referencesColorProperty =
                    line.Contains("Color=", StringComparison.Ordinal) ||
                    line.Contains("BackgroundColor=", StringComparison.Ordinal) ||
                    line.Contains("BorderColor=", StringComparison.Ordinal) ||
                    line.Contains("TextColor=", StringComparison.Ordinal);

                if (!referencesColorProperty || !line.Contains('#', StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add($"{file}:{i + 1} contains hardcoded color: {line.Trim()}");
            }
        }

        Assert.True(violations.Count == 0, string.Join(Environment.NewLine, violations));
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
