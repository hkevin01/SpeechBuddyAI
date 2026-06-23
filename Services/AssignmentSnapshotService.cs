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
                    .Take(1)
                    .ToListAsync();
                var previous = previousSnapshot.FirstOrDefault();
                var previousFocusTargets = ParseCsv(previous?.FocusTargetsCsv);
                var currentFocusTargets = (assignment.FocusTargets ?? Array.Empty<string>()).ToArray();
                var focusChangeCount = CountFocusChanges(previousFocusTargets, currentFocusTargets);
                var suppressed = assignment.FocusTargetReasons.Any(reason => reason.AssignmentChangeSuppressed);

                var snapshot = new AssignmentSnapshot
                {
                    SnapshotDate = DateTimeOffset.UtcNow,
                    Rationale = assignment.Rationale ?? string.Empty,
                    FocusTargetsCsv = string.Join(",", assignment.FocusTargets ?? Array.Empty<string>()),
                    SuggestedWordsCsv = string.Join(",", assignment.SuggestedWords ?? Array.Empty<string>()),
                    TargetReasonsJson = JsonSerializer.Serialize(assignment.FocusTargetReasons ?? Array.Empty<AssignmentFocusTargetReason>()),
                    PreviousRationale = previous?.Rationale ?? string.Empty,
                    RationaleDriftSummary = BuildRationaleDriftSummary(previous?.Rationale, assignment.Rationale, previousFocusTargets, currentFocusTargets, suppressed),
                    PreviousFocusTargetsCsv = string.Join(",", previousFocusTargets),
                    FocusChangeCount = focusChangeCount,
                    AssignmentChangeSuppressed = suppressed,
                    SourceEntryCount = Math.Max(0, sourceEntryCount)
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
                $"- {reason.TargetSound}: priority {reason.PriorityScore:0.00} (severity {reason.SeverityScore:0.00}, instability {reason.InstabilityScore:0.00}, decline {reason.DeclineScore:0.00}, frequency {reason.FrequencyScore:0.00}, confidence factor {reason.ConfidenceFactor:0.00}, variance {reason.ConfidenceVariance:0.000}); position order {reason.PositionSequence}; deltas {reason.PositionDeltaSummary}{(reason.AssignmentChangeSuppressed ? " [suppressed]" : string.Empty)}"));
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

    private sealed class TableInfoRow
    {
        [Column("name")]
        public string Name { get; init; } = string.Empty;
    }
}
