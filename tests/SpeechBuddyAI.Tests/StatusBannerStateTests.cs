using SpeechBuddyAI.Models;
using SpeechBuddyAI.Pages.ViewModels;

namespace SpeechBuddyAI.Tests;

public sealed class StatusBannerStateTests
{
    [Fact]
    public void Success_ReturnsSuccessTone()
    {
        var state = StatusBannerState.Success("Saved.");

        Assert.Equal("Saved.", state.Message);
        Assert.Equal(StatusBannerTone.Success, state.Tone);
    }

    [Fact]
    public void Warning_ReturnsWarningTone()
    {
        var state = StatusBannerState.Warning("Something failed.");

        Assert.Equal("Something failed.", state.Message);
        Assert.Equal(StatusBannerTone.Warning, state.Tone);
    }

    [Fact]
    public void FromAssignment_ReviewRequired_ReturnsReviewTone()
    {
        var assignment = new HomeAssignment
        {
            ReviewRequired = true,
            UncertaintyBudgetSummary = "Assignment flagged for clinician review."
        };

        var state = StatusBannerState.FromAssignment(assignment);

        Assert.Equal(StatusBannerTone.ReviewRequired, state.Tone);
        Assert.Contains("review", state.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromAssignment_ClearOutcome_ReturnsSuccessTone()
    {
        var assignment = new HomeAssignment
        {
            ReviewRequired = false,
            UncertaintyBudgetSummary = "Within cap."
        };

        var state = StatusBannerState.FromAssignment(assignment);

        Assert.Equal(StatusBannerTone.Success, state.Tone);
        Assert.Contains("generated successfully", state.Message, StringComparison.OrdinalIgnoreCase);
    }
}
