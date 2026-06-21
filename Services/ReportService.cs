using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public class ReportService
{
    public Task<SessionNote> GenerateReportsAsync(string rawNote, IReadOnlyList<ProgressEntry> recentEntries)
    {
        var note = new SessionNote
        {
            SessionDate = DateTimeOffset.UtcNow,
            RawNote = rawNote ?? string.Empty,
            SoapSummary = BuildSoapSummary(rawNote, recentEntries),
            ParentSummary = BuildParentSummary(recentEntries)
        };

        return Task.FromResult(note);
    }

    private static string BuildSoapSummary(string rawNote, IReadOnlyList<ProgressEntry> entries)
    {
        var recent = entries.OrderByDescending(e => e.Timestamp).Take(5).ToArray();
        var average = recent.Length == 0 ? 0.0 : recent.Average(e => e.OverallScore);
        var strongest = recent.OrderByDescending(e => e.OverallScore).FirstOrDefault()?.TargetSound ?? "n/a";
        var weakest = recent.OrderBy(e => e.OverallScore).FirstOrDefault()?.TargetSound ?? "n/a";

        return
            "S: " + (string.IsNullOrWhiteSpace(rawNote) ? "No subjective note entered." : rawNote.Trim()) + Environment.NewLine +
            $"O: Recent attempts: {recent.Length}, average overall score {average:P0}, strongest target {strongest}, weakest target {weakest}." + Environment.NewLine +
            "A: Performance indicates emerging stability with observable target-specific variability." + Environment.NewLine +
            "P: Continue home drills on weaker targets, reinforce successful targets with mixed-context carryover, and reassess next session.";
    }

    private static string BuildParentSummary(IReadOnlyList<ProgressEntry> entries)
    {
        var recent = entries.OrderByDescending(e => e.Timestamp).Take(5).ToArray();
        if (recent.Length == 0)
        {
            return "No practice attempts were recorded yet. Start with short daily sessions and we will summarize progress after the first attempts.";
        }

        var avg = recent.Average(e => e.OverallScore);
        var topFocus = recent.OrderBy(e => e.OverallScore).First().TargetSound;

        return
            $"Your child completed {recent.Length} recent practice attempts with an average score of {avg:P0}. " +
            $"The main focus right now is '{topFocus}'. " +
            "Short, consistent practice sessions with calm repetition will help build more stable speech patterns between visits.";
    }
}
