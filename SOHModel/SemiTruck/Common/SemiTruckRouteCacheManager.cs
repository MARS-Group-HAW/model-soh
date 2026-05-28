namespace SOHModel.SemiTruck.Common;

using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
/// Manages caching of precomputed routes between edge pairs using a local SQLite database.
/// Routes are stored and retrieved based on vehicle constraints and route characteristics.
/// This allows faster access to frequently used or costly-to-compute routes in later simulation runs.
/// </summary>
public class SemiTruckRouteCacheManager
{
    private readonly string _dbPath;
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Initializes the route cache manager and opens a connection to the SQLite database.
    /// If the database file does not exist, it will be created and initialized with the required schema.
    /// </summary>
    /// <param name="dbPath">Path to the SQLite database file (default is "route_cache.db").</param>
    public SemiTruckRouteCacheManager(string dbPath = "route_cache.db")
    {
        _dbPath = dbPath;
        bool createNew = !File.Exists(dbPath);

        _connection = new SqliteConnection($"Data Source={_dbPath}");
        _connection.Open();

        if (createNew)
            InitDb();
    }

    /// <summary>
    /// Initializes the database schema by creating the routes table if it does not exist.
    /// </summary>
    private void InitDb()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS routes (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                start_edge TEXT,
                end_edge TEXT,
                edge_ids TEXT,
                max_weight REAL,
                max_height REAL,
                max_width REAL,
                max_length REAL,
                max_incline INTEGER,
                suboptimal INTEGER
            );
        ";
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Constructs a unique key for identifying a route between two edges.
    /// Not used in SQL logic directly, but could be used for in-memory structures.
    /// </summary>
    private string GetKey(string startEdge, string endEdge)
    {
        return $"{startEdge}|{endEdge}";
    }

    /// <summary>
    /// Attempts to retrieve a route from the database that satisfies the given vehicle constraints.
    /// Routes are sorted in order of optimality and size to prefer better matches.
    /// </summary>
    /// <param name="startEdge">Start edge ID</param>
    /// <param name="endEdge">End edge ID</param>
    /// <param name="weight">Truck weight</param>
    /// <param name="height">Truck height</param>
    /// <param name="width">Truck width</param>
    /// <param name="length">Truck length</param>
    /// <param name="maxIncline">Maximum allowed road incline</param>
    /// <param name="edgeIds">Output: List of edge IDs if a route is found</param>
    /// <param name="wasSuboptimal">Output: Whether the route is suboptimal (due to limited constraints)</param>
    /// <param name="constraintsMatchExactly">Output: Whether the route constraints exactly match the query</param>
    /// <returns>True if a matching route was found; otherwise false</returns>
    public bool TryGetRoute(
        string startEdge,
        string endEdge,
        double weight,
        double height,
        double width,
        double length,
        int maxIncline,
        out List<string> edgeIds,
        out bool wasSuboptimal,
        out bool constraintsMatchExactly)
    {
        edgeIds = null;
        wasSuboptimal = false;
        constraintsMatchExactly = false;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT edge_ids, max_weight, max_height, max_width, max_length, max_incline, suboptimal
            FROM routes 
            WHERE start_edge = @start AND end_edge = @end;
            ORDER BY suboptimal ASC, max_weight ASC, max_height ASC, max_width ASC, max_length ASC, max_incline ASC;
        ";
        cmd.Parameters.AddWithValue("@start", startEdge);
        cmd.Parameters.AddWithValue("@end", endEdge);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            // Read route constraints from the database
            double maxWeight = reader.GetDouble(1);
            double maxHeight = reader.GetDouble(2);
            double maxWidth = reader.GetDouble(3);
            double maxLength = reader.GetDouble(4);
            int maxRouteIncline = reader.GetInt32(5);
            bool suboptimal = reader.GetInt32(6) == 1;

            // Check whether the current route satisfies the query constraints
            bool fits =
                weight <= maxWeight &&
                height <= maxHeight &&
                width <= maxWidth &&
                length <= maxLength &&
                maxIncline <= maxRouteIncline;

            if (!fits)
                continue;

            // Check for exact constraint match
            bool exactMatch =
                Math.Abs(weight - maxWeight) < 0.001 &&
                Math.Abs(height - maxHeight) < 0.001 &&
                Math.Abs(width - maxWidth) < 0.001 &&
                Math.Abs(length - maxLength) < 0.001 &&
                maxIncline == maxRouteIncline;

            constraintsMatchExactly = exactMatch;
            wasSuboptimal = suboptimal;

            // Deserialize the list of edge IDs
            string edgeJson = reader.GetString(0);
            edgeIds = JsonSerializer.Deserialize<List<string>>(edgeJson);
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// Stores a new route in the database, or replaces an existing one with the same start and end edge.
    /// Routes are stored with associated vehicle constraint metadata and whether the route is suboptimal.
    /// </summary>
    public void StoreRoute(
        string startEdge,
        string endEdge,
        List<string> edgeIds,
        double maxWeight,
        double maxHeight,
        double maxWidth,
        double maxLength,
        int maxIncline,
        bool wasSuboptimal)
    {
        string edgeJson = JsonSerializer.Serialize(edgeIds);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT OR REPLACE INTO routes 
            (start_edge, end_edge, edge_ids, 
             max_weight, max_height, max_width, max_length, max_incline, suboptimal) 
            VALUES 
            (@start, @end, @edges, 
             @weight, @height, @width, @length, @incline, @suboptimal);
        ";

        cmd.Parameters.AddWithValue("@start", startEdge);
        cmd.Parameters.AddWithValue("@end", endEdge);
        cmd.Parameters.AddWithValue("@edges", edgeJson);
        cmd.Parameters.AddWithValue("@weight", maxWeight);
        cmd.Parameters.AddWithValue("@height", maxHeight);
        cmd.Parameters.AddWithValue("@width", maxWidth);
        cmd.Parameters.AddWithValue("@length", maxLength);
        cmd.Parameters.AddWithValue("@incline", maxIncline);
        cmd.Parameters.AddWithValue("@suboptimal", wasSuboptimal ? 1 : 0);

        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Closes the underlying SQLite connection when the cache manager is no longer needed.
    /// </summary>
    public void Close()
    {
        _connection?.Close();
    }
}