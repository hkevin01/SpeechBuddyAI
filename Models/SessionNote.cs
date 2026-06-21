using SQLite;

namespace SpeechBuddyAI.Models;

// ID: MDL-SESSIONOTE-001
// Purpose: Persisted clinician session note with raw text, SOAP summary, and parent summary.
public class SessionNote
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public long SessionDateTicks { get; set; } = DateTimeOffset.UtcNow.Ticks;

    public string RawNote { get; set; } = string.Empty;
    public string SoapSummary { get; set; } = string.Empty;
    public string ParentSummary { get; set; } = string.Empty;

    [Ignore]
    public DateTimeOffset SessionDate
    {
        get => new DateTimeOffset(SessionDateTicks, TimeSpan.Zero);
        set => SessionDateTicks = value.UtcTicks;
    }
}
