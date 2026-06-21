// Lightweight attribute stubs so ProgressEntry and SessionNote compile
// in the plain net9.0 test project without pulling in sqlite-net-pcl.
namespace SQLite;

[AttributeUsage(AttributeTargets.Property)] public sealed class PrimaryKeyAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class AutoIncrementAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class IndexedAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class IgnoreAttribute : Attribute { }
[AttributeUsage(AttributeTargets.Property)] public sealed class ColumnAttribute(string name) : Attribute { public string Name { get; } = name; }
