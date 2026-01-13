namespace SOHModel.Database;

using Dapper;
using Npgsql;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
    /// <summary>
    /// Singleton instance for convenient access throughout the application.
    /// Set this in your main entry point after initialization.
    /// </summary>
    public static PostgresDbLogger? Instance { get; set; }

    private readonly string _connectionString;
    private readonly ConcurrentDictionary<Type, TypeMapping> _typeMappings = new();
    private readonly object _connectionLock = new();
    private readonly ConcurrentQueue<LogEntry> _logQueue = new();
    private readonly SemaphoreSlim _queueSignal = new(0);
    private readonly Task _backgroundTask;
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private readonly int _batchSize;
    private readonly TimeSpan _batchTimeout;
    private bool _disposed;

    /// <summary>
    /// Initializes a new PostgreSQL logger using connection details from environment variables.
    /// Expects: DB_HOST, DB_PORT, DB_NAME, DB_USER, DB_PASSWORD
    /// </summary>
    /// <param name="batchSize">Number of records to batch before writing (default: 100)</param>
    /// <param name="batchTimeout">Maximum time to wait before flushing batch (default: 1 second)</param>
    public PostgresDbLogger(int batchSize = 100, TimeSpan? batchTimeout = null)
    {
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "5432";
        var database = Environment.GetEnvironmentVariable("DB_NAME") ?? throw new InvalidOperationException("DB_NAME environment variable is required");
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? throw new InvalidOperationException("DB_USER environment variable is required");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD") ?? throw new InvalidOperationException("DB_PASSWORD environment variable is required");

        _connectionString = $"Host={host};Port={port};Database={database};Username={user};Password={password};SearchPath={database},public";
        _batchSize = batchSize;
        _batchTimeout = batchTimeout ?? TimeSpan.FromSeconds(1);
        Console.WriteLine($"Connecting to PostgreSQL database {_connectionString}");

        _backgroundTask = Task.Run(ProcessQueueAsync);
    }

    /// <summary>
    /// Initializes a new PostgreSQL logger with an explicit connection string.
    /// </summary>
    /// <param name="connectionString">The PostgreSQL connection string</param>
    /// <param name="batchSize">Number of records to batch before writing (default: 100)</param>
    /// <param name="batchTimeout">Maximum time to wait before flushing batch (default: 1 second)</param>
    public PostgresDbLogger(string connectionString, int batchSize = 100, TimeSpan? batchTimeout = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _batchSize = batchSize;
        _batchTimeout = batchTimeout ?? TimeSpan.FromSeconds(1);

        _backgroundTask = Task.Run(ProcessQueueAsync);
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
    /// This is a non-blocking operation that queues the object for batched insertion.
    /// </summary>
    public void Log<T>(T obj) where T : class
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var type = typeof(T);
        if (!_typeMappings.TryGetValue(type, out var mapping))
            throw new InvalidOperationException($"Type {type.Name} is not registered. Call Register<{type.Name}>() first.");

        _logQueue.Enqueue(new LogEntry(mapping, obj));
        _queueSignal.Release();
    }

    /// <summary>
    /// Logs an object to the database using runtime type detection.
    /// The object's type must be registered first.
    /// This is a non-blocking operation that queues the object for batched insertion.
    /// </summary>
    public void Log(object obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        var type = obj.GetType();
        if (!_typeMappings.TryGetValue(type, out var mapping))
            throw new InvalidOperationException($"Type {type.Name} is not registered. Call Register<{type.Name}>() first.");

        _logQueue.Enqueue(new LogEntry(mapping, obj));
        _queueSignal.Release();
    }

    /// <summary>
    /// Flushes all pending log entries to the database immediately.
    /// Blocks until all queued entries are written.
    /// </summary>
    public async Task FlushAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        _logQueue.Enqueue(new LogEntry(null!, null!) { FlushCompletionSource = tcs });
        _queueSignal.Release();
        await tcs.Task;
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
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS \"{mapping.TableName}\" (");
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
    /// Background task that processes the log queue and writes batches to the database.
    /// </summary>
    private async Task ProcessQueueAsync()
    {
        var batch = new List<LogEntry>();

        while (!_cancellationTokenSource.Token.IsCancellationRequested)
        {
            try
            {
                // Wait for items or timeout
                await _queueSignal.WaitAsync(_batchTimeout, _cancellationTokenSource.Token);

                // Collect batch
                while (batch.Count < _batchSize && _logQueue.TryDequeue(out var entry))
                {
                    // Check for flush marker
                    if (entry.Mapping == null)
                    {
                        // Flush current batch first
                        if (batch.Count > 0)
                        {
                            await WriteBatchAsync(batch);
                            batch.Clear();
                        }
                        entry.FlushCompletionSource?.SetResult(true);
                        continue;
                    }

                    batch.Add(entry);
                }

                // Write batch if we have items
                if (batch.Count > 0)
                {
                    await WriteBatchAsync(batch);
                    batch.Clear();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing log queue: {ex}");
            }
        }

        // Final flush on shutdown
        while (_logQueue.TryDequeue(out var entry))
        {
            if (entry.Mapping != null)
                batch.Add(entry);
        }

        if (batch.Count > 0)
        {
            await WriteBatchAsync(batch);
        }
    }

    /// <summary>
    /// Writes a batch of log entries to the database in a single transaction.
    /// </summary>
    private async Task WriteBatchAsync(List<LogEntry> batch)
    {
        if (batch.Count == 0) return;

        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Group by table for efficient insertion
            var groupedByTable = batch.GroupBy(e => e.Mapping.TableName);

            foreach (var group in groupedByTable)
            {
                var mapping = group.First().Mapping;
                var objects = group.Select(e => e.Object).ToList();

                var columnNames = string.Join(", ", mapping.Properties.Select(p => p.Name.ToLowerInvariant()));
                var valuePlaceholders = new List<string>();
                var parameters = new DynamicParameters();

                for (int i = 0; i < objects.Count; i++)
                {
                    var paramNames = string.Join(", ", mapping.Properties.Select(p => $"@{p.Name}{i}"));
                    valuePlaceholders.Add($"({paramNames})");

                    foreach (var prop in mapping.Properties)
                    {
                        var value = prop.GetValue(objects[i]);
                        parameters.Add($"@{prop.Name}{i}", value);
                    }
                }

                var sql = $"INSERT INTO \"{mapping.TableName}\" ({columnNames}) VALUES {string.Join(", ", valuePlaceholders)}";
                await connection.ExecuteAsync(sql, parameters, transaction);
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            Console.WriteLine($"Error writing batch to database: {ex}");
            throw;
        }
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

            // Signal shutdown and wait for background task
            _cancellationTokenSource.Cancel();
            try
            {
                _backgroundTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error waiting for background task to complete: {ex}");
            }

            _cancellationTokenSource.Dispose();
            _queueSignal.Dispose();
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

    /// <summary>
    /// Internal class representing a queued log entry.
    /// </summary>
    private class LogEntry
    {
        public TypeMapping Mapping { get; }
        public object Object { get; }
        public TaskCompletionSource<bool>? FlushCompletionSource { get; init; }

        public LogEntry(TypeMapping mapping, object obj)
        {
            Mapping = mapping;
            Object = obj;
        }
    }
}
