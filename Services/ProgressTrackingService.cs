using SpeechBuddyAI.Database;
using SpeechBuddyAI.Models;
using SQLite;

namespace SpeechBuddyAI.Services;

public class ProgressTrackingService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _database;
    private bool _isInitialized;

    public ProgressTrackingService()
    {
    }

    public Task AddEntryAsync(ProgressEntry entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        return AddAndSaveAsync(entry);
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetEntriesAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                return await Database.Table<ProgressEntry>()
                    .OrderByDescending(e => e.Timestamp)
                    .ToListAsync();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load progress entries.", ex);
        }
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetEntriesForSoundAsync(string targetSound)
    {
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return Array.Empty<ProgressEntry>();
        }

        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                var all = await Database.Table<ProgressEntry>().ToListAsync();
                return all
                    .Where(e => (e.TargetSound ?? string.Empty).Trim().ToLowerInvariant() == normalizedTarget)
                    .OrderBy(e => e.Timestamp)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load entries for target sound.", ex);
        }
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetWeakestEntriesAsync(int take = 5)
    {
        var takeCount = Math.Max(1, take);

        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                var all = await Database.Table<ProgressEntry>().ToListAsync();
                return all
                    .Where(e => !string.IsNullOrWhiteSpace(e.TargetSound))
                    .GroupBy(e => e.TargetSound)
                    .Select(g => new
                    {
                        Target = g.Key,
                        Avg = g.Average(x => x.OverallScore),
                        Latest = g.OrderByDescending(x => x.Timestamp).First()
                    })
                    .OrderBy(x => x.Avg)
                    .Take(takeCount)
                    .Select(x => x.Latest)
                    .ToList();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load weakest entries.", ex);
        }
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetRecentEntriesAsync(int take = 12)
    {
        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                return await Database.Table<ProgressEntry>()
                    .OrderByDescending(e => e.Timestamp)
                    .Take(Math.Max(1, take))
                    .ToListAsync();
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load recent entries.", ex);
        }
    }

    private async Task AddAndSaveAsync(ProgressEntry entry)
    {
        if (entry is null)
        {
            throw new ArgumentNullException(nameof(entry));
        }

        if (string.IsNullOrWhiteSpace(entry.TargetSound))
        {
            throw new ArgumentException("Target sound is required when persisting a progress entry.");
        }

        if (string.IsNullOrWhiteSpace(entry.Transcript))
        {
            throw new ArgumentException("Transcript is required when persisting a progress entry.");
        }

        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                if (entry.Timestamp == default)
                {
                    entry.Timestamp = DateTime.UtcNow;
                }

                if (entry.TrialCount <= 0)
                {
                    var prior = await GetEntriesForSoundInternalAsync(entry.TargetSound);
                    entry.TrialCount = prior.Count + 1;
                }

                await Database.InsertAsync(entry);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException("Failed to persist progress entry.", ex);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            await _gate.WaitAsync();
            try
            {
                if (_isInitialized)
                {
                    return;
                }

                _database = new SQLiteAsyncConnection(DbConstants.DatabasePath, DbConstants.Flags);
                await _database.CreateTableAsync<ProgressEntry>();
                await EnsureSchemaColumnsAsync(_database);
                _isInitialized = true;
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to initialize progress database.", ex);
        }
    }

    private async Task<IReadOnlyList<ProgressEntry>> GetEntriesForSoundInternalAsync(string targetSound)
    {
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return Array.Empty<ProgressEntry>();
        }

        var all = await Database.Table<ProgressEntry>().ToListAsync();
        return all
            .Where(e => (e.TargetSound ?? string.Empty).Trim().ToLowerInvariant() == normalizedTarget)
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    private SQLiteAsyncConnection Database =>
        _database ?? throw new InvalidOperationException("Database is not initialized.");

    private static async Task EnsureSchemaColumnsAsync(SQLiteAsyncConnection db)
    {
        await TryAddColumnAsync(db, "ConfidenceScore", "REAL NOT NULL DEFAULT 0.0");
        await TryAddColumnAsync(db, "ConfidenceBand", "TEXT NOT NULL DEFAULT 'Low'");
    }

    private static async Task TryAddColumnAsync(SQLiteAsyncConnection db, string columnName, string definition)
    {
        try
        {
            await db.ExecuteAsync($"ALTER TABLE ProgressEntry ADD COLUMN {columnName} {definition};");
        }
        catch (SQLiteException ex) when (
            ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Existing databases may already include this column.
        }
    }
}
