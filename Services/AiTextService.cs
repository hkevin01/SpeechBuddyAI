using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Confidence;

namespace SpeechBuddyAI.Services;

// ID: SVC-TEXT-001
// Purpose: Generates practice word lists and home assignment plans.
public class AiTextService
{
    private const int MaxFocusTargets = 3;
    private const int RecentWindow = 5;
    private const int ConfidenceVarianceWindow = 12;
    private static readonly string[] PositionOrder = ["initial", "medial", "final"];

    private readonly PhonemeWordBankService _wordBank;
    private readonly ConfidenceSettingsService _settingsService;
    private readonly AssignmentSnapshotService _assignmentSnapshotService;

    public AiTextService(
        PhonemeWordBankService wordBank,
        ConfidenceSettingsService settingsService,
        AssignmentSnapshotService assignmentSnapshotService)
    {
        _wordBank = wordBank ?? throw new ArgumentNullException(nameof(wordBank));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _assignmentSnapshotService = assignmentSnapshotService ?? throw new ArgumentNullException(nameof(assignmentSnapshotService));
    }

    // ID: SVC-TEXT-002
    // Purpose: Returns position-aware practice words for a given target sound.
    // Inputs: target ("r", "sh", "r:initial", etc.), null position falls back to all positions.
    public Task<string[]> GeneratePracticeWordsAsync(string target)
    {
        var normalizedTarget = (target ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedTarget))
            return Task.FromResult(_wordBank.GetWords("r", "initial"));

