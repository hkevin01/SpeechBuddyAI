using SQLite;

namespace SpeechBuddyAI.Models;

public sealed class AssignmentSnapshot
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long SnapshotDateTicks { get; set; } = DateTimeOffset.UtcNow.Ticks;

    public string Rationale { get; set; } = string.Empty;
    public string FocusTargetsCsv { get; set; } = string.Empty;
    public string SuggestedWordsCsv { get; set; } = string.Empty;
    public string TargetReasonsJson { get; set; } = "[]";
    public string PreviousRationale { get; set; } = string.Empty;
    public string RationaleDriftSummary { get; set; } = string.Empty;
    public string PreviousFocusTargetsCsv { get; set; } = string.Empty;
    public int FocusChangeCount { get; set; }
    public bool AssignmentChangeSuppressed { get; set; }
    public int SourceEntryCount { get; set; }

    [Ignore]
    public DateTimeOffset SnapshotDate
    {
        get => new DateTimeOffset(SnapshotDateTicks, TimeSpan.Zero);
        set => SnapshotDateTicks = value.UtcTicks;
    }
}
