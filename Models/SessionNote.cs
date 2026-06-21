namespace SpeechBuddyAI.Models;

public class SessionNote
{
    public int Id { get; set; }
    public DateTimeOffset SessionDate { get; set; } = DateTimeOffset.UtcNow;
    public string RawNote { get; set; } = string.Empty;
    public string SoapSummary { get; set; } = string.Empty;
    public string ParentSummary { get; set; } = string.Empty;
}
