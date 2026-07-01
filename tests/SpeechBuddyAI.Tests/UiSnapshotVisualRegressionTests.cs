using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Text.Json;

namespace SpeechBuddyAI.Tests;

public sealed class UiSnapshotVisualRegressionTests
{
    [Fact]
    public void CompactPhoneSnapshots_DoNotRegressAgainstBaseline()
    {
        var root = ResolveRepositoryRoot();
        var baselineDir = ResolveDirectory(root, "UI_SNAPSHOT_BASELINE_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "baseline", "compact-phone"));
        var currentDir = ResolveDirectory(root, "UI_SNAPSHOT_CURRENT_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "current", "compact-phone"));
        var ignoreRegionsPath = ResolveFile(root, "UI_SNAPSHOT_IGNORE_REGIONS_FILE", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "ignore-regions.json"));
        var ignoreRegions = LoadIgnoreRegions(ignoreRegionsPath);

        if (!Directory.Exists(baselineDir) || !Directory.Exists(currentDir))
        {
            // Runtime screenshot capture pipeline is optional in local dev.
            return;
        }

        var baselineFiles = Directory.GetFiles(baselineDir, "*.png", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (baselineFiles.Count == 0)
        {
            return;
        }

        var violations = new List<string>();

        foreach (var fileName in baselineFiles)
        {
            var baselinePath = Path.Combine(baselineDir, fileName!);
            var currentPath = Path.Combine(currentDir, fileName!);

            if (!File.Exists(currentPath))
            {
                violations.Add($"Missing current snapshot: {fileName}");
                continue;
            }

            using var baselineImage = Image.Load<Rgba32>(baselinePath);
            using var currentImage = Image.Load<Rgba32>(currentPath);

            if (baselineImage.Width != currentImage.Width || baselineImage.Height != currentImage.Height)
            {
                violations.Add($"Image size mismatch for {fileName}: baseline {baselineImage.Width}x{baselineImage.Height}, current {currentImage.Width}x{currentImage.Height}");
                continue;
            }

            var regions = ResolveIgnoreRegions(ignoreRegions, fileName!);
            var delta = ComputeNormalizedPixelDelta(baselineImage, currentImage, regions);
            const double allowedDelta = 0.015; // 1.5% average normalized RGB delta
            if (delta > allowedDelta)
            {
                violations.Add($"Visual delta for {fileName} exceeded threshold: {delta:0.0000} > {allowedDelta:0.0000}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Visual regression violations found:" + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    private static double ComputeNormalizedPixelDelta(Image<Rgba32> baseline, Image<Rgba32> current, IReadOnlyList<IgnoreRegion> ignoreRegions)
    {
        var totalDelta = 0d;
        var pixelCount = 0;

        for (var y = 0; y < baseline.Height; y++)
        {
            for (var x = 0; x < baseline.Width; x++)
            {
                if (IsIgnored(ignoreRegions, x, y))
                {
                    continue;
                }

                var b = baseline[x, y];
                var c = current[x, y];

                var channelDelta = Math.Abs(b.R - c.R) + Math.Abs(b.G - c.G) + Math.Abs(b.B - c.B);
                totalDelta += channelDelta / (255d * 3d);
                pixelCount++;
            }
        }

        if (pixelCount == 0)
        {
            return 0d;
        }

        return totalDelta / pixelCount;
    }

    private static bool IsIgnored(IReadOnlyList<IgnoreRegion> regions, int x, int y)
    {
        for (var i = 0; i < regions.Count; i++)
        {
            var region = regions[i];
            if (x >= region.X && x < region.X + region.Width && y >= region.Y && y < region.Y + region.Height)
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<IgnoreRegion> ResolveIgnoreRegions(IReadOnlyDictionary<string, List<IgnoreRegion>> ignoreRegions, string fileName)
    {
        if (ignoreRegions.TryGetValue(fileName, out var byName))
        {
            return byName;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrWhiteSpace(stem) && ignoreRegions.TryGetValue(stem, out var byStem))
        {
            return byStem;
        }

        return Array.Empty<IgnoreRegion>();
    }

    private static IReadOnlyDictionary<string, List<IgnoreRegion>> LoadIgnoreRegions(string path)
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, List<IgnoreRegion>>(StringComparer.OrdinalIgnoreCase);
        }

        var json = File.ReadAllText(path);
        var parsed = JsonSerializer.Deserialize<Dictionary<string, List<IgnoreRegion>>>(json)
            ?? new Dictionary<string, List<IgnoreRegion>>(StringComparer.OrdinalIgnoreCase);

        return new Dictionary<string, List<IgnoreRegion>>(parsed, StringComparer.OrdinalIgnoreCase);
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

    private static string ResolveFile(string root, string envKey, string defaultRelativePath)
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

    public sealed record IgnoreRegion(int X, int Y, int Width, int Height);
}
