using System.Text.RegularExpressions;
using System.Xml.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SpeechBuddyAI.Tests;

public sealed class UiLayoutBoundsOverlapGuardrailsTests
{
    private static readonly Regex BoundsPattern = new(@"\[(\d+),(\d+)\]\[(\d+),(\d+)\]", RegexOptions.Compiled);

    [Fact]
    public void CompactPhoneLayoutBounds_DoNotContainSiblingOverlapRegressions()
    {
        var root = ResolveRepositoryRoot();
        var boundsDirectory = ResolveDirectory(root, "UI_LAYOUT_BOUNDS_CURRENT_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "current", "compact-phone", "layout-bounds"));
        var currentSnapshotsDirectory = ResolveDirectory(root, "UI_SNAPSHOT_CURRENT_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "current", "compact-phone"));
        var debugDirectory = ResolveDirectory(root, "UI_OVERLAP_DEBUG_DIR", Path.Combine(currentSnapshotsDirectory, "debug-overlap"));

        if (!Directory.Exists(boundsDirectory))
        {
            return;
        }

        Directory.CreateDirectory(debugDirectory);
        foreach (var file in Directory.GetFiles(debugDirectory, "*", SearchOption.TopDirectoryOnly))
        {
            File.Delete(file);
        }

        var xmlFiles = Directory.GetFiles(boundsDirectory, "*.xml", SearchOption.TopDirectoryOnly);
        if (xmlFiles.Length == 0)
        {
            return;
        }

        var violations = new List<string>();
        var fileDebugViolations = new Dictionary<string, List<OverlapViolation>>(StringComparer.OrdinalIgnoreCase);

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
                    if (!fileDebugViolations.TryGetValue(xmlPath, out var list))
                    {
                        list = new List<OverlapViolation>();
                        fileDebugViolations[xmlPath] = list;
                    }

                    list.Add(new OverlapViolation(a, b, area));
                }
            }
        }

        if (violations.Count > 0)
        {
            WriteDebugArtifacts(fileDebugViolations, currentSnapshotsDirectory, debugDirectory, violations);
        }

        Assert.True(
            violations.Count == 0,
            "Detected actionable overlap regressions:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static void WriteDebugArtifacts(
        IReadOnlyDictionary<string, List<OverlapViolation>> fileDebugViolations,
        string currentSnapshotsDirectory,
        string debugDirectory,
        IReadOnlyList<string> violations)
    {
        foreach (var pair in fileDebugViolations)
        {
            var xmlPath = pair.Key;
            var stem = Path.GetFileNameWithoutExtension(xmlPath);
            if (string.IsNullOrWhiteSpace(stem))
            {
                continue;
            }

            var screenshotPath = Path.Combine(currentSnapshotsDirectory, stem + ".png");
            if (!File.Exists(screenshotPath))
            {
                continue;
            }

            var debugImagePath = Path.Combine(debugDirectory, stem + "-overlap-debug.png");
            using var image = Image.Load<Rgba32>(screenshotPath);

            foreach (var overlap in pair.Value)
            {
                DrawBounds(image, overlap.A.Bounds, new Rgba32(220, 0, 0, 255));
                DrawBounds(image, overlap.B.Bounds, new Rgba32(0, 170, 255, 255));
                var intersection = Intersect(overlap.A.Bounds, overlap.B.Bounds);
                if (intersection is not null)
                {
                    FillBounds(image, intersection.Value, new Rgba32(255, 0, 0, 80));
                }
            }

            image.Save(debugImagePath);
        }

        File.WriteAllLines(Path.Combine(debugDirectory, "overlap-violations.txt"), violations);
    }

    private static void DrawBounds(Image<Rgba32> image, Bounds bounds, Rgba32 color)
    {
        var left = Math.Clamp(bounds.Left, 0, image.Width - 1);
        var right = Math.Clamp(bounds.Right - 1, 0, image.Width - 1);
        var top = Math.Clamp(bounds.Top, 0, image.Height - 1);
        var bottom = Math.Clamp(bounds.Bottom - 1, 0, image.Height - 1);

        for (var x = left; x <= right; x++)
        {
            image[x, top] = color;
            image[x, bottom] = color;
        }

        for (var y = top; y <= bottom; y++)
        {
            image[left, y] = color;
            image[right, y] = color;
        }
    }

    private static void FillBounds(Image<Rgba32> image, Bounds bounds, Rgba32 color)
    {
        var left = Math.Clamp(bounds.Left, 0, image.Width - 1);
        var right = Math.Clamp(bounds.Right - 1, 0, image.Width - 1);
        var top = Math.Clamp(bounds.Top, 0, image.Height - 1);
        var bottom = Math.Clamp(bounds.Bottom - 1, 0, image.Height - 1);

        for (var y = top; y <= bottom; y++)
        {
            var row = image.DangerousGetPixelRowMemory(y).Span;
            for (var x = left; x <= right; x++)
            {
                row[x] = Blend(row[x], color);
            }
        }
    }

    private static Rgba32 Blend(Rgba32 background, Rgba32 overlay)
    {
        var alpha = overlay.A / 255f;
        var inverse = 1f - alpha;
        return new Rgba32(
            (byte)Math.Clamp((background.R * inverse) + (overlay.R * alpha), 0, 255),
            (byte)Math.Clamp((background.G * inverse) + (overlay.G * alpha), 0, 255),
            (byte)Math.Clamp((background.B * inverse) + (overlay.B * alpha), 0, 255),
            255);
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

    private static Bounds? Intersect(Bounds a, Bounds b)
    {
        var left = Math.Max(a.Left, b.Left);
        var top = Math.Max(a.Top, b.Top);
        var right = Math.Min(a.Right, b.Right);
        var bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= left || bottom <= top)
        {
            return null;
        }

        return new Bounds(left, top, right, bottom);
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

    private sealed record OverlapViolation(BoundsNode A, BoundsNode B, int Area);
}
