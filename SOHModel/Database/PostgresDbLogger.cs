namespace SOHModel.Database;

using Dapper;
using Npgsql;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

/// <summary>
/// PostgreSQL-based database logger for flexible, type-safe logging of simulation data.
/// Inspired by SemiTruckRouteCacheManager but using PostgreSQL with Dapper for easy object mapping.
///
/// Usage:
///   var logger = new PostgresDbLogger()
///       .Register&lt;MyDataType&gt;("my_table_name")
///       .Register&lt;OtherType&gt;(); // Auto-generates table name
///
///   logger.Log(new MyDataType { ... });
///   logger.Log&lt;OtherType&gt;(new OtherType { ... });
/// </summary>
public class PostgresDbLogger : IDisposable
{
    private readonly string _connectionString;
    private readonly ConcurrentDictionary<Type, TypeMapping> _typeMappings = new();
    private readonly object _connectionLock = new();
    private bool _disposed;

    /// <summary>
    /// Initializes a new PostgreSQL logger using connection details from environment variables.
    /// Expects: DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD
    /// </summary>
    public PostgresDbLogger()
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? throw new InvalidOperationException("DB_NAME environment variable is required");
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? throw new InvalidOperationException("DB_USER environment variable is required");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD environment variable is required");

        _connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password}";
        Console.WriteLine($"Connecting to PostgreSQL database {_connectionString}");
    }

    /// <summary>
    /// Initializes a new PostgreSQL logger with an explicit connection string.
    /// </summary>
    public PostgresDbLogger(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Registers a type for logging with an automatically generated table name (lowercase type name).
    /// </summary>
    public PostgresDbLogger Register<T>() where T : class
    {
        var tableName = typeof(T).Name.ToLowerInvariant();
        return Register<T>(tableName);
    }

    /// <summary>
    /// Registers a type for logging with a specific table name.
    /// Creates the table if it doesn't exist.
    /// </summary>
    public PostgresDbLogger Register<T>(string tableName) where T : class
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be null or whitespace", nameof(tableName));

        var type = typeof(T);
        var properties = GetLoggableProperties(type);
        var mapping = new TypeMapping(tableName, properties);

        if (!_typeMappings.TryAdd(type, mapping))
            throw new InvalidOperationException($"Type {type.Name} is already registered");

        // Create table if it doesn't exist
        EnsureTableExists(mapping);

        return this;
    }

    /// <summary>
    /// Logs an object to the database. The object's type must be registered first.
    /// </summary>
    public void Log<T>(T obj) where T : class
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var type = typeof(T);
        if (!_typeMappings.TryGetValue(type, out var mapping))
            throw new InvalidOperationException($"Type {type.Name} is not registered. Call Register<{type.Name}>() first.");

        InsertObject(mapping, obj);
    }

    /// <summary>
    /// Logs an object to the database using runtime type detection.
    /// The object's type must be registered first.
    /// </summary>
    public void Log(object obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var type = obj.GetType();
        if (!_typeMappings.TryGetValue(type, out var mapping))
            throw new InvalidOperationException($"Type {type.Name} is not registered. Call Register<{type.Name}>() first.");

        InsertObject(mapping, obj);
    }

    /// <summary>
    /// Extracts loggable properties from a type (public instance properties with getters).
    /// </summary>
    private static PropertyInfo[] GetLoggableProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.GetGetMethod() != null)
            .ToArray();
    }

    /// <summary>
    /// Ensures the table exists in the database, creating it if necessary.
    /// </summary>
    private void EnsureTableExists(TypeMapping mapping)
    {
        lock (_connectionLock)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();

            var createTableSql = BuildCreateTableSql(mapping);
            connection.Execute(createTableSql);
        }
    }

    /// <summary>
    /// Builds a CREATE TABLE IF NOT EXISTS SQL statement for the given type mapping.
    /// </summary>
    private static string BuildCreateTableSql(TypeMapping mapping)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {mapping.TableName} (");
        sb.AppendLine("    id BIGSERIAL PRIMARY KEY,");

        var columnDefinitions = mapping.Properties.Select(p =>
        {
            var sqlType = GetPostgreSqlType(p.PropertyType);
            var nullable = IsNullable(p.PropertyType) ? "NULL" : "NOT NULL";
            return $"    {p.Name.ToLowerInvariant()} {sqlType} {nullable}";
        });

        sb.AppendLine(string.Join(",\n", columnDefinitions));
        sb.AppendLine(");");

        return sb.ToString();
    }

    /// <summary>
    /// Maps a C# type to a PostgreSQL type.
    /// </summary>
    private static string GetPostgreSqlType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        if (underlyingType == typeof(int) || underlyingType == typeof(short))
            return "INTEGER";
        if (underlyingType == typeof(long))
            return "BIGINT";
        if (underlyingType == typeof(float) || underlyingType == typeof(double))
            return "DOUBLE PRECISION";
        if (underlyingType == typeof(decimal))
            return "NUMERIC";
        if (underlyingType == typeof(bool))
            return "BOOLEAN";
        if (underlyingType == typeof(DateTime))
            return "TIMESTAMP";
        if (underlyingType == typeof(DateTimeOffset))
            return "TIMESTAMPTZ";
        if (underlyingType == typeof(Guid))
            return "UUID";
        if (underlyingType == typeof(byte[]))
            return "BYTEA";
        if (underlyingType == typeof(string))
            return "TEXT";

        // Default to JSONB for complex types
        return "JSONB";
    }

    /// <summary>
    /// Checks if a type is nullable (either Nullable&lt;T&gt; or reference type).
    /// </summary>
    private static bool IsNullable(Type type)
    {
        return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
    }

    /// <summary>
    /// Inserts an object into the database.
    /// </summary>
    private void InsertObject(TypeMapping mapping, object obj)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        connection.Open();

        var columnNames = string.Join(", ", mapping.Properties.Select(p => p.Name.ToLowerInvariant()));
        var paramNames = string.Join(", ", mapping.Properties.Select(p => $"@{p.Name}"));
        var sql = $"INSERT INTO {mapping.TableName} ({columnNames}) VALUES ({paramNames})";

        var parameters = new DynamicParameters();
        foreach (var prop in mapping.Properties)
        {
            var value = prop.GetValue(obj);
            parameters.Add($"@{prop.Name}", value);
        }

        connection.Execute(sql, parameters);
    }

    /// <summary>
    /// Tests the database connection.
    /// </summary>
    public bool TestConnection()
    {
        try
        {
            using var connection = new NpgsqlConnection(_connectionString);
            connection.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Internal class representing the mapping between a C# type and a database table.
    /// </summary>
    private class TypeMapping
    {
        public string TableName { get; }
        public PropertyInfo[] Properties { get; }

        public TypeMapping(string tableName, PropertyInfo[] properties)
        {
            TableName = tableName;
            Properties = properties;
        }
    }
}
