using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace SpeechBuddyAI.Tests;

public sealed class UiSnapshotVisualRegressionTests
{
    [Fact]
    public void CompactPhoneSnapshots_DoNotRegressAgainstBaseline()
    {
        var root = ResolveRepositoryRoot();
        var baselineDir = ResolveDirectory(root, "UI_SNAPSHOT_BASELINE_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "baseline", "compact-phone"));
        var currentDir = ResolveDirectory(root, "UI_SNAPSHOT_CURRENT_DIR", Path.Combine("tests", "SpeechBuddyAI.Tests", "UiSnapshots", "current", "compact-phone"));

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

            var delta = ComputeNormalizedPixelDelta(baselineImage, currentImage);
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

    private static double ComputeNormalizedPixelDelta(Image<Rgba32> baseline, Image<Rgba32> current)
    {
        var totalDelta = 0d;
        var pixelCount = baseline.Width * baseline.Height;

        for (var y = 0; y < baseline.Height; y++)
        {
            var baselineRow = baseline.DangerousGetPixelRowMemory(y).Span;
            var currentRow = current.DangerousGetPixelRowMemory(y).Span;

            for (var x = 0; x < baseline.Width; x++)
            {
                var b = baselineRow[x];
                var c = currentRow[x];

                var channelDelta = Math.Abs(b.R - c.R) + Math.Abs(b.G - c.G) + Math.Abs(b.B - c.B);
                totalDelta += channelDelta / (255d * 3d);
            }
        }

        return totalDelta / pixelCount;
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
}
