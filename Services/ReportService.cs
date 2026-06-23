using SpeechBuddyAI.Models;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using SpeechBuddyAI.Services.Confidence;
using SpeechBuddyAI.Services.Reports;

namespace SpeechBuddyAI.Services;

public class ReportService
{
    private readonly ConfidenceSettingsService _confidenceSettingsService;
    private readonly ComparisonSnapshotCacheService _comparisonSnapshotCacheService;
    private readonly AssignmentSnapshotService _assignmentSnapshotService;

    public ReportService(
        ConfidenceSettingsService confidenceSettingsService,
        ComparisonSnapshotCacheService comparisonSnapshotCacheService,
        AssignmentSnapshotService assignmentSnapshotService)
    {
        _confidenceSettingsService = confidenceSettingsService ?? throw new ArgumentNullException(nameof(confidenceSettingsService));
        _comparisonSnapshotCacheService = comparisonSnapshotCacheService ?? throw new ArgumentNullException(nameof(comparisonSnapshotCacheService));
        _assignmentSnapshotService = assignmentSnapshotService ?? throw new ArgumentNullException(nameof(assignmentSnapshotService));
    }

    public Task<SessionNote> GenerateReportsAsync(string rawNote, IReadOnlyList<ProgressEntry> recentEntries)
    {
        var safeRawNote = rawNote ?? string.Empty;
        var safeEntries = recentEntries ?? Array.Empty<ProgressEntry>();

        try
        {
            var latestAssignmentSnapshot = await _assignmentSnapshotService.GetLatestSnapshotAsync();
            var targetReasons = AssignmentSnapshotService.ParseReasons(latestAssignmentSnapshot?.TargetReasonsJson);
            var note = new SessionNote
            {
                SessionDate = DateTimeOffset.UtcNow,
                RawNote = safeRawNote,
                SoapSummary = BuildSoapSummary(safeRawNote, safeEntries),
                ParentSummary = BuildParentSummary(safeEntries),
                AssignmentSnapshotDate = latestAssignmentSnapshot?.SnapshotDate,
                AssignmentSelectionSummary = latestAssignmentSnapshot?.Rationale ?? "No assignment snapshot available for this report window.",
                AssignmentSelectionDetails = AssignmentSnapshotService.BuildSelectionDetails(targetReasons),
                AssignmentRationaleDriftSummary = latestAssignmentSnapshot?.RationaleDriftSummary ?? "No rationale drift comparison available yet."
            };

            return Task.FromResult(note);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to generate clinician and parent reports.", ex);
        }
    }

    public string BuildExportText(SessionNote note)
    {
        return BuildExportText(note, Array.Empty<ProgressEntry>(), ReportExportFormat.PlainText);
    }

    public async Task<string> ExportReportAsync(SessionNote note)
    {
        return await ExportReportAsync(note, Array.Empty<ProgressEntry>(), ReportExportFormat.PlainText);
    }

    public string BuildExportText(SessionNote note, IReadOnlyList<ProgressEntry> metadataEntries, ReportExportFormat format)
    {
        var normalizationMode = _confidenceSettingsService.GetSessionComparisonNormalizationMode();
        var smoothingStrength = _confidenceSettingsService.GetSessionComparisonSmoothingStrength();
        var comparisonSnapshot = _comparisonSnapshotCacheService.GetOrBuild(metadataEntries ?? Array.Empty<ProgressEntry>(), normalizationMode, smoothingStrength);
        return ReportExportFormatter.BuildContent(note, metadataEntries, format, comparisonSnapshot);
    }

    public string BuildExportFileName(SessionNote note, ReportExportFormat format)
    {
        return ReportExportFormatter.BuildFileName(note, format);
    }

    public async Task<string> ExportReportAsync(SessionNote note, IReadOnlyList<ProgressEntry> metadataEntries, ReportExportFormat format)
    {
        if (note is null)
        {
            throw new ArgumentNullException(nameof(note));
        }

        try
        {
            var exportsDir = Path.Combine(FileSystem.AppDataDirectory, "exports");
            Directory.CreateDirectory(exportsDir);

            var fileName = BuildExportFileName(note, format);
            var filePath = Path.Combine(exportsDir, fileName);
            var content = BuildExportText(note, metadataEntries ?? Array.Empty<ProgressEntry>(), format);

            await File.WriteAllTextAsync(filePath, content);
            return filePath;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to export session report.", ex);
        }
    }

    public async Task ShareReportAsync(SessionNote note)
    {
        await ShareReportAsync(note, Array.Empty<ProgressEntry>(), ReportExportFormat.PlainText);
    }

