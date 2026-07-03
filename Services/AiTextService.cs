using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services.Confidence;
using System.Text.Json;

namespace SpeechBuddyAI.Services;

// ID: SVC-TEXT-001
// Purpose: Generates practice word lists and home assignment plans.
public class AiTextService
{
    public const string ScoringFormulaVersion = "assign-v3.0-adaptive-calibrated";

    private const int MaxFocusTargets = 3;
    private const int RecentWindow = 5;
    private const int ConfidenceVarianceWindow = 12;
    private const int MinimumPositionSamples = 3;
    private const double EvidenceHalfLifeAttempts = 6.0;
    private const double EvidenceFloor = 0.35;
    private const double DeclineWindowDays = 7.0;
    private const int MinimumAdaptiveWindow = 3;
    private const int MaximumAdaptiveWindow = 12;
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
    // decline = max(0, negative confidence-weighted trend slope over recent days),
    // frequency favors repeated challenges, and evidence weighting tempers sparse samples.
    // confidenceFactor down-weights low-confidence sessions according to clinician penalty settings.
    public async Task<HomeAssignment> GenerateHomeAssignmentAsync(IReadOnlyList<ProgressEntry> history)
    {
        var sourceEntries = history ?? Array.Empty<ProgressEntry>();

        try
        {
            var settings = _settingsService.GetAssignmentPrioritySettings().Normalize();
            var confidenceVarianceGate = _settingsService.GetAssignmentConfidenceVarianceGate();
            var suppressionBehavior = _settingsService.GetAssignmentSuppressionBehavior();
            var ciMinSamples = _settingsService.GetAssignmentConfidenceIntervalMinSamples();
            var uncertaintyBudgetCap = _settingsService.GetAssignmentUncertaintyBudgetCap();
            var confidenceVariance = ComputeConfidenceVariance(sourceEntries);
            var exceedsVarianceGate = confidenceVariance > Math.Max(0.0, confidenceVarianceGate);
            var candidates = BuildTargetCandidates(sourceEntries, settings, ciMinSamples)
                .Select(candidate => candidate with
                {
                    Priority = ComputePriority(candidate, settings)
                })
                .OrderByDescending(c => c.Priority)
                .Take(MaxFocusTargets)
                .ToArray();
            var hasLowReliabilityRisk = candidates.Any(candidate => candidate.ReliabilityScore < 0.40);
            var shouldSuppressForReliability = candidates.Take(MaxFocusTargets).Any(candidate => candidate.ReliabilityScore < 0.35);
            var uncertaintyBudgetScore = ComputeAssignmentUncertaintyBudget(candidates);
            var reviewRequired = uncertaintyBudgetScore > uncertaintyBudgetCap;
            var uncertaintyBudgetSummary = reviewRequired
                ? $"Assignment flagged for clinician review: uncertainty budget {uncertaintyBudgetScore:0.000} exceeded cap {uncertaintyBudgetCap:0.000}."
                : $"Uncertainty budget {uncertaintyBudgetScore:0.000} is within cap {uncertaintyBudgetCap:0.000}.";

            var latestSnapshot = await _assignmentSnapshotService.GetLatestSnapshotAsync();
            var suppressChange = false;
            if (exceedsVarianceGate || shouldSuppressForReliability)
            {
                switch (suppressionBehavior)
                {
                    case AssignmentSuppressionBehavior.HardFreeze:
                        suppressChange = true;
                        candidates = ApplyHardFreezeTargets(candidates, latestSnapshot, settings);
                        break;
                    case AssignmentSuppressionBehavior.PartialUpdate:
                        suppressChange = true;
                        candidates = ApplyPartialUpdateTargets(candidates, latestSnapshot, settings);
                        break;
                    case AssignmentSuppressionBehavior.WarningOnly:
                    default:
                        suppressChange = false;
                        break;
                }
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
                    ScoringFormulaVersion = ScoringFormulaVersion,
                    FocusTargets = Array.Empty<string>(),
                    SuggestedWords = ["rabbit", "lamp", "sun"],
                    FocusTargetReasons = Array.Empty<AssignmentFocusTargetReason>(),
                    ReviewRequired = false,
                    UncertaintyBudgetScore = 0.0,
                    UncertaintyBudgetCap = uncertaintyBudgetCap,
                    UncertaintyBudgetSummary = "Uncertainty budget not computed because no focus targets were selected."
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
                    PositionWeightedDeclineScore = candidate.PositionWeightedDecline,
                    FrequencyNormalizationFactor = candidate.FrequencyNormalizationFactor,
                    ConfidenceFactor = candidate.ConfidenceFactor,
                    EvidenceStrength = candidate.EvidenceStrength,
                    ReliabilityScore = candidate.ReliabilityScore,
                    ReliabilitySampleDepthScore = candidate.ReliabilitySampleDepthScore,
                    ReliabilityVarianceScore = candidate.ReliabilityVarianceScore,
                    ReliabilityTrendStabilityScore = candidate.ReliabilityTrendStabilityScore,
                    CalibrationQualityScore = candidate.CalibrationQualityScore,
                    CalibrationConfidenceAdjustment = candidate.CalibrationConfidenceAdjustment,
                    ConfidenceVariance = confidenceVariance,
                    AssignmentChangeSuppressed = suppressChange,
                    OverallScoreMean = candidate.OverallScoreMean,
                    OverallScoreCiLower = candidate.CiSuppressed ? 0.0 : candidate.OverallScoreCiLower,
                    OverallScoreCiUpper = candidate.CiSuppressed ? 0.0 : candidate.OverallScoreCiUpper,
                    ConfidenceIntervalSuppressed = candidate.CiSuppressed,
                    ConfidenceIntervalMinSamples = ciMinSamples,
                    InstabilityWindowSize = candidate.InstabilityWindowSize,
                    DeclineWindowSize = candidate.DeclineWindowSize,
                    ScoringFormulaVersion = ScoringFormulaVersion,
                    InitialAverageScore = candidate.PositionAverages.TryGetValue("initial", out var initialAvg) ? initialAvg : 0.0,
                    MedialAverageScore = candidate.PositionAverages.TryGetValue("medial", out var medialAvg) ? medialAvg : 0.0,
                    FinalAverageScore = candidate.PositionAverages.TryGetValue("final", out var finalAvg) ? finalAvg : 0.0,
                    PositionSequenceSampleGateMet = candidate.PositionSequenceGateMet,
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

            var rationale = BuildRationale(candidates, commonPattern, settings, suppressChange, confidenceVariance, confidenceVarianceGate, suppressionBehavior, exceedsVarianceGate, hasLowReliabilityRisk);
            if (reviewRequired)
            {
                rationale += " Review is required because low-support evidence exceeded the assignment uncertainty budget cap.";
            }

            return new HomeAssignment
            {
                Title = "Home Practice Plan",
                Rationale = rationale,
                ScoringFormulaVersion = ScoringFormulaVersion,
                FocusTargets = targets,
                SuggestedWords = suggestedWords.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                FocusTargetReasons = reasons,
                ReviewRequired = reviewRequired,
                UncertaintyBudgetScore = uncertaintyBudgetScore,
                UncertaintyBudgetCap = uncertaintyBudgetCap,
                UncertaintyBudgetSummary = uncertaintyBudgetSummary
            };
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate a home assignment from weak-pattern history.", ex);
        }
    }

    private static IReadOnlyList<TargetAssignmentCandidate> BuildTargetCandidates(
        IReadOnlyList<ProgressEntry> entries,
        AssignmentPrioritySettings settings,
        int ciMinSamples)
    {
        var now = DateTime.UtcNow;
        var positionImportanceWeights = BuildPositionImportanceWeights(settings);

        var baseCandidates = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
            .GroupBy(ResolveBaseTarget, StringComparer.OrdinalIgnoreCase)
            .Select(group => BuildTargetCandidate(group.Key, group.ToArray(), now, settings, ciMinSamples, positionImportanceWeights))
            .ToArray();

        if (baseCandidates.Length == 0)
        {
            return Array.Empty<TargetAssignmentCandidate>();
        }

        var maxAttempts = Math.Max(1, baseCandidates.Max(candidate => candidate.Attempts));
        var averageAttempts = baseCandidates.Average(candidate => candidate.Attempts);

        return baseCandidates
            .Select(candidate =>
            {
                var normalizedFrequency = Clamp((double)candidate.Attempts / maxAttempts);
                var frequencyNormalizationFactor = Clamp(Math.Sqrt((averageAttempts + 1.0) / (candidate.Attempts + 1.0)));
                var priority = ComputePriority(candidate with
                {
                    Frequency = normalizedFrequency,
                    FrequencyNormalizationFactor = frequencyNormalizationFactor
                }, settings);

                return candidate with
                {
                    Frequency = normalizedFrequency,
                    FrequencyNormalizationFactor = frequencyNormalizationFactor,
                    Priority = priority
                };
            })
            .OrderByDescending(candidate => candidate.Priority)
            .ToArray();
    }

    private static TargetAssignmentCandidate BuildTargetCandidate(
        string target,
        IReadOnlyList<ProgressEntry> entries,
        DateTime now,
        AssignmentPrioritySettings settings,
        int ciMinSamples,
        IReadOnlyDictionary<string, double> positionImportanceWeights)
    {
        var ordered = entries.OrderBy(e => e.Timestamp).ToArray();
        var instabilityWindow = ResolveAdaptiveWindow(ordered.Length);
        var declineWindow = ResolveAdaptiveWindow(ordered.Length + 2);
        var recent = ordered.TakeLast(Math.Min(instabilityWindow, ordered.Length)).ToArray();
        var declineScoped = ordered.TakeLast(Math.Min(declineWindow, ordered.Length)).ToArray();

        var recentMean = ComputeConfidenceWeightedAverage(recent, settings.ConfidencePenaltyStrength);
        var severity = Clamp(1.0 - recentMean);
        var instability = Math.Sqrt(ComputeVariance(recent.Select(e => Clamp(e.OverallScore)).ToArray()));
        var overallDecline = Clamp(Math.Max(0.0, -ComputeConfidenceWeightedTrendSlope(declineScoped, settings.ConfidencePenaltyStrength) * DeclineWindowDays));
        var positionTrendSlopes = BuildPositionTrendSlopes(target, entries);
        var positionWeightedDecline = ComputePositionWeightedDecline(positionTrendSlopes, positionImportanceWeights);
        var decline = Clamp((overallDecline * 0.55) + (positionWeightedDecline * 0.45));
        var frequency = Clamp(Math.Log(entries.Count + 1, 2) / 4.0);
        var daysSinceLast = Math.Max(0.0, (now - ordered[^1].Timestamp).TotalDays);
        var recency = Math.Exp(-daysSinceLast / 14.0);
        var averageConfidence = Clamp(ordered.Average(entry => NormalizeConfidence(entry.ConfidenceScore)));
        var calibrationQualityScore = ComputeRecentCalibrationQualityScore(ordered);
        var calibrationConfidenceAdjustment = 0.70 + (0.30 * calibrationQualityScore);
        var evidence = ComputeEvidenceStrength(entries.Count);
        var reliabilitySampleDepthScore = Clamp(entries.Count / 8.0);
        var reliabilityVarianceScore = Clamp(1.0 - Math.Min(1.0, ComputeVariance(recent.Select(item => Clamp(item.OverallScore)).ToArray()) / 0.08));
        var overallTrend = ComputeConfidenceWeightedTrendSlope(declineScoped, settings.ConfidencePenaltyStrength);
        var reliabilityTrendStabilityScore = Clamp(1.0 - Math.Min(1.0, Math.Abs(overallTrend * DeclineWindowDays) / 0.45));
        var reliabilityScore = Clamp((reliabilitySampleDepthScore + reliabilityVarianceScore + reliabilityTrendStabilityScore + calibrationQualityScore) / 4.0);
        var (ciLower, ciUpper) = ComputeScoreConfidenceInterval(recent.Select(item => Clamp(item.OverallScore)).ToArray());
        var ciSuppressed = recent.Length < Math.Max(2, ciMinSamples);
        var positionSamples = BuildPositionSamples(target, entries);
        var positionAverages = positionSamples.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Length == 0 ? 0.0 : kvp.Value.Average(),
            StringComparer.OrdinalIgnoreCase);
        var positionSampleGateMet = PositionOrder.All(position =>
            positionSamples.TryGetValue(position, out var values) && values.Length >= MinimumPositionSamples);
        var positionDeltas = BuildPositionDeltas(target, entries, positionSamples, positionSampleGateMet);
        var positionSequence = positionSampleGateMet
            ? PositionOrder
                .OrderBy(position => positionDeltas.TryGetValue(position, out var delta) ? delta : 0.0)
                .ThenBy(position => position)
                .ToArray()
            : PositionOrder.ToArray();

        var weighted = (settings.SeverityWeight * severity) +
                       (settings.InstabilityWeight * instability) +
                       (settings.DeclineWeight * decline) +
                       (settings.FrequencyWeight * frequency);
        var priority = Clamp(weighted * recency * evidence);

        return new TargetAssignmentCandidate(
            target,
            priority,
            severity,
            instability,
            decline,
            frequency,
            1.0,
            averageConfidence,
            calibrationQualityScore,
            calibrationConfidenceAdjustment,
            recency,
            evidence,
            positionWeightedDecline,
            reliabilityScore,
            reliabilitySampleDepthScore,
            reliabilityVarianceScore,
            reliabilityTrendStabilityScore,
            recentMean,
            ciLower,
            ciUpper,
            ciSuppressed,
            instabilityWindow,
            declineWindow,
            entries.Count,
            ordered[^1].Timestamp,
            positionTrendSlopes,
            positionSequence,
                positionDeltas,
                positionAverages,
                positionSamples,
                positionSampleGateMet);
    }

