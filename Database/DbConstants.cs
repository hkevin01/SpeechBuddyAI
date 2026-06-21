using SQLite;

namespace SpeechBuddyAI.Database;

public static class DbConstants
{
    public const string DatabaseFilename = "speechbuddy.db3";

    public const SQLiteOpenFlags Flags =
        SQLiteOpenFlags.ReadWrite |
        SQLiteOpenFlags.Create |
        SQLiteOpenFlags.SharedCache;

    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, DatabaseFilename);
}
