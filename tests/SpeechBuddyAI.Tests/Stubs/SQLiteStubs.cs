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
	private static readonly Dictionary<string, DatabaseState> Databases = new(StringComparer.Ordinal);
	private readonly string _databasePath;

	public SQLiteAsyncConnection(string databasePath, SQLiteOpenFlags flags)
	{
		_databasePath = databasePath ?? string.Empty;
	}

	public Task CreateTableAsync<T>() where T : new()
	{
		var db = GetDatabase();
		lock (db.Sync)
		{
			if (!db.Tables.ContainsKey(typeof(T)))
			{
				db.Tables[typeof(T)] = new List<object>();
			}
		}

		return Task.CompletedTask;
	}

	public Task<int> InsertAsync(object obj)
	{
		if (obj is null)
		{
			return Task.FromResult(0);
		}

		var db = GetDatabase();
		var type = obj.GetType();
		lock (db.Sync)
		{
			if (!db.Tables.TryGetValue(type, out var items))
			{
				items = new List<object>();
				db.Tables[type] = items;
			}

			items.Add(obj);
		}

		return Task.FromResult(1);
	}

	public Task<int> UpdateAsync(object obj)
	{
		return Task.FromResult(1);
	}

	public AsyncTableQuery<T> Table<T>() where T : new()
	{
		return new AsyncTableQuery<T>(GetTableSnapshot<T>);
	}

	public Task<List<T>> QueryAsync<T>(string query)
	{
		return Task.FromResult(new List<T>());
	}

	public Task<int> ExecuteAsync(string command)
	{
		return Task.FromResult(0);
	}

	private List<T> GetTableSnapshot<T>()
	{
		var db = GetDatabase();
		lock (db.Sync)
		{
			if (!db.Tables.TryGetValue(typeof(T), out var items))
			{
				return new List<T>();
			}

			return items.OfType<T>().ToList();
		}
	}

	private DatabaseState GetDatabase()
	{
		lock (Databases)
		{
			if (!Databases.TryGetValue(_databasePath, out var state))
			{
				state = new DatabaseState();
				Databases[_databasePath] = state;
			}

			return state;
		}
	}

	private sealed class DatabaseState
	{
		public object Sync { get; } = new();
		public Dictionary<Type, List<object>> Tables { get; } = new();
	}
}

public sealed class AsyncTableQuery<T>
{
	private readonly Func<List<T>> _source;
	private IEnumerable<T>? _query;

	public AsyncTableQuery(Func<List<T>> source)
	{
		_source = source;
	}

	private IEnumerable<T> Current => _query ?? _source();

	public AsyncTableQuery<T> OrderByDescending<TKey>(Func<T, TKey> keySelector)
	{
		_query = Current.OrderByDescending(keySelector);
		return this;
	}

	public AsyncTableQuery<T> OrderBy<TKey>(Func<T, TKey> keySelector)
	{
		_query = Current.OrderBy(keySelector);
		return this;
	}

	public AsyncTableQuery<T> Take(int n)
	{
		_query = Current.Take(Math.Max(0, n));
		return this;
	}

	public Task<List<T>> ToListAsync()
	{
		return Task.FromResult(Current.ToList());
	}
}
