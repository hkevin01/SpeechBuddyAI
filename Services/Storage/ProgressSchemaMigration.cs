namespace SpeechBuddyAI.Services.Storage;

public static class ProgressSchemaMigration
{
    private static readonly (string Name, string Definition)[] RequiredColumns =
    {
        ("ConfidenceScore", "REAL NOT NULL DEFAULT 0.0"),
        ("ConfidenceBand", "TEXT NOT NULL DEFAULT 'Low'")
    };

    public static IReadOnlyList<string> BuildMissingColumnCommands(IEnumerable<string> existingColumns)
    {
        var knownColumns = new HashSet<string>(
            (existingColumns ?? Array.Empty<string>()).Where(c => !string.IsNullOrWhiteSpace(c)),
            StringComparer.OrdinalIgnoreCase);

        var commands = new List<string>();
        foreach (var required in RequiredColumns)
        {
            if (!knownColumns.Contains(required.Name))
            {
                commands.Add($"ALTER TABLE ProgressEntry ADD COLUMN {required.Name} {required.Definition};");
            }
        }

        return commands;
    }
}
