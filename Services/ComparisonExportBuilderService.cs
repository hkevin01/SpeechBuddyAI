using SpeechBuddyAI.Models;

namespace SpeechBuddyAI.Services;

public sealed class ComparisonExportBuilderService
{
    private readonly SessionComparisonService _sessionComparisonService;
    private readonly ComparisonNarrativeGenerator _comparisonNarrativeGenerator;

    public ComparisonExportBuilderService(
        SessionComparisonService sessionComparisonService,
        ComparisonNarrativeGenerator comparisonNarrativeGenerator)
    {
        _sessionComparisonService = sessionComparisonService ?? throw new ArgumentNullException(nameof(sessionComparisonService));
        _comparisonNarrativeGenerator = comparisonNarrativeGenerator ?? throw new ArgumentNullException(nameof(comparisonNarrativeGenerator));
    }

    public SessionComparisonSnapshot Build(
        IReadOnlyList<ProgressEntry> entries,
        SessionComparisonNormalizationMode normalizationMode = SessionComparisonNormalizationMode.AttemptWeighted,
        SessionComparisonSmoothingStrength smoothingStrength = SessionComparisonSmoothingStrength.Balanced)
    {
        var snapshot = _sessionComparisonService.Build(entries, normalizationMode, smoothingStrength);
        var narrative = _comparisonNarrativeGenerator.Generate(snapshot);

        return new SessionComparisonSnapshot
        {
            HasCurrentSession = snapshot.HasCurrentSession,
            HasPreviousSession = snapshot.HasPreviousSession,
            CurrentSessionDate = snapshot.CurrentSessionDate,
            CurrentAttemptCount = snapshot.CurrentAttemptCount,
            CurrentAverageOverall = snapshot.CurrentAverageOverall,
            CurrentAverageConfidence = snapshot.CurrentAverageConfidence,
            PreviousSessionDate = snapshot.PreviousSessionDate,
            PreviousAttemptCount = snapshot.PreviousAttemptCount,
            PreviousAverageOverall = snapshot.PreviousAverageOverall,
            PreviousAverageConfidence = snapshot.PreviousAverageConfidence,
            NormalizationMode = snapshot.NormalizationMode,
            SmoothingStrength = snapshot.SmoothingStrength,
            TargetComparisons = snapshot.TargetComparisons,
            ConfidenceBandTransitions = snapshot.ConfidenceBandTransitions,
            RollingTimeline = snapshot.RollingTimeline,
            ComparisonNarrative = narrative
        };
    }
}
