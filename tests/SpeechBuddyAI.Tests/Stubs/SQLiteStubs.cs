// Lightweight attribute stubs so ProgressEntry and SessionNote compile
// in the plain net9.0 test project without pulling in sqlite-net-pcl.
namespace SQLite;

[AttributeUsage(AttributeTargets.Property)] public sealed class PrimaryKeyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class AutoIncrementAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class IndexedAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class IgnoreAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class ColumnAttribute(string name) : Attribute { public string Name { get; } = name; }

[Flags]
public enum SQLiteOpenFlags
{
	ReadWrite = 1,
	Create = 2,
	SharedCache = 4
}

public sealed class SQLiteException : Exception
{
	public SQLiteException(string message) : base(message)
	{
	}
}

public sealed class SQLiteAsyncConnection
{
	public SQLiteAsyncConnection(string databasePath, SQLiteOpenFlags flags)
	{
	}

	public Task CreateTableAsync<T>() where T : new()
	{
		return Task.CompletedTask;
	}

	public Task<int> InsertAsync(object obj)
	{
		return Task.FromResult(1);
	}

	public Task<int> UpdateAsync(object obj)
	{
		return Task.FromResult(1);
	}

	public AsyncTableQuery<T> Table<T>() where T : new()
	{
		return new AsyncTableQuery<T>();
	}

	public Task<List<T>> QueryAsync<T>(string query)
	{
		return Task.FromResult(new List<T>());
	}

	public Task<int> ExecuteAsync(string command)
	{
		return Task.FromResult(0);
	}
}

public sealed class AsyncTableQuery<T>
{
	public AsyncTableQuery<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
	{
		return this;
	}

	public AsyncTableQuery<T> OrderBy<TKey>(Func<T, TKey> keySelector)
	{
		return this;
	}

	public AsyncTableQuery<T> Take(int n)
	{
		return this;
	}

	public Task<List<T>> ToListAsync()
	{
		return Task.FromResult(new List<T>());
	}
}
