using System.Text.Json;
using SpeechBuddyAI.Database;
using SpeechBuddyAI.Models;
using SQLite;

namespace SpeechBuddyAI.Services;

public sealed class AssignmentSnapshotService
{
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
                var previousReasons = ParseReasons(previous?.TargetReasonsJson);
                var currentReasons = assignment.FocusTargetReasons ?? Array.Empty<AssignmentFocusTargetReason>();
                var componentTraces = BuildComponentTraces(currentReasons);
                var scoringFormulaVersion = string.IsNullOrWhiteSpace(assignment.ScoringFormulaVersion)
                    ? AiTextService.ScoringFormulaVersion
                    : assignment.ScoringFormulaVersion;
                var calibration = BuildCalibrationMetrics(historicalSnapshots, currentReasons);
                var advisorySuggestion = BuildAdvisoryWeightSuggestion(historicalSnapshots, calibration);

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
                    AdvisoryWeightSuggestionJson = JsonSerializer.Serialize(advisorySuggestion)
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
                InstabilityWindowSize = reason.InstabilityWindowSize,
                DeclineWindowSize = reason.DeclineWindowSize,
                AttemptCount = reason.InitialAttemptScores.Count + reason.MedialAttemptScores.Count + reason.FinalAttemptScores.Count
            })
            .ToArray();
    }

    private static AssignmentCalibrationMetrics BuildCalibrationMetrics(
        IReadOnlyList<AssignmentFocusTargetReason> previousReasons,
        IReadOnlyList<AssignmentFocusTargetReason> currentReasons)
    {
        if (previousReasons is null || currentReasons is null)
        {
            return new AssignmentCalibrationMetrics { Summary = "Calibration unavailable for this snapshot." };
        }

        var currentByTarget = currentReasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason.TargetSound))
            .ToDictionary(reason => reason.TargetSound, StringComparer.OrdinalIgnoreCase);
        var matched = previousReasons
            .Where(reason => !string.IsNullOrWhiteSpace(reason.TargetSound) && currentByTarget.ContainsKey(reason.TargetSound))
            .Select(reason => new
            {
                Target = reason.TargetSound,
                PredictedRisk = Math.Clamp(reason.PriorityScore, 0.0, 1.0),
                ObservedRisk = Math.Clamp(currentByTarget[reason.TargetSound].SeverityScore, 0.0, 1.0)
            })
            .ToArray();

        if (matched.Length == 0)
        {
            return new AssignmentCalibrationMetrics
            {
                Summary = "Calibration pending: no overlapping targets between consecutive snapshots."
            };
        }

        var mae = matched.Average(item => Math.Abs(item.PredictedRisk - item.ObservedRisk));
        var mse = matched.Average(item => Math.Pow(item.PredictedRisk - item.ObservedRisk, 2));
        var rankAgreement = ComputeRankAgreement(matched.Select(item => item.PredictedRisk).ToArray(), matched.Select(item => item.ObservedRisk).ToArray());

        var topPredicted = matched.OrderByDescending(item => item.PredictedRisk).First();
        var topObserved = matched.OrderByDescending(item => item.ObservedRisk).First();
        var topHitRate = string.Equals(topPredicted.Target, topObserved.Target, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;

        return new AssignmentCalibrationMetrics
        {
            MatchedTargetCount = matched.Length,
            MeanAbsoluteError = mae,
            MeanSquaredError = mse,
            RankAgreement = rankAgreement,
            TopTargetHitRate = topHitRate,
            Summary = $"matched targets {matched.Length}, MAE {mae:0.000}, MSE {mse:0.000}, rank agreement {rankAgreement:P0}, top-target hit {topHitRate:P0}."
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
