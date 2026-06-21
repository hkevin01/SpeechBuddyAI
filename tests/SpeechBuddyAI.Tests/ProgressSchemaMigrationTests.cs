using SpeechBuddyAI.Services.Storage;

namespace SpeechBuddyAI.Tests;

public sealed class ProgressSchemaMigrationTests
{
    [Fact]
    public void BuildMissingColumnCommands_ReturnsAllRequiredWhenNoColumnsExist()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(Array.Empty<string>());

        Assert.Equal(2, commands.Count);
        Assert.Contains(commands, c => c.Contains("ConfidenceScore", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("ConfidenceBand", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildMissingColumnCommands_ReturnsOnlyMissingColumns()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(new[] { "Id", "ConfidenceScore" });

        Assert.Single(commands);
        Assert.Contains("ConfidenceBand", commands[0], StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMissingColumnCommands_TreatsExistingColumnsCaseInsensitive()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(new[] { "confidencescore", "CONFIDENCEBAND" });

        Assert.Empty(commands);
    }
}
