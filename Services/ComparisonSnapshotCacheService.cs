using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public sealed class ComparisonSnapshotCacheService
{
    private readonly ComparisonExportBuilderService _comparisonExportBuilderService;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, SessionComparisonSnapshot> _cache = new(StringComparer.Ordinal);
    private readonly Queue<string> _cacheOrder = new();

    public ComparisonSnapshotCacheService(ComparisonExportBuilderService comparisonExportBuilderService)
    {
        _comparisonExportBuilderService = comparisonExportBuilderService ?? throw new ArgumentNullException(nameof(comparisonExportBuilderService));
    }

    public SessionComparisonSnapshot GetOrBuild(
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode,
        SessionComparisonSmoothingStrength smoothingStrength)
    {
        var safeEntries = entries ?? Array.Empty<ProgressEntry>();
        var cacheKey = BuildCacheKey(safeEntries, normalizationMode, smoothingStrength);

        lock (_syncRoot)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }
        }

        var snapshot = _comparisonExportBuilderService.Build(safeEntries, normalizationMode, smoothingStrength);

        lock (_syncRoot)
        {
            if (_cache.TryGetValue(cacheKey, out var cached))
            {
                return cached;
            }

            _cache[cacheKey] = snapshot;
            _cacheOrder.Enqueue(cacheKey);

            while (_cacheOrder.Count > 16)
            {
                var expired = _cacheOrder.Dequeue();
                _cache.Remove(expired);
            }

            return snapshot;
        }
    }

    private static string BuildCacheKey(
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode,
        SessionComparisonSmoothingStrength smoothingStrength)
    {
        var ordered = entries
            .OrderBy(entry => entry.Timestamp)
            .ThenBy(entry => entry.TargetSound, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hash = new HashCode();
        hash.Add((int)normalizationMode);
        hash.Add((int)smoothingStrength);
        hash.Add(ordered.Length);

        foreach (var entry in ordered)
        {
            hash.Add(entry.Timestamp.Ticks);
            hash.Add(entry.TargetSound, StringComparer.OrdinalIgnoreCase);
            hash.Add(entry.OverallScore);
            hash.Add(entry.ConfidenceScore);
            hash.Add(entry.ConfidenceBand, StringComparer.OrdinalIgnoreCase);
        }

        return hash.ToHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
