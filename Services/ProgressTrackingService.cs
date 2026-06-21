using SpeechBuddyAI.Models;
using System.Text.Json;

namespace SpeechBuddyAI.Services;

public class ProgressTrackingService
{
    private readonly List<ProgressEntry> _entries = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _storagePath;
    private bool _isLoaded;

    public ProgressTrackingService()
    {
        _storagePath = Path.Combine(FileSystem.AppDataDirectory, "progress_entries.json");
    }

    public Task AddEntryAsync(ProgressEntry entry)
    {
        return AddAndSaveAsync(entry);
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetEntriesAsync()
    {
        await EnsureLoadedAsync();
        await _gate.WaitAsync();
        try
        {
            return _entries
                .OrderByDescending(e => e.Timestamp)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<ProgressEntry>> GetEntriesForSoundAsync(string targetSound)
    {
        await EnsureLoadedAsync();
        var normalizedTarget = (targetSound ?? string.Empty).Trim().ToLowerInvariant();

        await _gate.WaitAsync();
        try
        {
            return _entries
                .Where(e => e.TargetSound.Trim().ToLowerInvariant() == normalizedTarget)
                .OrderBy(e => e.Timestamp)
                .ToArray();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task AddAndSaveAsync(ProgressEntry entry)
    {
        await EnsureLoadedAsync();

        await _gate.WaitAsync();
        try
        {
            entry.Id = _entries.Count == 0 ? 1 : _entries.Max(e => e.Id) + 1;
            _entries.Add(entry);
            await PersistUnsafeAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureLoadedAsync()
    {
        if (_isLoaded)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            if (_isLoaded)
            {
                return;
            }

            if (File.Exists(_storagePath))
            {
                var raw = await File.ReadAllTextAsync(_storagePath);
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    var loaded = JsonSerializer.Deserialize<List<ProgressEntry>>(raw);
                    if (loaded is not null)
                    {
                        _entries.Clear();
                        _entries.AddRange(loaded);
                    }
                }
            }

            _isLoaded = true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task PersistUnsafeAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_storagePath)!);
        var payload = JsonSerializer.Serialize(_entries);
        await File.WriteAllTextAsync(_storagePath, payload);
    }
}
