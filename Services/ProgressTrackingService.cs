using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class ProgressTrackingService
{
    private readonly List<ProgressEntry> _entries = new();

    public Task AddEntryAsync(ProgressEntry entry)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ProgressEntry>> GetEntriesAsync()
    {
        return Task.FromResult((IReadOnlyList<ProgressEntry>)_entries);
    }
}
