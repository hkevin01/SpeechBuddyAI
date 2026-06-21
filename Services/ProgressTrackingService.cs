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
        return AddAndSaveAsync(entry);
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetEntriesAsync()
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

    public async Task<IReadOnlyList<ProgressEntry>> GetEntriesForSoundAsync(string targetSound)
    {
        await EnsureInitializedAsync();
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();

        await _gate.WaitAsync();
        try
        {
            var all = await Database.Table<ProgressEntry>().ToListAsync();
            return all
                .Where(e => e.TargetSound.Trim().ToLowerInvariant() == normalizedTarget)
                .OrderBy(e => e.Timestamp)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetWeakestEntriesAsync(int take = 5)
    {
        await EnsureInitializedAsync();

        await _gate.WaitAsync();
        try
        {
            var all = await Database.Table<ProgressEntry>().ToListAsync();
            return all
                .GroupBy(e => e.TargetSound)
                .Select(g => new
                {
                    Target = g.Key,
                    Avg = g.Average(x => x.OverallScore),
                    Latest = g.OrderByDescending(x => x.Timestamp).First()
                })
                .OrderBy(x => x.Avg)
                .Take(Math.Max(1, take))
                .Select(x => x.Latest)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetRecentEntriesAsync(int take = 12)
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

    private async Task AddAndSaveAsync(ProgressEntry entry)
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
            await _database.CreateTableAsync<ProgressEntry>();
            _isInitialized = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<ProgressEntry>> GetEntriesForSoundInternalAsync(string targetSound)
    {
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();
        var all = await Database.Table<ProgressEntry>().ToListAsync();
        return all
            .Where(e => e.TargetSound.Trim().ToLowerInvariant() == normalizedTarget)
            .OrderBy(e => e.Timestamp)
            .ToList();
    }

    private SQLiteAsyncConnection Database =>
        _database ?? throw new InvalidOperationException("Database is not initialized.");
}
