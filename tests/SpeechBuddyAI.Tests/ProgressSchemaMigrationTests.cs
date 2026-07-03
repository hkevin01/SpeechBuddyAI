using SpeechBuddyAI.Services.Storage;

namespace SpeechBuddyAI.Tests;

public sealed class ProgressSchemaMigrationTests
{
    [Fact]
    public void BuildMissingColumnCommands_ReturnsAllRequiredWhenNoColumnsExist()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(Array.Empty<string>());

        Assert.Equal(18, commands.Count);
        Assert.Contains(commands, c => c.Contains("ConfidenceScore", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("ConfidenceBand", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("BaseTargetSound", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("PositionTag", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("ScoringFormulaVersion", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("HistoricalDriftDetected", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("HistoricalDriftZScore", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("HistoricalDriftSummary", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("AdaptiveModerateThreshold", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("AdaptiveHighThreshold", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("RawConfidenceScore", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("EmpiricalOutcomeMean", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("EmpiricalOutcomeSupport", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("CalibrationResidual", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("CalibrationMethod", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("CalibrationTableJson", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("VarianceUncertaintyComponent", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("SparsityUncertaintyComponent", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildMissingColumnCommands_ReturnsOnlyMissingColumns()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(new[] { "Id", "ConfidenceScore", "BaseTargetSound", "PositionTag" });

        Assert.Equal(15, commands.Count);
        Assert.Contains(commands, c => c.Contains("ConfidenceBand", StringComparison.Ordinal));
        Assert.Contains(commands, c => c.Contains("ScoringFormulaVersion", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildMissingColumnCommands_TreatsExistingColumnsCaseInsensitive()
    {
        var commands = ProgressSchemaMigration.BuildMissingColumnCommands(new[]
        {
            "confidencescore",
            "CONFIDENCEBAND",
            "basetargetsound",
            "POSITIONTAG",
            "SCORINGFORMULAVERSION",
            "HISTORICALDRIFTDETECTED",
            "historicaldriftzscore",
            "HistoricalDriftSummary",
            "adaptivemoderatethreshold",
            "ADAPTIVEHIGHTHRESHOLD",
            "RAWCONFIDENCESCORE",
            "EmpiricalOutcomeMean",
            "empiricaloutcomesupport",
            "CalibrationResidual",
            "calibrationmethod",
            "CalibrationTableJson",
            "varianceuncertaintycomponent",
            "SPARSITYUNCERTAINTYCOMPONENT"
        });

        Assert.Empty(commands);
    }
}