    private static string BuildRationale(
        IReadOnlyList<TargetAssignmentCandidate> candidates,
        string commonPattern,
        AssignmentPrioritySettings settings,
        bool suppressChange,
        double confidenceVariance,
        double confidenceVarianceGate,
        AssignmentSuppressionBehavior suppressionBehavior,
        bool exceedsVarianceGate,
        bool hasLowReliabilityRisk)
    {
        if (candidates.Count == 0)
        {
            return "No weak patterns found yet. Continue with mixed articulation drills for consistency.";
        }

        var top = candidates[0];
        var targetList = string.Join(", ", candidates.Select(c => c.Target));
        var gatingText = exceedsVarianceGate
            ? $" Confidence variance {confidenceVariance:0.000} exceeded gate {confidenceVarianceGate:0.000} under {suppressionBehavior.ToDisplayLabel().ToLowerInvariant()} behavior."
            : string.Empty;
        var reliabilityText = hasLowReliabilityRisk
            ? " Reliability profile indicates low-support targets; suppression guardrails may hold updates until stability improves."
            : string.Empty;
        var suppressionText = suppressChange
            ? " Assignment updates were suppressed for this cycle."
            : string.Empty;

         return $"Focus on {targetList} based on weighted priority from recent severity, instability, trend decline, and repetition frequency. " +
               $"Weights are severity {settings.SeverityWeight:0.00}, instability {settings.InstabilityWeight:0.00}, decline {settings.DeclineWeight:0.00}, frequency {settings.FrequencyWeight:0.00}, with confidence penalty strength {settings.ConfidencePenaltyStrength:0.00}. " +
                             $"Highest-priority target is {top.Target} (priority {top.Priority:0.00}, severity {top.Severity:0.00}, instability {top.Instability:0.00}, position-weighted decline {top.PositionWeightedDecline:0.00}, confidence factor {top.ConfidenceFactor:0.00}, calibration quality {top.CalibrationQualityScore:0.00}, calibration confidence adjustment {top.CalibrationConfidenceAdjustment:0.00}). " +
             $"Most frequent challenge pattern was '{commonPattern}', so drills should emphasize slow, repeatable production before speed." +
               gatingText + reliabilityText + suppressionText;
    }

