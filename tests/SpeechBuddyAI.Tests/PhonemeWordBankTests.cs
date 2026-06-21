using SpeechBuddyAI.Services;

namespace SpeechBuddyAI.Tests;

public sealed class PhonemeWordBankTests
{
    [Theory]
    [InlineData("r", null)]
    [InlineData("s", "initial")]
    [InlineData("sh", "final")]
    [InlineData("bl", "initial")]
    public void GetWords_KnownTargets_ReturnNonEmptyList(string target, string? position)
    {
        var bank = new PhonemeWordBankService();
        var words = bank.GetWords(target, position);
        Assert.NotEmpty(words);
    }

    [Fact]
    public void GetWords_UnknownTarget_ReturnsFallback()
    {
        var bank = new PhonemeWordBankService();
        var words = bank.GetWords("zzz_unknown");
        Assert.NotEmpty(words);
    }

    [Fact]
    public void GetWords_PositionFilterReturnsSubset()
    {
        var bank = new PhonemeWordBankService();
        var initial = bank.GetWords("r", "initial");
        var all = bank.GetWords("r");

        // All initial words should be included in the all-positions list
        Assert.All(initial, w => Assert.Contains(w, all));
    }

    [Fact]
    public void KnownTargets_ContainsCorePhonemes()
    {
        var bank = new PhonemeWordBankService();
        var targets = bank.KnownTargets();

        foreach (var expected in new[] { "r", "l", "s", "sh", "ch", "f", "k" })
            Assert.Contains(expected, targets);
    }
}
