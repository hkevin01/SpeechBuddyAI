using System.Text.Json;
using SpeechBuddyAI.Database;
using SpeechBuddyAI.Models;
using SQLite;

namespace SpeechBuddyAI.Services;

public sealed class AssignmentSnapshotService
{
    private const int MinHistoryDepthForWeightShift = 4;
    private const int MinMatchedTargetsNext1ForWeightShift = 3;
    private const int MinMatchedTargetsNext3ForWeightShift = 5;
    private const double MaxWeightDeltaPerUpdate = 0.05;
    private const double MaxPenaltyDeltaPerUpdate = 0.08;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _database;
    private bool _isInitialized;

    public async Task SaveSnapshotAsync(HomeAssignment assignment, int sourceEntryCount)
    {
        if (assignment is null)
        {
            throw new ArgumentNullException(nameof(assignment));
        }

        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                var previousSnapshot = await Database.Table<AssignmentSnapshot>()
                    .OrderByDescending(item => item.SnapshotDateTicks)
                    .Take(12)
                    .ToListAsync();
                var previous = previousSnapshot.FirstOrDefault();
                var historicalSnapshots = previousSnapshot
                    .OrderBy(item => item.SnapshotDateTicks)
                    .ToArray();
                var previousFocusTargets = ParseCsv(previous?.FocusTargetsCsv);
                var currentFocusTargets = (assignment.FocusTargets ?? Array.Empty<string>()).ToArray();
                var focusChangeCount = CountFocusChanges(previousFocusTargets, currentFocusTargets);
                var suppressed = assignment.FocusTargetReasons.Any(reason => reason.AssignmentChangeSuppressed);
                var currentReasons = assignment.FocusTargetReasons ?? Array.Empty<AssignmentFocusTargetReason>();
                var componentTraces = BuildComponentTraces(currentReasons);
                var scoringFormulaVersion = string.IsNullOrWhiteSpace(assignment.ScoringFormulaVersion)
                    ? AiTextService.ScoringFormulaVersion
                    : assignment.ScoringFormulaVersion;
                var calibration = BuildCalibrationMetrics(historicalSnapshots, currentReasons);
                var advisorySuggestion = BuildAdvisoryWeightSuggestion(calibration, historicalSnapshots);

                var snapshot = new AssignmentSnapshot
                {
                    SnapshotDate = DateTimeOffset.UtcNow,
                    Rationale = assignment.Rationale ?? string.Empty,
                    FocusTargetsCsv = string.Join(",", assignment.FocusTargets ?? Array.Empty<string>()),
                    SuggestedWordsCsv = string.Join(",", assignment.SuggestedWords ?? Array.Empty<string>()),
                    TargetReasonsJson = JsonSerializer.Serialize(currentReasons),
                    PreviousRationale = previous?.Rationale ?? string.Empty,
                    RationaleDriftSummary = BuildRationaleDriftSummary(previous?.Rationale, assignment.Rationale, previousFocusTargets, currentFocusTargets, suppressed),
                    PreviousFocusTargetsCsv = string.Join(",", previousFocusTargets),
                    FocusChangeCount = focusChangeCount,
                    AssignmentChangeSuppressed = suppressed,
                    SourceEntryCount = Math.Max(0, sourceEntryCount),
                    ComponentTracesJson = JsonSerializer.Serialize(componentTraces),
                    CalibrationMetricsJson = JsonSerializer.Serialize(calibration),
                    CalibrationSummary = calibration.Summary,
                    ScoringFormulaVersion = scoringFormulaVersion,
                    AdvisoryWeightSuggestionJson = JsonSerializer.Serialize(advisorySuggestion),
                    ReviewRequired = assignment.ReviewRequired,
                    UncertaintyBudgetScore = assignment.UncertaintyBudgetScore,
                    UncertaintyBudgetCap = assignment.UncertaintyBudgetCap,
                    UncertaintyBudgetSummary = assignment.UncertaintyBudgetSummary ?? string.Empty
                };

                await Database.InsertAsync(snapshot);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to persist assignment snapshot.", ex);
        }
    }

    public async Task<AssignmentSnapshot?> GetLatestSnapshotAsync()
    {
        var recent = await GetRecentSnapshotsAsync(1);
        return recent.FirstOrDefault();
    }

    public async Task<IReadOnlyList<AssignmentSnapshot>> GetRecentSnapshotsAsync(int take = 10)
    {
        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                var count = Math.Max(1, take);
                var snapshots = await Database.Table<AssignmentSnapshot>()
                    .OrderByDescending(item => item.SnapshotDateTicks)
                    .Take(count)
                    .ToListAsync();
                return snapshots;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load assignment snapshots.", ex);
        }
    }

    public static IReadOnlyList<AssignmentFocusTargetReason> ParseReasons(string? reasonsJson)
    {
        if (string.IsNullOrWhiteSpace(reasonsJson))
        {
            return Array.Empty<AssignmentFocusTargetReason>();
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<AssignmentFocusTargetReason>>(reasonsJson) ?? Array.Empty<AssignmentFocusTargetReason>();
        }
        catch
        {
            return Array.Empty<AssignmentFocusTargetReason>();
        }
    }

    public static string BuildSelectionDetails(IReadOnlyList<AssignmentFocusTargetReason> reasons)
    {
        if (reasons is null || reasons.Count == 0)
        {
            return "No focus-target reason details were available.";
        }

        return string.Join(
            Environment.NewLine,
            reasons.Select(reason =>
                $"- {reason.TargetSound}: priority {reason.PriorityScore:0.00} (severity {reason.SeverityScore:0.00}, instability {reason.InstabilityScore:0.00}, decline {reason.DeclineScore:0.00}, frequency {reason.FrequencyScore:0.00}, confidence factor {reason.ConfidenceFactor:0.00}, evidence {reason.EvidenceStrength:0.00}, variance {reason.ConfidenceVariance:0.000}); " +
                (reason.ConfidenceIntervalSuppressed
                    ? $"score CI95 hidden (n below {reason.ConfidenceIntervalMinSamples}); "
                    : $"score CI95 [{reason.OverallScoreCiLower:0.00}, {reason.OverallScoreCiUpper:0.00}] around mean {reason.OverallScoreMean:0.00}; ") +
                $"windows instability n={reason.InstabilityWindowSize}, decline n={reason.DeclineWindowSize}; position order {reason.PositionSequence}; deltas {reason.PositionDeltaSummary}{(reason.AssignmentChangeSuppressed ? " [suppressed]" : string.Empty)}"));
    }

    public static IReadOnlyList<AssignmentComponentTracePoint> ParseComponentTraces(string? componentTracesJson)
    {
        if (string.IsNullOrWhiteSpace(componentTracesJson))
        {
            return Array.Empty<AssignmentComponentTracePoint>();
        }

        try
        {
            return JsonSerializer.Deserialize<IReadOnlyList<AssignmentComponentTracePoint>>(componentTracesJson) ?? Array.Empty<AssignmentComponentTracePoint>();
        }
        catch
        {
            return Array.Empty<AssignmentComponentTracePoint>();
        }
    }

    public static AssignmentCalibrationMetrics ParseCalibrationMetrics(string? calibrationMetricsJson)
    {
        if (string.IsNullOrWhiteSpace(calibrationMetricsJson))
        {
            return new AssignmentCalibrationMetrics { Summary = "Calibration unavailable for this snapshot." };
        }

        try
        {
            return JsonSerializer.Deserialize<AssignmentCalibrationMetrics>(calibrationMetricsJson) ??
                   new AssignmentCalibrationMetrics { Summary = "Calibration unavailable for this snapshot." };
        }
        catch
        {
            return new AssignmentCalibrationMetrics { Summary = "Calibration unavailable for this snapshot." };
        }
    }

    public static AssignmentWeightSuggestion ParseAdvisoryWeightSuggestion(string? advisoryWeightSuggestionJson)
    {
        if (string.IsNullOrWhiteSpace(advisoryWeightSuggestionJson))
        {
            return new AssignmentWeightSuggestion { Summary = "No advisory weight suggestion available yet." };
        }

        try
        {
            return JsonSerializer.Deserialize<AssignmentWeightSuggestion>(advisoryWeightSuggestionJson) ??
                   new AssignmentWeightSuggestion { Summary = "No advisory weight suggestion available yet." };
        }
        catch
        {
            return new AssignmentWeightSuggestion { Summary = "No advisory weight suggestion available yet." };
        }
    }

    public static string BuildModelAuditSummary(IReadOnlyList<AssignmentSnapshot> snapshots)
    {
        if (snapshots is null || snapshots.Count == 0)
        {
            return "No assignment model-audit snapshots available.";
        }

        var ordered = snapshots
            .OrderBy(snapshot => snapshot.SnapshotDateTicks)
            .TakeLast(8)
            .ToArray();
        var latest = ordered[^1];
        var latestCalibration = ParseCalibrationMetrics(latest.CalibrationMetricsJson);
        var latestSuggestion = ParseAdvisoryWeightSuggestion(latest.AdvisoryWeightSuggestionJson);

        var latestTraces = ParseComponentTraces(latest.ComponentTracesJson);
        var latestTraceText = latestTraces.Count == 0
            ? "No component traces in latest snapshot."
            : string.Join(
                "; ",
                latestTraces
                    .OrderByDescending(trace => trace.PriorityScore)
                    .Select(trace =>
                        $"{trace.TargetSound}: priority {trace.PriorityScore:0.00}, sev {trace.SeverityScore:0.00}, inst {trace.InstabilityScore:0.00}, dec {trace.DeclineScore:0.00}, ci95 [{trace.OverallScoreCiLower:0.00}, {trace.OverallScoreCiUpper:0.00}]"));
        var formulaVersions = ordered
            .Select(snapshot => string.IsNullOrWhiteSpace(snapshot.ScoringFormulaVersion) ? "unknown" : snapshot.ScoringFormulaVersion)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return
            $"Latest calibration - {latestCalibration.Summary}" + Environment.NewLine +
            $"Horizon next-1 MAE {latestCalibration.MeanAbsoluteErrorNext1:0.000}, next-3 MAE {latestCalibration.MeanAbsoluteErrorNext3:0.000}." + Environment.NewLine +
            $"Formula versions in window: {string.Join(", ", formulaVersions)}." + Environment.NewLine +
            $"Uncertainty budget: {(latest.ReviewRequired ? "review-required" : "within-cap")} (score {latest.UncertaintyBudgetScore:0.000}, cap {latest.UncertaintyBudgetCap:0.000})." + Environment.NewLine +
            $"Advisory weighting: {latestSuggestion.Summary}" + Environment.NewLine +
            $"Latest component trace - {latestTraceText}";
    }

    public static string BuildRationaleDriftSummary(
        string? previousRationale,
        string? currentRationale,
        IReadOnlyList<string> previousTargets,
        IReadOnlyList<string> currentTargets,
        bool changeSuppressed)
    {
        var previous = (previousRationale ?? string.Empty).Trim();
        var current = (currentRationale ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(previous))
        {
            return "Initial assignment snapshot captured. No prior rationale for drift comparison.";
        }

        var overlap = ComputeTokenOverlap(previous, current);
        var focusChangeCount = CountFocusChanges(previousTargets, currentTargets);
        var suppressionText = changeSuppressed ? " Assignment updates were suppressed due to high confidence variance." : string.Empty;

        return $"Rationale overlap {overlap:P0}; focus target changes: {focusChangeCount}." + suppressionText;
    }

    private static double ComputeTokenOverlap(string previous, string current)
    {
        var previousTokens = Tokenize(previous);
        var currentTokens = Tokenize(current);
        if (previousTokens.Count == 0)
        {
            return 0.0;
        }

        var intersection = previousTokens.Intersect(currentTokens, StringComparer.OrdinalIgnoreCase).Count();
        return Math.Clamp((double)intersection / previousTokens.Count, 0.0, 1.0);
    }

    private static HashSet<string> Tokenize(string text)
    {
        var parts = text
            .Split([' ', ',', '.', ';', ':', '\n', '\r', '\t', '|', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => token.Length > 2);
        return new HashSet<string>(parts, StringComparer.OrdinalIgnoreCase);
    }

    private static int CountFocusChanges(IReadOnlyList<string> previousTargets, IReadOnlyList<string> currentTargets)
    {
        var previous = new HashSet<string>(previousTargets ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var current = new HashSet<string>(currentTargets ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        return previous.Except(current, StringComparer.OrdinalIgnoreCase).Count() +
               current.Except(previous, StringComparer.OrdinalIgnoreCase).Count();
    }

    private static IReadOnlyList<string> ParseCsv(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_isInitialized)
            {
                return;
            }

            _database = new SQLiteAsyncConnection(DbConstants.DatabasePath, DbConstants.Flags);
            await _database.CreateTableAsync<AssignmentSnapshot>();
            await EnsureSchemaColumnsAsync(_database);
            _isInitialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private SQLiteAsyncConnection Database =>
        _database ?? throw new InvalidOperationException("Database is not initialized.");

    private static async Task EnsureSchemaColumnsAsync(SQLiteAsyncConnection db)
    {
        var tableInfo = await db.QueryAsync<TableInfoRow>("PRAGMA table_info(AssignmentSnapshot);");
        var existing = new HashSet<string>(tableInfo.Select(item => item.Name), StringComparer.OrdinalIgnoreCase);
        var commands = new List<string>();

        if (!existing.Contains("PreviousRationale"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN PreviousRationale TEXT NOT NULL DEFAULT '';");
        if (!existing.Contains("RationaleDriftSummary"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN RationaleDriftSummary TEXT NOT NULL DEFAULT '';");
        if (!existing.Contains("PreviousFocusTargetsCsv"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN PreviousFocusTargetsCsv TEXT NOT NULL DEFAULT '';");
        if (!existing.Contains("FocusChangeCount"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN FocusChangeCount INTEGER NOT NULL DEFAULT 0;");
        if (!existing.Contains("AssignmentChangeSuppressed"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN AssignmentChangeSuppressed INTEGER NOT NULL DEFAULT 0;");
        if (!existing.Contains("ComponentTracesJson"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN ComponentTracesJson TEXT NOT NULL DEFAULT '[]';");
        if (!existing.Contains("CalibrationMetricsJson"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN CalibrationMetricsJson TEXT NOT NULL DEFAULT '{}';");
        if (!existing.Contains("CalibrationSummary"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN CalibrationSummary TEXT NOT NULL DEFAULT '';");
        if (!existing.Contains("ScoringFormulaVersion"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN ScoringFormulaVersion TEXT NOT NULL DEFAULT '';");
        if (!existing.Contains("AdvisoryWeightSuggestionJson"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN AdvisoryWeightSuggestionJson TEXT NOT NULL DEFAULT '{}';");
        if (!existing.Contains("ReviewRequired"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN ReviewRequired INTEGER NOT NULL DEFAULT 0;");
        if (!existing.Contains("UncertaintyBudgetScore"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN UncertaintyBudgetScore REAL NOT NULL DEFAULT 0.0;");
        if (!existing.Contains("UncertaintyBudgetCap"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN UncertaintyBudgetCap REAL NOT NULL DEFAULT 0.0;");
        if (!existing.Contains("UncertaintyBudgetSummary"))
            commands.Add("ALTER TABLE AssignmentSnapshot ADD COLUMN UncertaintyBudgetSummary TEXT NOT NULL DEFAULT '';");

        foreach (var command in commands)
        {
            await TryExecuteMigrationCommandAsync(db, command);
        }
    }

    private static async Task TryExecuteMigrationCommandAsync(SQLiteAsyncConnection db, string command)
    {
        try
        {
            await db.ExecuteAsync(command);
        }
        catch (SQLiteException ex) when (
            ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Existing databases may already include this column.
        }
    }

    private static IReadOnlyList<AssignmentComponentTracePoint> BuildComponentTraces(IReadOnlyList<AssignmentFocusTargetReason> reasons)
    {
        if (reasons is null || reasons.Count == 0)
        {
            return Array.Empty<AssignmentComponentTracePoint>();
        }

        return reasons
            .Select(reason => new AssignmentComponentTracePoint
            {
                TargetSound = reason.TargetSound,
                PriorityScore = reason.PriorityScore,
                SeverityScore = reason.SeverityScore,
                InstabilityScore = reason.InstabilityScore,
                DeclineScore = reason.DeclineScore,
                FrequencyScore = reason.FrequencyScore,
                ConfidenceFactor = reason.ConfidenceFactor,
                EvidenceStrength = reason.EvidenceStrength,
                OverallScoreMean = reason.OverallScoreMean,
                OverallScoreCiLower = reason.OverallScoreCiLower,
                OverallScoreCiUpper = reason.OverallScoreCiUpper,
                ConfidenceIntervalSuppressed = reason.ConfidenceIntervalSuppressed,
                ConfidenceIntervalMinSamples = reason.ConfidenceIntervalMinSamples,
                InstabilityWindowSize = reason.InstabilityWindowSize,
                DeclineWindowSize = reason.DeclineWindowSize,
                AttemptCount = reason.InitialAttemptScores.Count + reason.MedialAttemptScores.Count + reason.FinalAttemptScores.Count,
                ScoringFormulaVersion = reason.ScoringFormulaVersion
            })
            .ToArray();
    }

    private static AssignmentCalibrationMetrics BuildCalibrationMetrics(
        IReadOnlyList<AssignmentSnapshot> historicalSnapshots,
        IReadOnlyList<AssignmentFocusTargetReason> currentReasons)
    {
        if (historicalSnapshots is null || currentReasons is null)
        {
            return new AssignmentCalibrationMetrics { Summary = "Calibration unavailable for this snapshot." };
        }

        var allReasonSets = new List<IReadOnlyList<AssignmentFocusTargetReason>>();
        allReasonSets.AddRange(historicalSnapshots
            .OrderBy(snapshot => snapshot.SnapshotDateTicks)
            .Select(snapshot => ParseReasons(snapshot.TargetReasonsJson)));
        allReasonSets.Add(currentReasons);

        var next1 = BuildHorizonCalibration(allReasonSets, horizonLength: 1);
        var next3 = BuildHorizonCalibration(allReasonSets, horizonLength: 3);

        if (next1.MatchedTargetCount == 0 && next3.MatchedTargetCount == 0)
        {
            return new AssignmentCalibrationMetrics
            {
                Summary = "Calibration pending: no overlapping targets between consecutive snapshots."
            };
        }

        return new AssignmentCalibrationMetrics
        {
            MatchedTargetCountNext1 = next1.MatchedTargetCount,
            MeanAbsoluteErrorNext1 = next1.MeanAbsoluteError,
            MeanSquaredErrorNext1 = next1.MeanSquaredError,
            RankAgreementNext1 = next1.RankAgreement,
            TopTargetHitRateNext1 = next1.TopTargetHitRate,
            MatchedTargetCountNext3 = next3.MatchedTargetCount,
            MeanAbsoluteErrorNext3 = next3.MeanAbsoluteError,
            MeanSquaredErrorNext3 = next3.MeanSquaredError,
            RankAgreementNext3 = next3.RankAgreement,
            TopTargetHitRateNext3 = next3.TopTargetHitRate,
            MatchedTargetCount = next1.MatchedTargetCount,
            MeanAbsoluteError = next1.MeanAbsoluteError,
            MeanSquaredError = next1.MeanSquaredError,
            RankAgreement = next1.RankAgreement,
            TopTargetHitRate = next1.TopTargetHitRate,
            Summary = $"next-1 matched {next1.MatchedTargetCount}, MAE {next1.MeanAbsoluteError:0.000}, rank {next1.RankAgreement:P0}; next-3 matched {next3.MatchedTargetCount}, MAE {next3.MeanAbsoluteError:0.000}, rank {next3.RankAgreement:P0}."
        };
    }

    private static AssignmentWeightSuggestion BuildAdvisoryWeightSuggestion(
        AssignmentCalibrationMetrics calibration,
        IReadOnlyList<AssignmentSnapshot> historicalSnapshots)
    {
        var historyDepth = Math.Max(0, historicalSnapshots?.Count ?? 0);
        var previousSuggestion = historicalSnapshots?
            .OrderByDescending(snapshot => snapshot.SnapshotDateTicks)
            .Select(snapshot => ParseAdvisoryWeightSuggestion(snapshot.AdvisoryWeightSuggestionJson))
            .FirstOrDefault();

        return BuildAdvisoryWeightSuggestionPreview(calibration, historyDepth, previousSuggestion);
    }

    public static AssignmentWeightSuggestion BuildAdvisoryWeightSuggestionPreview(
        AssignmentCalibrationMetrics calibration,
        int historyDepth,
        AssignmentWeightSuggestion? previousSuggestion = null)
    {
        var anchor = ResolveAnchorSuggestion(previousSuggestion);

        if (calibration is null)
        {
            return anchor with
            {
                Summary = "advisory only - no calibration snapshot available, retaining prior weights.",
                AdvisoryOnly = true
            };
        }

        if (historyDepth < MinHistoryDepthForWeightShift ||
            calibration.MatchedTargetCountNext1 < MinMatchedTargetsNext1ForWeightShift ||
            calibration.MatchedTargetCountNext3 < MinMatchedTargetsNext3ForWeightShift)
        {
            return anchor with
            {
                Summary = $"advisory only - insufficient history for safe weight shift (history {historyDepth}, next-1 matched {calibration.MatchedTargetCountNext1}, next-3 matched {calibration.MatchedTargetCountNext3}); retaining prior weights.",
                AdvisoryOnly = true
            };
        }

        var severity = anchor.SuggestedSeverityWeight;
        var instability = anchor.SuggestedInstabilityWeight;
        var decline = anchor.SuggestedDeclineWeight;
        var frequency = anchor.SuggestedFrequencyWeight;
        var confidencePenalty = anchor.SuggestedConfidencePenaltyStrength;

        if (calibration.MeanAbsoluteErrorNext3 > 0.18)
        {
            instability += 0.05;
            decline += 0.05;
            frequency -= 0.05;
        }

        if (calibration.RankAgreementNext1 < 0.55)
        {
            severity += 0.04;
            frequency -= 0.02;
        }

        if (calibration.TopTargetHitRateNext1 < 0.50)
        {
            decline += 0.03;
        }

        if (calibration.MeanAbsoluteErrorNext1 > 0.20)
        {
            confidencePenalty = Math.Clamp(confidencePenalty + 0.10, 0.0, 1.0);
        }

        if (calibration.MeanAbsoluteErrorNext1 < 0.08 && calibration.RankAgreementNext1 > 0.75)
        {
            confidencePenalty = Math.Clamp(confidencePenalty - 0.05, 0.0, 1.0);
        }

        (severity, instability, decline, frequency) = NormalizeWeights(severity, instability, decline, frequency);
        severity = ClampByDelta(anchor.SuggestedSeverityWeight, severity, MaxWeightDeltaPerUpdate);
        instability = ClampByDelta(anchor.SuggestedInstabilityWeight, instability, MaxWeightDeltaPerUpdate);
        decline = ClampByDelta(anchor.SuggestedDeclineWeight, decline, MaxWeightDeltaPerUpdate);
        frequency = ClampByDelta(anchor.SuggestedFrequencyWeight, frequency, MaxWeightDeltaPerUpdate);
        (severity, instability, decline, frequency) = NormalizeWeights(severity, instability, decline, frequency);
        confidencePenalty = ClampByDelta(anchor.SuggestedConfidencePenaltyStrength, confidencePenalty, MaxPenaltyDeltaPerUpdate);

        return new AssignmentWeightSuggestion
        {
            SuggestedSeverityWeight = severity,
            SuggestedInstabilityWeight = instability,
            SuggestedDeclineWeight = decline,
            SuggestedFrequencyWeight = frequency,
            SuggestedConfidencePenaltyStrength = confidencePenalty,
            Summary = $"advisory only - suggested weights: severity {severity:0.00}, instability {instability:0.00}, decline {decline:0.00}, frequency {frequency:0.00}, confidence penalty {confidencePenalty:0.00}; guarded by history depth {historyDepth}, max per-update weight delta {MaxWeightDeltaPerUpdate:0.00}, max penalty delta {MaxPenaltyDeltaPerUpdate:0.00}; based on next-1 MAE {calibration.MeanAbsoluteErrorNext1:0.000} and next-3 MAE {calibration.MeanAbsoluteErrorNext3:0.000}.",
            AdvisoryOnly = true
        };
    }

    private static AssignmentWeightSuggestion ResolveAnchorSuggestion(AssignmentWeightSuggestion? previousSuggestion)
    {
        if (previousSuggestion is null)
        {
            return new AssignmentWeightSuggestion
            {
                SuggestedSeverityWeight = AssignmentPrioritySettings.DefaultSeverityWeight,
                SuggestedInstabilityWeight = AssignmentPrioritySettings.DefaultInstabilityWeight,
                SuggestedDeclineWeight = AssignmentPrioritySettings.DefaultDeclineWeight,
                SuggestedFrequencyWeight = AssignmentPrioritySettings.DefaultFrequencyWeight,
                SuggestedConfidencePenaltyStrength = AssignmentPrioritySettings.DefaultConfidencePenaltyStrength,
                Summary = "advisory only - using default weight anchor.",
                AdvisoryOnly = true
            };
        }

        (var severity, var instability, var decline, var frequency) = NormalizeWeights(
            previousSuggestion.SuggestedSeverityWeight,
            previousSuggestion.SuggestedInstabilityWeight,
            previousSuggestion.SuggestedDeclineWeight,
            previousSuggestion.SuggestedFrequencyWeight);

        return new AssignmentWeightSuggestion
        {
            SuggestedSeverityWeight = severity,
            SuggestedInstabilityWeight = instability,
            SuggestedDeclineWeight = decline,
            SuggestedFrequencyWeight = frequency,
            SuggestedConfidencePenaltyStrength = Math.Clamp(previousSuggestion.SuggestedConfidencePenaltyStrength, 0.0, 1.0),
            Summary = previousSuggestion.Summary,
            AdvisoryOnly = true
        };
    }

    private static (double Severity, double Instability, double Decline, double Frequency) NormalizeWeights(
        double severity,
        double instability,
        double decline,
        double frequency)
    {
        var s = Math.Clamp(severity, 0.0, 1.0);
        var i = Math.Clamp(instability, 0.0, 1.0);
        var d = Math.Clamp(decline, 0.0, 1.0);
        var f = Math.Clamp(frequency, 0.0, 1.0);
        var sum = Math.Max(1e-6, s + i + d + f);
        return (s / sum, i / sum, d / sum, f / sum);
    }

    private static double ClampByDelta(double anchor, double proposal, double maxDelta)
    {
        var delta = proposal - anchor;
        var clampedDelta = Math.Clamp(delta, -Math.Abs(maxDelta), Math.Abs(maxDelta));
        return Math.Clamp(anchor + clampedDelta, 0.0, 1.0);
    }

    private static AssignmentCalibrationMetrics BuildHorizonCalibration(
        IReadOnlyList<IReadOnlyList<AssignmentFocusTargetReason>> reasonSets,
        int horizonLength)
    {
        var comparisons = new List<(string Target, double PredictedRisk, double ObservedRisk)>();
        for (var i = 0; i < reasonSets.Count - 1; i++)
        {
            var predictionSet = reasonSets[i]
                .Where(reason => !string.IsNullOrWhiteSpace(reason.TargetSound))
                .ToArray();
            if (predictionSet.Length == 0)
            {
                continue;
            }

            var observedWindow = reasonSets
                .Skip(i + 1)
                .Take(Math.Min(horizonLength, reasonSets.Count - (i + 1)))
                .ToArray();
            if (observedWindow.Length == 0)
            {
                continue;
            }

            var observedByTarget = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var target in predictionSet.Select(item => item.TargetSound).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var observed = observedWindow
                    .Select(window => window.FirstOrDefault(reason => string.Equals(reason.TargetSound, target, StringComparison.OrdinalIgnoreCase)))
                    .Where(reason => reason is not null)
                    .Select(reason => Math.Clamp(reason!.SeverityScore, 0.0, 1.0))
                    .ToArray();

                if (observed.Length > 0)
                {
                    observedByTarget[target] = observed.Average();
                }
            }

            comparisons.AddRange(predictionSet
                .Where(reason => observedByTarget.ContainsKey(reason.TargetSound))
                .Select(reason =>
                    (reason.TargetSound,
                     PredictedRisk: Math.Clamp(reason.PriorityScore, 0.0, 1.0),
                     ObservedRisk: observedByTarget[reason.TargetSound])));
        }

        if (comparisons.Count == 0)
        {
            return new AssignmentCalibrationMetrics();
        }

        var mae = comparisons.Average(item => Math.Abs(item.PredictedRisk - item.ObservedRisk));
        var mse = comparisons.Average(item => Math.Pow(item.PredictedRisk - item.ObservedRisk, 2));
        var rankAgreement = ComputeRankAgreement(comparisons.Select(item => item.PredictedRisk).ToArray(), comparisons.Select(item => item.ObservedRisk).ToArray());
        var topPredicted = comparisons.OrderByDescending(item => item.PredictedRisk).First();
        var topObserved = comparisons.OrderByDescending(item => item.ObservedRisk).First();
        var topHitRate = string.Equals(topPredicted.Target, topObserved.Target, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

        return new AssignmentCalibrationMetrics
        {
            MatchedTargetCount = comparisons.Count,
            MeanAbsoluteError = mae,
            MeanSquaredError = mse,
            RankAgreement = rankAgreement,
            TopTargetHitRate = topHitRate
        };
    }

    private static double ComputeRankAgreement(IReadOnlyList<double> predicted, IReadOnlyList<double> observed)
    {
        if (predicted.Count != observed.Count || predicted.Count < 2)
        {
            return 0.0;
        }

        var concordant = 0;
        var total = 0;
        for (var i = 0; i < predicted.Count; i++)
        {
            for (var j = i + 1; j < predicted.Count; j++)
            {
                var left = predicted[i] - predicted[j];
                var right = observed[i] - observed[j];
                if (Math.Abs(left) < 1e-9 || Math.Abs(right) < 1e-9)
                {
                    continue;
                }

                total++;
                if ((left > 0 && right > 0) || (left < 0 && right < 0))
                {
                    concordant++;
                }
            }
        }

        if (total == 0)
        {
            return 0.0;
        }

        return Math.Clamp((double)concordant / total, 0.0, 1.0);
    }

    private sealed class TableInfoRow
    {
        [Column("name")]
        public string Name { get; init; } = string.Empty;
    }
}
