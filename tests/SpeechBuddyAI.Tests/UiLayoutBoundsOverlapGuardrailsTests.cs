using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace SpeechBuddyAI.Tests;

public sealed class UiLayoutBoundsOverlapGuardrailsTests
{
    private static readonly Regex BoundsPattern = new(@"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", RegexOptions.Compiled);

    [Fact]
    public void CompactPhoneLayoutBounds_DoNotContainSiblingOverlapRegressions()
    {
        var root = ResolveRepositoryRoot();
        var boundsDirectory = ResolveDirectory(root, "UI_LAYOUT_BOUNDS_CURRENT_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "current", "compact-phone", "layout-bounds"));

        if (!Directory.Exists(boundsDirectory))
        {
            return;
        }

        var xmlFiles = Directory.GetFiles(boundsDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        if (xmlFiles.Length == 0)
        {
            return;
        }

        var violations = new List<string>();

        foreach (var xmlPath in xmlFiles)
        {
            var doc = XDocument.Load(xmlPath);
            var nodes = doc
                .Descendants("node")
                .Select(node => new
                {
                    Bounds = ParseBounds(node.Attribute("bounds")?.Value),
                    Clickable = IsTrue(node.Attribute("clickable")?.Value),
                    Focusable = IsTrue(node.Attribute("focusable")?.Value),
                    Enabled = IsTrue(node.Attribute("enabled")?.Value),
                    ClassName = node.Attribute("class")?.Value ?? string.Empty,
                    Text = node.Attribute("text")?.Value ?? string.Empty,
                    ContentDescription = node.Attribute("content-desc")?.Value ?? string.Empty
                })
                .Where(x => x.Bounds is not null && x.Enabled && (x.Clickable || x.Focusable))
                .Select(x => new BoundsNode(x.Bounds!.Value, x.ClassName, x.Text, x.ContentDescription))
                .Where(node => node.Bounds.Width > 1 && node.Bounds.Height > 1)
                .ToList();

            for (var i = 0; i < nodes.Count; i++)
            {
                for (var j = i + 1; j < nodes.Count; j++)
                {
                    var a = nodes[i];
                    var b = nodes[j];

                    if (!Intersects(a.Bounds, b.Bounds))
                    {
                        continue;
                    }

                    if (Contains(a.Bounds, b.Bounds) || Contains(b.Bounds, a.Bounds))
                    {
                        continue;
                    }

                    var area = IntersectionArea(a.Bounds, b.Bounds);
                    if (area < 64)
                    {
                        continue;
                    }

                    var file = Path.GetFileName(xmlPath);
                    violations.Add($"{file}: overlap between '{Describe(a)}' and '{Describe(b)}' with area {area}");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Detected actionable overlap regressions:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static string Describe(BoundsNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ContentDescription))
        {
            return node.ContentDescription;
        }

        if (!string.IsNullOrWhiteSpace(node.Text))
        {
            return node.Text;
        }

        return node.ClassName;
    }

    private static bool IsTrue(string? value) => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    private static Bounds? ParseBounds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var match = BoundsPattern.Match(raw);
        if (!match.Success)
        {
            return null;
        }

        var left = int.Parse(match.Groups[1].Value);
        var top = int.Parse(match.Groups[2].Value);
        var right = int.Parse(match.Groups[3].Value);
        var bottom = int.Parse(match.Groups[4].Value);
        return new Bounds(left, top, right, bottom);
    }

    private static bool Intersects(Bounds a, Bounds b)
    {
        return a.Left < b.Right && a.Right > b.Left && a.Top < b.Bottom && a.Bottom > b.Top;
    }

    private static bool Contains(Bounds outer, Bounds inner)
    {
        return outer.Left <= inner.Left && outer.Top <= inner.Top && outer.Right >= inner.Right && outer.Bottom >= inner.Bottom;
    }

    private static int IntersectionArea(Bounds a, Bounds b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        var width = Math.Max(0, right - left);
        var height = Math.Max(0, bottom - top);
        return width * height;
    }

    private static string ResolveDirectory(string root, string envKey, string defaultRelativePath)
    {
        var env = Environment.GetEnvironmentVariable(envKey);
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env;
        }

        return Path.Combine(root, defaultRelativePath);
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

    private readonly record struct Bounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width => Math.Max(0, Right - Left);
        public int Height => Math.Max(0, Bottom - Top);
    }

    private sealed record BoundsNode(Bounds Bounds, string ClassName, string Text, string ContentDescription);
}
