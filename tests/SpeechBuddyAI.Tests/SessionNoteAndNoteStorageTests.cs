using SpeechBuddyAI.Models;
using SpeechBuddyAI.Services;
using SQLite;

namespace SpeechBuddyAI.Tests;

public sealed class SessionNoteAndNoteStorageTests
{
    [Fact]
    public void SessionDate_SetterStoresUtcTicks_AndGetterReturnsUtcOffset()
    {
        var source = new DateTimeOffset(2026, 6, 1, 14, 0, 0, TimeSpan.FromHours(-4));
        var note = new SessionNote();

        note.SessionDate = source;

        Assert.Equal(source.UtcTicks, note.SessionDateTicks);
        Assert.Equal(TimeSpan.Zero, note.SessionDate.Offset);
        Assert.Equal(source.ToUniversalTime(), note.SessionDate);
    }

    [Fact]
    public void SessionNote_SchemaAttributes_ArePresent()
    {
        var idProperty = typeof(SessionNote).GetProperty(nameof(SessionNote.Id));
        var ticksProperty = typeof(SessionNote).GetProperty(nameof(SessionNote.SessionDateTicks));
        var sessionDateProperty = typeof(SessionNote).GetProperty(nameof(SessionNote.SessionDate));
        var assignmentDateProperty = typeof(SessionNote).GetProperty(nameof(SessionNote.AssignmentSnapshotDate));

        Assert.NotNull(idProperty);
        Assert.NotNull(ticksProperty);
        Assert.NotNull(sessionDateProperty);
        Assert.NotNull(assignmentDateProperty);

        Assert.NotNull(idProperty!.GetCustomAttributes(typeof(PrimaryKeyAttribute), inherit: true).FirstOrDefault());
        Assert.NotNull(ticksProperty!.GetCustomAttributes(typeof(IndexedAttribute), inherit: true).FirstOrDefault());
        Assert.NotNull(sessionDateProperty!.GetCustomAttributes(typeof(IgnoreAttribute), inherit: true).FirstOrDefault());
        Assert.NotNull(assignmentDateProperty!.GetCustomAttributes(typeof(IgnoreAttribute), inherit: true).FirstOrDefault());
    }

    [Fact]
    public void SortBySessionDateDescending_OrdersNewestFirst()
    {
        var older = new SessionNote { SessionDate = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), SoapSummary = "older" };
        var newer = new SessionNote { SessionDate = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), SoapSummary = "newer" };

        var sorted = NoteStorageService.SortBySessionDateDescending([older, newer]);

        Assert.Equal("newer", sorted[0].SoapSummary);
        Assert.Equal("older", sorted[1].SoapSummary);
    }

    [Fact]
    public void SelectMostRecent_RespectsTakeCount()
    {
        var a = new SessionNote { SessionDate = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero), SoapSummary = "a" };
        var b = new SessionNote { SessionDate = new DateTimeOffset(2026, 1, 2, 10, 0, 0, TimeSpan.Zero), SoapSummary = "b" };
        var c = new SessionNote { SessionDate = new DateTimeOffset(2026, 1, 3, 10, 0, 0, TimeSpan.Zero), SoapSummary = "c" };

        var mostRecent = NoteStorageService.SelectMostRecent([a, b, c], 2);

        Assert.Equal(2, mostRecent.Count);
        Assert.Equal("c", mostRecent[0].SoapSummary);
        Assert.Equal("b", mostRecent[1].SoapSummary);
    }
}