    private static double ComputePriority(TargetAssignmentCandidate candidate, AssignmentPrioritySettings settings)
    {
                var baseConfidenceFactor = ((1.0 - settings.ConfidencePenaltyStrength) +
                                                                        (settings.ConfidencePenaltyStrength * candidate.AverageConfidence));
                var confidenceFactor = baseConfidenceFactor * candidate.CalibrationConfidenceAdjustment;
        var weighted = (settings.SeverityWeight * candidate.Severity) +
                       (settings.InstabilityWeight * candidate.Instability) +
                       (settings.DeclineWeight * candidate.Decline) +
                       (settings.FrequencyWeight * candidate.Frequency);
        var reliabilityFactor = 0.70 + (0.30 * candidate.ReliabilityScore);
        return Clamp(weighted * candidate.Recency * confidenceFactor * candidate.EvidenceStrength * candidate.FrequencyNormalizationFactor * reliabilityFactor);
    }

    private static IReadOnlyDictionary<string, double> BuildPositionTrendSlopes(string target, IReadOnlyList<ProgressEntry> entries)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in PositionOrder)
        {
            var values = entries
                .Where(entry => IsPositionMatch(target, position, entry))
                .OrderBy(entry => entry.Timestamp)
                .Select(entry => Clamp(entry.OverallScore))
                .ToArray();
            result[position] = ComputeSimpleTrendSlope(values);
        }

        return result;
    }

    private static double ComputePositionWeightedDecline(
        IReadOnlyDictionary<string, double> positionTrendSlopes,
        IReadOnlyDictionary<string, double> positionImportanceWeights)
    {
        if (positionTrendSlopes.Count == 0)
        {
            return 0.0;
        }

        var weightedDecline = 0.0;
        var totalWeight = 0.0;
        foreach (var position in PositionOrder)
        {
            var slope = positionTrendSlopes.TryGetValue(position, out var value) ? value : 0.0;
            var weight = positionImportanceWeights.TryGetValue(position, out var positionWeight) ? positionWeight : 0.0;
            weightedDecline += weight * Math.Max(0.0, -slope * DeclineWindowDays);
            totalWeight += weight;
        }

        if (totalWeight <= 0)
        {
            return 0.0;
        }

        return Clamp(weightedDecline / totalWeight);
    }

    private static double ComputeSimpleTrendSlope(IReadOnlyList<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var n = values.Count;
        var meanX = (n - 1) / 2.0;
        var meanY = values.Average();
        var covariance = 0.0;
        var variance = 0.0;
        for (var i = 0; i < n; i++)
        {
            var x = i - meanX;
            covariance += x * (values[i] - meanY);
            variance += x * x;
        }

        if (variance <= 1e-9)
        {
            return 0.0;
        }

        return covariance / variance;
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
        IReadOnlyDictionary<string, double[]> positionSamples,
        bool positionSampleGateMet)
    {
        var deltas = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (!positionSampleGateMet)
        {
            foreach (var position in PositionOrder)
            {
                deltas[position] = 0.0;
            }

            return deltas;
        }

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

    private static TargetAssignmentCandidate[] ApplyHardFreezeTargets(
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

    private static TargetAssignmentCandidate[] ApplyPartialUpdateTargets(
        IReadOnlyList<TargetAssignmentCandidate> candidates,
        AssignmentSnapshot? latestSnapshot,
        AssignmentPrioritySettings settings)
    {
        if (latestSnapshot is null)
        {
            return candidates.ToArray();
        }

        var frozenTargets = ParseCsv(latestSnapshot.FocusTargetsCsv);
        var candidateByTarget = candidates.ToDictionary(candidate => candidate.Target, StringComparer.OrdinalIgnoreCase);

        var result = new List<TargetAssignmentCandidate>();
        foreach (var target in frozenTargets)
        {
            if (candidateByTarget.TryGetValue(target, out var candidate))
            {
                result.Add(candidate with { Priority = ComputePriority(candidate, settings) + 0.0001 });
            }
        }

        foreach (var candidate in candidates)
        {
            if (result.Any(existing => string.Equals(existing.Target, candidate.Target, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            result.Add(candidate);
            if (result.Count >= MaxFocusTargets)
            {
                break;
            }
        }

        return result
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

    private static double ComputeConfidenceWeightedTrendSlope(IReadOnlyList<ProgressEntry> entries, double penaltyStrength)
    {
        if (entries.Count < 2)
        {
            return 0.0;
        }

        var ordered = entries
            .OrderBy(item => item.Timestamp)
            .ToArray();
        var t0 = ordered[0].Timestamp;

        var weighted = ordered
            .Select(entry =>
            {
                var x = Math.Max(0.0, (entry.Timestamp - t0).TotalDays);
                var y = Clamp(entry.OverallScore);
                var confidence = NormalizeConfidence(entry.ConfidenceScore);
                var w = Math.Max(0.05, (1.0 - penaltyStrength) + (penaltyStrength * confidence));
                return (x, y, w);
            })
            .ToArray();

        var weightSum = weighted.Sum(item => item.w);
        if (weightSum <= 0.0)
        {
            return 0.0;
        }

        var meanX = weighted.Sum(item => item.x * item.w) / weightSum;
        var meanY = weighted.Sum(item => item.y * item.w) / weightSum;
        var covariance = weighted.Sum(item => item.w * (item.x - meanX) * (item.y - meanY));
        var varianceX = weighted.Sum(item => item.w * Math.Pow(item.x - meanX, 2));

        if (varianceX <= 1e-9)
        {
            return 0.0;
        }

        return covariance / varianceX;
    }

    private static double ComputeEvidenceStrength(int attempts)
    {
        var count = Math.Max(0, attempts);
        var scaled = 1.0 - Math.Exp(-count / EvidenceHalfLifeAttempts);
        return Clamp(EvidenceFloor + ((1.0 - EvidenceFloor) * scaled));
    }

    private static double ComputeRecentCalibrationQualityScore(IReadOnlyList<ProgressEntry> orderedEntries)
    {
        if (orderedEntries is null || orderedEntries.Count == 0)
        {
            return 0.60;
        }

        var recent = orderedEntries
            .OrderByDescending(entry => entry.Timestamp)
            .Take(8)
            .Reverse()
            .ToArray();

        var smoothed = 0.60;
        foreach (var entry in recent)
        {
            var support = Clamp(entry.EmpiricalOutcomeSupport <= 0.0 ? 0.40 : entry.EmpiricalOutcomeSupport);
            var quality = ResolveEntryCalibrationQuality(entry);
            var alpha = 0.35 + (0.45 * support);
            smoothed = ((1.0 - alpha) * smoothed) + (alpha * quality);
        }

        return Clamp(smoothed);
    }

    private static double ResolveEntryCalibrationQuality(ProgressEntry entry)
    {
        var payload = (entry.CalibrationTableJson ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty("quality", out var quality) &&
                    quality.ValueKind == JsonValueKind.Object &&
                    quality.TryGetProperty("qualityScore", out var scoreElement) &&
                    scoreElement.ValueKind == JsonValueKind.Number)
                {
                    return Clamp(scoreElement.GetDouble());
                }
            }
            catch
            {
                // Keep fallback behavior for legacy or malformed calibration payloads.
            }
        }

        var support = Clamp(entry.EmpiricalOutcomeSupport <= 0.0 ? 0.35 : entry.EmpiricalOutcomeSupport);
        if (!string.IsNullOrWhiteSpace(entry.CalibrationMethod) &&
            entry.CalibrationMethod.Contains("isotonic", StringComparison.OrdinalIgnoreCase))
        {
            return Clamp(0.65 + (0.25 * support));
        }

        return Clamp(0.55 + (0.20 * support));
    }

    private static IReadOnlyDictionary<string, double> BuildPositionImportanceWeights(AssignmentPrioritySettings settings)
    {
        var weights = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["initial"] = Clamp(settings.PositionInitialWeight),
            ["medial"] = Clamp(settings.PositionMedialWeight),
            ["final"] = Clamp(settings.PositionFinalWeight)
        };

        var sum = weights.Values.Sum();
        if (sum <= 0)
        {
            return new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                ["initial"] = AssignmentPrioritySettings.DefaultPositionInitialWeight,
                ["medial"] = AssignmentPrioritySettings.DefaultPositionMedialWeight,
                ["final"] = AssignmentPrioritySettings.DefaultPositionFinalWeight
            };
        }

        return weights.ToDictionary(item => item.Key, item => item.Value / sum, StringComparer.OrdinalIgnoreCase);
    }

    private static double ComputeAssignmentUncertaintyBudget(IReadOnlyList<TargetAssignmentCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return 0.0;
        }

        var scoped = candidates.Take(MaxFocusTargets).ToArray();
        var weights = scoped
            .Select(candidate => Math.Max(candidate.Priority, 0.01))
            .ToArray();
        var weightSum = weights.Sum();
        if (weightSum <= 0)
        {
            return 0.0;
        }

        var aggregate = 0.0;
        for (var i = 0; i < scoped.Length; i++)
        {
            var candidate = scoped[i];
            var evidenceUncertainty = 1.0 - Clamp(candidate.EvidenceStrength);
            var reliabilityUncertainty = 1.0 - Clamp(candidate.ReliabilityScore);
            var candidateUncertainty = Clamp((0.45 * evidenceUncertainty) + (0.55 * reliabilityUncertainty));
            aggregate += candidateUncertainty * (weights[i] / weightSum);
        }

        return Clamp(aggregate);
    }

    private static int ResolveAdaptiveWindow(int attemptCount)
    {
        if (attemptCount <= 0)
        {
            return MinimumAdaptiveWindow;
        }

        var normalized = Math.Log(attemptCount + 1, 2);
        var window = (int)Math.Round(MinimumAdaptiveWindow + (normalized * 1.6));
        return Math.Clamp(window, MinimumAdaptiveWindow, MaximumAdaptiveWindow);
    }

    private static (double Lower, double Upper) ComputeScoreConfidenceInterval(IReadOnlyList<double> sample)
    {
        if (sample is null || sample.Count == 0)
        {
            return (0.0, 0.0);
        }

        var mean = sample.Average();
        if (sample.Count < 2)
        {
            return (Clamp(mean), Clamp(mean));
        }

        var stdDev = Math.Sqrt(ComputeVariance(sample));
        var margin = 1.96 * (stdDev / Math.Sqrt(sample.Count));
        return (Clamp(mean - margin), Clamp(mean + margin));
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
        double FrequencyNormalizationFactor,
        double AverageConfidence,
        double CalibrationQualityScore,
        double CalibrationConfidenceAdjustment,
        double Recency,
        double EvidenceStrength,
        double PositionWeightedDecline,
        double ReliabilityScore,
        double ReliabilitySampleDepthScore,
        double ReliabilityVarianceScore,
        double ReliabilityTrendStabilityScore,
        double OverallScoreMean,
        double OverallScoreCiLower,
        double OverallScoreCiUpper,
        bool CiSuppressed,
        int InstabilityWindowSize,
        int DeclineWindowSize,
        int Attempts,
        DateTime LastAttemptAt,
        IReadOnlyDictionary<string, double> PositionTrendSlopes,
        IReadOnlyList<string> PositionSequence,
        IReadOnlyDictionary<string, double> PositionDeltas,
        IReadOnlyDictionary<string, double> PositionAverages,
        IReadOnlyDictionary<string, double[]> PositionSamples,
        bool PositionSequenceGateMet)
    {
        public double ConfidenceFactor => AverageConfidence;
    }
}