        try
        {
            // Support "target:position" shorthand from callers
            var parts = normalizedTarget.Split(':', 2);
            var phoneme = parts[0].Trim();
            var position = parts.Length > 1 ? parts[1].Trim() : null;

            return Task.FromResult(_wordBank.GetWords(phoneme, position));
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate practice words.", ex);
        }
    }

    // ID: SVC-TEXT-003
    // Purpose: Builds a formula-driven assignment plan from persisted attempt history.
    // Formula: priority = recency * (
    //   ws*severity + wi*instability + wd*decline + wf*frequency) * confidenceFactor
    // where severity = 1 - recent mean overall, instability = sqrt(recent variance),
    // decline = max(0, baseline mean - recent mean), and frequency favors repeated challenges.
    // confidenceFactor down-weights low-confidence sessions according to clinician penalty settings.
    public async Task<HomeAssignment> GenerateHomeAssignmentAsync(IReadOnlyList<ProgressEntry> history)
    {
        var sourceEntries = history ?? Array.Empty<ProgressEntry>();

        try
        {
            var settings = _settingsService.GetAssignmentPrioritySettings().Normalize();
            var confidenceVarianceGate = _settingsService.GetAssignmentConfidenceVarianceGate();
            var confidenceVariance = ComputeConfidenceVariance(sourceEntries);
            var candidates = BuildTargetCandidates(sourceEntries, settings)
                .Select(candidate => candidate with
                {
                    Priority = ComputePriority(candidate, settings)
                })
                .OrderByDescending(c => c.Priority)
                .Take(MaxFocusTargets)
                .ToArray();

            var latestSnapshot = await _assignmentSnapshotService.GetLatestSnapshotAsync();
            var suppressChange = ShouldSuppressAssignmentChange(confidenceVariance, confidenceVarianceGate, latestSnapshot);
            if (suppressChange)
            {
                candidates = ApplySuppressedTargets(candidates, latestSnapshot, settings);
            }

            var targets = candidates
                .Select(c => c.Target)
                .ToArray();

            if (targets.Length == 0)
            {
                return new HomeAssignment
                {
                    Title = "Home Practice Plan",
                    Rationale = "No weak patterns found yet. Continue with mixed articulation drills for consistency.",
                    FocusTargets = Array.Empty<string>(),
                    SuggestedWords = ["rabbit", "lamp", "sun"],
                    FocusTargetReasons = Array.Empty<AssignmentFocusTargetReason>()
                };
            }

            var suggestedWords = new List<string>();
            var reasons = new List<AssignmentFocusTargetReason>();
            foreach (var candidate in candidates)
            {
                var wordsForTarget = new List<string>();
                foreach (var position in candidate.PositionSequence)
                {
                    var positionedWords = await GeneratePracticeWordsAsync($"{candidate.Target}:{position}");
                    var nextWord = positionedWords.FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(nextWord))
                    {
                        wordsForTarget.Add(nextWord);
                    }

                    if (wordsForTarget.Count >= 2)
                    {
                        break;
                    }
                }

                if (wordsForTarget.Count < 2)
                {
                    var fallback = await GeneratePracticeWordsAsync(candidate.Target);
                    wordsForTarget.AddRange(fallback.Take(2 - wordsForTarget.Count));
                }

                suggestedWords.AddRange(wordsForTarget);
                reasons.Add(new AssignmentFocusTargetReason
                {
                    TargetSound = candidate.Target,
                    PriorityScore = candidate.Priority,
                    SeverityScore = candidate.Severity,
                    InstabilityScore = candidate.Instability,
                    DeclineScore = candidate.Decline,
                    FrequencyScore = candidate.Frequency,
                    ConfidenceFactor = candidate.ConfidenceFactor,
                    ConfidenceVariance = confidenceVariance,
                    AssignmentChangeSuppressed = suppressChange,
                    InitialAverageScore = candidate.PositionAverages.TryGetValue("initial", out var initialAvg) ? initialAvg : 0.0,
                    MedialAverageScore = candidate.PositionAverages.TryGetValue("medial", out var medialAvg) ? medialAvg : 0.0,
                    FinalAverageScore = candidate.PositionAverages.TryGetValue("final", out var finalAvg) ? finalAvg : 0.0,
                    InitialAttemptScores = candidate.PositionSamples.TryGetValue("initial", out var initialScores) ? initialScores : Array.Empty<double>(),
                    MedialAttemptScores = candidate.PositionSamples.TryGetValue("medial", out var medialScores) ? medialScores : Array.Empty<double>(),
                    FinalAttemptScores = candidate.PositionSamples.TryGetValue("final", out var finalScores) ? finalScores : Array.Empty<double>(),
                    PositionSequence = string.Join(" -> ", candidate.PositionSequence),
                    PositionDeltaSummary = BuildPositionDeltaSummary(candidate)
                });
            }

            var commonPattern = sourceEntries
                .Where(e => !string.IsNullOrWhiteSpace(e.ErrorPattern))
                .GroupBy(e => e.ErrorPattern)
                .OrderByDescending(g => g.Count())
                .Select(g => g.Key)
                .FirstOrDefault() ?? "mixed_patterns";

            var rationale = BuildRationale(candidates, commonPattern, settings, suppressChange, confidenceVariance, confidenceVarianceGate);

            return new HomeAssignment
            {
                Title = "Home Practice Plan",
                Rationale = rationale,
                FocusTargets = targets,
                SuggestedWords = suggestedWords.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                FocusTargetReasons = reasons
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate a home assignment from weak-pattern history.", ex);
        }
    }

    private static IReadOnlyList<TargetAssignmentCandidate> BuildTargetCandidates(
        IReadOnlyList<ProgressEntry> entries,
        AssignmentPrioritySettings settings)
    {
        var now = DateTime.UtcNow;

        return entries
            .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
            .GroupBy(ResolveBaseTarget, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildTargetCandidate(group.Key, group.ToArray(), now, settings))
            .OrderByDescending(candidate => candidate.Priority)
            .ToArray();
    }

    private static TargetAssignmentCandidate BuildTargetCandidate(
        string target,
        IReadOnlyList<ProgressEntry> entries,
        DateTime now,
        AssignmentPrioritySettings settings)
    {
        var ordered = entries.OrderBy(e => e.Timestamp).ToArray();
        var recent = ordered.TakeLast(Math.Min(RecentWindow, ordered.Length)).ToArray();
        var baseline = ordered.Take(Math.Min(RecentWindow, ordered.Length)).ToArray();

        var recentMean = ComputeConfidenceWeightedAverage(recent, settings.ConfidencePenaltyStrength);
        var baselineMean = baseline.Length > 0
            ? ComputeConfidenceWeightedAverage(baseline, settings.ConfidencePenaltyStrength)
            : recentMean;
        var severity = Clamp(1.0 - recentMean);
        var instability = Math.Sqrt(ComputeVariance(recent.Select(e => Clamp(e.OverallScore)).ToArray()));
        var decline = Clamp(Math.Max(0.0, baselineMean - recentMean));
        var frequency = Clamp(Math.Log(entries.Count + 1, 2) / 4.0);
        var daysSinceLast = Math.Max(0.0, (now - ordered[^1].Timestamp).TotalDays);
        var recency = Math.Exp(-daysSinceLast / 14.0);
        var averageConfidence = Clamp(ordered.Average(entry => NormalizeConfidence(entry.ConfidenceScore)));
        var positionSamples = BuildPositionSamples(target, entries);
        var positionAverages = positionSamples.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Length == 0 ? 0.0 : kvp.Value.Average(),
            StringComparer.OrdinalIgnoreCase);
        var positionDeltas = BuildPositionDeltas(target, entries, positionSamples);
        var positionSequence = PositionOrder
            .OrderBy(position => positionDeltas.TryGetValue(position, out var delta) ? delta : 0.0)
            .ThenBy(position => position)
            .ToArray();

        var basePriority = (0.45 * severity) + (0.20 * instability) + (0.20 * decline) + (0.15 * frequency);
        var priority = Clamp(basePriority * recency);

        return new TargetAssignmentCandidate(
            target,
            priority,
            severity,
            instability,
            decline,
            frequency,
            averageConfidence,
            recency,
            entries.Count,
            ordered[^1].Timestamp,
            positionSequence,
                positionDeltas,
                positionAverages,
                positionSamples);
    }

    private static string BuildRationale(
        IReadOnlyList<TargetAssignmentCandidate> candidates,
        string commonPattern,
        AssignmentPrioritySettings settings,
        bool suppressChange,
        double confidenceVariance,
        double confidenceVarianceGate)
    {
        if (candidates.Count == 0)
        {
            return "No weak patterns found yet. Continue with mixed articulation drills for consistency.";
        }

        var top = candidates[0];
        var targetList = string.Join(", ", candidates.Select(c => c.Target));
         var gatingText = suppressChange
             ? $" Assignment updates were held because confidence variance {confidenceVariance:0.000} exceeded the gate {confidenceVarianceGate:0.000}."
             : string.Empty;

         return $"Focus on {targetList} based on weighted priority from recent severity, instability, trend decline, and repetition frequency. " +
               $"Weights are severity {settings.SeverityWeight:0.00}, instability {settings.InstabilityWeight:0.00}, decline {settings.DeclineWeight:0.00}, frequency {settings.FrequencyWeight:0.00}, with confidence penalty strength {settings.ConfidencePenaltyStrength:0.00}. " +
               $"Highest-priority target is {top.Target} (priority {top.Priority:0.00}, severity {top.Severity:0.00}, instability {top.Instability:0.00}, confidence factor {top.ConfidenceFactor:0.00}). " +
             $"Most frequent challenge pattern was '{commonPattern}', so drills should emphasize slow, repeatable production before speed." +
             gatingText;
    }

    private static double ComputePriority(TargetAssignmentCandidate candidate, AssignmentPrioritySettings settings)
    {
        var confidenceFactor = ((1.0 - settings.ConfidencePenaltyStrength) +
                                (settings.ConfidencePenaltyStrength * candidate.AverageConfidence));
        var weighted = (settings.SeverityWeight * candidate.Severity) +
                       (settings.InstabilityWeight * candidate.Instability) +
                       (settings.DeclineWeight * candidate.Decline) +
                       (settings.FrequencyWeight * candidate.Frequency);
        return Clamp(weighted * candidate.Recency * confidenceFactor);
    }

    private static Dictionary<string, double[]> BuildPositionSamples(string target, IReadOnlyList<ProgressEntry> entries)
    {
        var samples = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in PositionOrder)
        {
            samples[position] = entries
                .Where(entry => IsPositionMatch(target, position, entry))
                .OrderBy(entry => entry.Timestamp)
                .Select(entry => Clamp(entry.OverallScore))
                .ToArray();
        }

        return samples;
    }

    private static Dictionary<string, double> BuildPositionDeltas(
        string target,
        IReadOnlyList<ProgressEntry> entries,
        IReadOnlyDictionary<string, double[]> positionSamples)
    {
        var deltas = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in PositionOrder)
        {
            var scoped = positionSamples.TryGetValue(position, out var values)
                ? values
                : Array.Empty<double>();

            if (scoped.Length < 2)
            {
                deltas[position] = 0.0;
                continue;
            }

            var window = Math.Min(RecentWindow, scoped.Length);
            var baseline = scoped.Take(window).Average();
            var recent = scoped.TakeLast(window).Average();
            deltas[position] = recent - baseline;
        }

        return deltas;
    }

    private static bool IsPositionMatch(string target, string position, ProgressEntry entry)
    {
        var baseTarget = ResolveBaseTarget(entry);
        var positionTag = ResolvePositionTag(entry);
        return string.Equals(baseTarget, target, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(positionTag, position, StringComparison.OrdinalIgnoreCase);
    }

    private static (string BaseTarget, string Position) ParseBaseTarget(string? targetSound)
    {
        var normalized = (targetSound ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return (string.Empty, string.Empty);
        }

        var parts = normalized.Split(':', 2);
        var baseTarget = parts[0].Trim();
        var position = parts.Length > 1 ? parts[1].Trim() : string.Empty;
        return (baseTarget, position);
    }

    private static double ComputeConfidenceWeightedAverage(IReadOnlyList<ProgressEntry> entries, double penaltyStrength)
    {
        if (entries.Count == 0)
        {
            return 0.0;
        }

        var weighted = entries
            .Select(entry =>
            {
                var confidence = NormalizeConfidence(entry.ConfidenceScore);
                var confidenceFactor = (1.0 - penaltyStrength) + (penaltyStrength * confidence);
                return new
                {
                    Weight = Math.Max(0.05, confidenceFactor),
                    Score = Clamp(entry.OverallScore)
                };
            })
            .ToArray();

        var denominator = weighted.Sum(item => item.Weight);
        if (denominator <= 0)
        {
            return entries.Average(entry => Clamp(entry.OverallScore));
        }

        return weighted.Sum(item => item.Score * item.Weight) / denominator;
    }

    private static double NormalizeConfidence(double confidence)
    {
        if (confidence <= 0)
        {
            return 0.5;
        }

        return Clamp(confidence);
    }

    private static double ComputeConfidenceVariance(IReadOnlyList<ProgressEntry> entries)
    {
        var sample = entries
            .OrderByDescending(entry => entry.Timestamp)
            .Take(ConfidenceVarianceWindow)
            .Select(entry => NormalizeConfidence(entry.ConfidenceScore))
            .ToArray();

        if (sample.Length < 2)
        {
            return 0.0;
        }

        return ComputeVariance(sample);
    }

    private static bool ShouldSuppressAssignmentChange(
        double confidenceVariance,
        double confidenceVarianceGate,
        AssignmentSnapshot? latestSnapshot)
    {
        return latestSnapshot is not null &&
               confidenceVariance > Math.Max(0.0, confidenceVarianceGate);
    }

    private static TargetAssignmentCandidate[] ApplySuppressedTargets(
        IReadOnlyList<TargetAssignmentCandidate> candidates,
        AssignmentSnapshot? latestSnapshot,
        AssignmentPrioritySettings settings)
    {
        if (latestSnapshot is null)
        {
            return candidates.ToArray();
        }

        var targetOrder = ParseCsv(latestSnapshot.FocusTargetsCsv);
        if (targetOrder.Count == 0)
        {
            return candidates.ToArray();
        }

        var candidateByTarget = candidates.ToDictionary(candidate => candidate.Target, StringComparer.OrdinalIgnoreCase);
        var selected = new List<TargetAssignmentCandidate>();
        foreach (var target in targetOrder)
        {
            if (!candidateByTarget.TryGetValue(target, out var candidate))
            {
                continue;
            }

            selected.Add(candidate with { Priority = ComputePriority(candidate, settings) + 0.0001 });
        }

        if (selected.Count == 0)
        {
            return candidates.ToArray();
        }

        return selected
            .Take(MaxFocusTargets)
            .ToArray();
    }

    private static IReadOnlyList<string> ParseCsv(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return Array.Empty<string>();
        }

        return csv
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string ResolveBaseTarget(ProgressEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.BaseTargetSound))
        {
            return entry.BaseTargetSound.Trim().ToLowerInvariant();
        }

        return ParseBaseTarget(entry.TargetSound).BaseTarget;
    }

    private static string ResolvePositionTag(ProgressEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.PositionTag))
        {
            return entry.PositionTag.Trim().ToLowerInvariant();
        }

        return ParseBaseTarget(entry.TargetSound).Position;
    }

    private static string BuildPositionDeltaSummary(TargetAssignmentCandidate candidate)
    {
        return string.Join(
            " | ",
            PositionOrder.Select(position =>
            {
                var delta = candidate.PositionDeltas.TryGetValue(position, out var value) ? value : 0.0;
                return $"{position} {delta:+0.00;-0.00;0.00}";
            }));
    }

    private static double ComputeVariance(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = values.Average();
        return values.Average(value => Math.Pow(value - mean, 2));
    }

    private static double Clamp(double value)
    {
        return Math.Max(0.0, Math.Min(1.0, value));
    }

    private sealed record TargetAssignmentCandidate(
        string Target,
        double Priority,
        double Severity,
        double Instability,
        double Decline,
        double Frequency,
        double AverageConfidence,
        double Recency,
        int Attempts,
        DateTime LastAttemptAt,
        IReadOnlyList<string> PositionSequence,
        IReadOnlyDictionary<string, double> PositionDeltas,
        IReadOnlyDictionary<string, double> PositionAverages,
        IReadOnlyDictionary<string, double[]> PositionSamples)
    {
        public double ConfidenceFactor => AverageConfidence;
    }
}
