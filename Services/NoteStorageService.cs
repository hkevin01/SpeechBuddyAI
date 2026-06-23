using SpeechBuddyAI.Database;
using SpeechBuddyAI.Models;
using SQLite;

namespace SpeechBuddyAI.Services;

// ID: SVC-NOTES-001
// Purpose: Persists and queries SessionNote records in the local SQLite database.
public sealed class NoteStorageService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SQLiteAsyncConnection? _database;
    private bool _isInitialized;

    // ID: SVC-NOTES-002
    // Purpose: Saves a note, assigning current UTC time if not set.
    // Pre: note must not be null; SoapSummary or ParentSummary must be non-empty.
    public async Task SaveNoteAsync(SessionNote note)
    {
        if (note is null) throw new ArgumentNullException(nameof(note));

        if (string.IsNullOrWhiteSpace(note.SoapSummary) && string.IsNullOrWhiteSpace(note.ParentSummary))
            throw new ArgumentException("At least one summary must be present before saving.");

        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                if (note.SessionDateTicks == 0)
                    note.SessionDate = DateTimeOffset.UtcNow;

                if (note.Id == 0)
                    await Database.InsertAsync(note);
                else
                    await Database.UpdateAsync(note);
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException("Failed to save session note.", ex);
        }
    }

    // ID: SVC-NOTES-003
    // Purpose: Returns all notes ordered newest-first.
    public async Task<IReadOnlyList<SessionNote>> GetAllNotesAsync()
    {
        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                var notes = await Database.Table<SessionNote>()
                    .OrderByDescending(n => n.SessionDateTicks)
                    .ToListAsync();
                return SortBySessionDateDescending(notes);
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load session notes.", ex);
        }
    }

    // ID: SVC-NOTES-004
    // Purpose: Returns the most-recent N notes.
    public async Task<IReadOnlyList<SessionNote>> GetRecentNotesAsync(int take = 5)
    {
        try
        {
            await EnsureInitializedAsync();
            await _gate.WaitAsync();
            try
            {
                var notes = await Database.Table<SessionNote>()
                    .OrderByDescending(n => n.SessionDateTicks)
                    .Take(Math.Max(1, take))
                    .ToListAsync();
                return SelectMostRecent(notes, take);
            }
            finally { _gate.Release(); }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load recent notes.", ex);
        }
    }

    private async Task EnsureInitializedAsync()
    {
        if (_isInitialized) return;
        await _gate.WaitAsync();
        try
        {
            if (_isInitialized) return;
            _database = new SQLiteAsyncConnection(DbConstants.DatabasePath, DbConstants.Flags);
            await _database.CreateTableAsync<SessionNote>();
            await EnsureSchemaColumnsAsync(_database);
            _isInitialized = true;
        }
        finally { _gate.Release(); }
    }

    private SQLiteAsyncConnection Database =>
        _database ?? throw new InvalidOperationException("Database not initialized.");

    public static IReadOnlyList<SessionNote> SortBySessionDateDescending(IEnumerable<SessionNote> notes)
    {
        return (notes ?? Array.Empty<SessionNote>())
            .OrderByDescending(n => n.SessionDateTicks)
            .ToList();
    }

    public static IReadOnlyList<SessionNote> SelectMostRecent(IEnumerable<SessionNote> notes, int take)
    {
        return SortBySessionDateDescending(notes)
            .Take(Math.Max(1, take))
            .ToList();
    }

    private static async Task EnsureSchemaColumnsAsync(SQLiteAsyncConnection db)
    {
        var tableInfo = await db.QueryAsync<TableInfoRow>("PRAGMA table_info(SessionNote);");
        var existingColumns = new HashSet<string>(tableInfo.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
        var commands = new List<string>();

        if (!existingColumns.Contains("AssignmentSnapshotDateTicks"))
            commands.Add("ALTER TABLE SessionNote ADD COLUMN AssignmentSnapshotDateTicks INTEGER NOT NULL DEFAULT 0;");
        if (!existingColumns.Contains("AssignmentSelectionSummary"))
            commands.Add("ALTER TABLE SessionNote ADD COLUMN AssignmentSelectionSummary TEXT NOT NULL DEFAULT '';");
        if (!existingColumns.Contains("AssignmentSelectionDetails"))
            commands.Add("ALTER TABLE SessionNote ADD COLUMN AssignmentSelectionDetails TEXT NOT NULL DEFAULT '';");

        foreach (var command in commands)
        {
            await TryExecuteMigrationCommandAsync(db, command);
        }
    }

    private static async Task TryExecuteMigrationCommandAsync(SQLiteAsyncConnection db, string command)
    {
        try
        {
            await db.ExecuteAsync(command);
        }
        catch (SQLiteException ex) when (
            ex.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase) ||
            ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            // Existing databases may already include this column.
        }
    }

    private sealed class TableInfoRow
    {
        [Column("name")]
        public string Name { get; init; } = string.Empty;
    }
}
