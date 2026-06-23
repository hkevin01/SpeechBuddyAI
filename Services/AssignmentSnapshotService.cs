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
                var snapshot = new AssignmentSnapshot
                {
                    SnapshotDate = DateTimeOffset.UtcNow,
                    Rationale = assignment.Rationale ?? string.Empty,
                    FocusTargetsCsv = string.Join(",", assignment.FocusTargets ?? Array.Empty<string>()),
                    SuggestedWordsCsv = string.Join(",", assignment.SuggestedWords ?? Array.Empty<string>()),
                    TargetReasonsJson = JsonSerializer.Serialize(assignment.FocusTargetReasons ?? Array.Empty<AssignmentFocusTargetReason>()),
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
                $"- {reason.TargetSound}: priority {reason.PriorityScore:0.00} (severity {reason.SeverityScore:0.00}, instability {reason.InstabilityScore:0.00}, decline {reason.DeclineScore:0.00}, frequency {reason.FrequencyScore:0.00}, confidence factor {reason.ConfidenceFactor:0.00}); position order {reason.PositionSequence}; deltas {reason.PositionDeltaSummary}"));
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
            _isInitialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private SQLiteAsyncConnection Database =>
        _database ?? throw new InvalidOperationException("Database is not initialized.");
}
