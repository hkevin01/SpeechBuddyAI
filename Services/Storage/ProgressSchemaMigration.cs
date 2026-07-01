namespace SpeechBuddyAI.Services.Storage;

public static class ProgressSchemaMigration
{
    private static readonly (string Name, string Definition)[] RequiredColumns =
    {
        ("ConfidenceScore", "REAL NOT NULL DEFAULT 0.0"),
        ("ConfidenceBand", "TEXT NOT NULL DEFAULT 'Low'"),
        ("BaseTargetSound", "TEXT NOT NULL DEFAULT ''"),
        ("PositionTag", "TEXT NOT NULL DEFAULT ''"),
        ("ScoringFormulaVersion", "TEXT NOT NULL DEFAULT ''"),
        ("HistoricalDriftDetected", "INTEGER NOT NULL DEFAULT 0"),
        ("HistoricalDriftZScore", "REAL NOT NULL DEFAULT 0.0"),
        ("HistoricalDriftSummary", "TEXT NOT NULL DEFAULT ''"),
        ("AdaptiveModerateThreshold", "REAL NOT NULL DEFAULT 0.0"),
        ("AdaptiveHighThreshold", "REAL NOT NULL DEFAULT 0.0")
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
