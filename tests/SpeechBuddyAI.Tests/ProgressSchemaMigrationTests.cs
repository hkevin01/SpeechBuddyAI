using SpeechBuddyAI.Services.Storage;

namespace SpeechBuddyAI.Tests;

public sealed class ProgressSchemaMigrationTests
{
    [Fact]
    public void BuildMissingColumnCommands_ReturnsAllRequiredWhenNoColumnsExist()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(Array.Empty<string>());

        Assert.Equal(4, commands.Count);
        Assert.Contains(commands, c => c.Contains("ConfidenceScore", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("ConfidenceBand", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("BaseTargetSound", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("PositionTag", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildMissingColumnCommands_ReturnsOnlyMissingColumns()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(new[] { "Id", "ConfidenceScore", "BaseTargetSound", "PositionTag" });

        Assert.Single(commands);
        Assert.Contains("ConfidenceBand", commands[0], StringComparison.Ordinal);
    }

    [Fact]
    public void BuildMissingColumnCommands_TreatsExistingColumnsCaseInsensitive()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(new[] { "confidencescore", "CONFIDENCEBAND", "basetargetsound", "POSITIONTAG" });

        Assert.Empty(commands);
    }
}