    public async Task ShareReportAsync(SessionNote note, IReadOnlyList<ProgressEntry> metadataEntries, ReportExportFormat format)
    {
        if (note is null)
        {
            throw new ArgumentNullException(nameof(note));
        }

        var filePath = await ExportReportAsync(note, metadataEntries, format);

        try
        {
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Share Session Report",
                File = new ShareFile(filePath)
            });
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to share session report.", ex);
        }
    }

    private string BuildSoapSummary(string rawNote, IReadOnlyList<ProgressEntry> entries)
    {
        var safeEntries = entries ?? Array.Empty<ProgressEntry>();
        var safeRawNote = rawNote ?? string.Empty;

        try
        {
            var recent = safeEntries.OrderByDescending(e => e.Timestamp).Take(5).ToArray();
            var average = recent.Length == 0 ? 0.0 : recent.Average(e => e.OverallScore);
            var strongest = recent.OrderByDescending(e => e.OverallScore).FirstOrDefault()?.TargetSound ?? "n/a";
            var weakest = recent.OrderBy(e => e.OverallScore).FirstOrDefault()?.TargetSound ?? "n/a";
            var normalizationMode = _confidenceSettingsService.GetSessionComparisonNormalizationMode();
            var smoothingStrength = _confidenceSettingsService.GetSessionComparisonSmoothingStrength();
            var comparison = _comparisonSnapshotCacheService.GetOrBuild(safeEntries, normalizationMode, smoothingStrength);
            var mostVariable = comparison.TargetComparisons
                .OrderByDescending(item => item.VariabilityIndex)
                .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()?.TargetSound ?? "n/a";

            return
                "S: " + (string.IsNullOrWhiteSpace(safeRawNote) ? "No subjective note entered." : safeRawNote.Trim()) + Environment.NewLine +
                $"O: Recent attempts: {recent.Length}, average overall score {average:P0}, strongest target {strongest}, weakest target {weakest}." + Environment.NewLine +
                $"A: {comparison.ComparisonNarrative} The most variable target in the current review window is {mostVariable}." + Environment.NewLine +
                "P: Continue home drills on weaker targets, reinforce successful targets with mixed-context carryover, and monitor the most variable target for stabilization across the next sessions.";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build SOAP summary.", ex);
        }
    }

    private string BuildParentSummary(IReadOnlyList<ProgressEntry> entries)
    {
        var safeEntries = entries ?? Array.Empty<ProgressEntry>();

        try
        {
            var recent = safeEntries.OrderByDescending(e => e.Timestamp).Take(5).ToArray();
            if (recent.Length == 0)
            {
                return "No practice attempts were recorded yet. Start with short daily sessions and we will summarize progress after the first attempts.";
            }

            var avg = recent.Average(e => e.OverallScore);
            var topFocus = recent.OrderBy(e => e.OverallScore).First().TargetSound;
            var normalizationMode = _confidenceSettingsService.GetSessionComparisonNormalizationMode();
            var smoothingStrength = _confidenceSettingsService.GetSessionComparisonSmoothingStrength();
            var comparison = _comparisonSnapshotCacheService.GetOrBuild(safeEntries, normalizationMode, smoothingStrength);
            var mostVariable = comparison.TargetComparisons
                .OrderByDescending(item => item.VariabilityIndex)
                .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()?.TargetSound ?? topFocus;
            var stabilizing = comparison.TargetComparisons
                .OrderByDescending(item => item.PreviousSessionVariance - item.CurrentSessionVariance)
                .ThenBy(item => item.TargetSound, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault()?.TargetSound;

            var progressSentence = comparison.HasPreviousSession
                ? BuildParentTrendSentence(comparison)
                : "This review window is still building a baseline, so the main goal is steady short practice.";

            var stabilizationSentence = string.IsNullOrWhiteSpace(stabilizing)
                ? string.Empty
                : $" We saw the steadiest recent practice on '{stabilizing}'.";

            return
                $"Your child completed {recent.Length} recent practice attempts with an average score of {avg:P0}. " +
                $"The main focus right now is '{topFocus}', and '{mostVariable}' still benefits from the most steady repetition. " +
                progressSentence + stabilizationSentence +
                " Short, consistent practice sessions with calm repetition will help build more stable speech patterns between visits.";
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to build parent summary.", ex);
        }
    }

    private static string BuildParentTrendSentence(SessionComparisonSnapshot comparison)
    {
        if (!comparison.HasPreviousSession)
        {
            return "This review window is still building a baseline, so the main goal is steady short practice.";
        }

        if (comparison.OverallDelta >= 0.08)
        {
            return "Compared with the earlier session in this review window, overall performance is moving upward.";
        }

        if (comparison.OverallDelta <= -0.08)
        {
            return "Compared with the earlier session in this review window, performance was less steady, so gentle repetition will help rebuild consistency.";
        }

        return "Compared with the earlier session in this review window, performance stayed fairly steady overall.";
    }
}
