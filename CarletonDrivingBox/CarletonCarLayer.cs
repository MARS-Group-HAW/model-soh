using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using ServiceStack;
using SOHModel.Car.Model;

namespace SOHCarletonDrivingBox;

/// <summary>
/// Carleton campus car layer. Skips init spawn when agent config has no schedule file
/// (scheduler-only setups use <see cref="CarletonCarDriverSchedulerLayer"/> instead).
/// Also manages temporary edge blocks from vehicle breakdowns (scenario 11).
/// </summary>
public class CarletonCarLayer : CarLayer
{
    private readonly object _breakdownSync = new();
    private readonly object _breakdownLogSync = new();
    private readonly Dictionary<ISpatialEdge, BreakdownBlock> _activeBlocks = new();
    private bool _breakdownLogHeaderWritten;

    /// <summary>
    /// Path for <c>breakdowns.csv</c> (set from <see cref="Program"/> before the run).
    /// Empty disables file logging; console lines still print.
    /// </summary>
    public static string BreakdownLogPath { get; set; } = "";

    /// <summary>
    /// Per-tick chance that a moving car breaks down (0 disables the feature).
    /// Scenario 11 uses a small value; scenarios 01–10 leave this at 0.
    /// </summary>
    [PropertyDescription(Name = "breakdownProbabilityPerTick")]
    public double BreakdownProbabilityPerTick { get; set; }

    /// <summary>
    /// How long a broken-down car blocks its edge before being cleared (seconds).
    /// Default matches SemiTruck roadside clearance (~41 min ADAC-style hold).
    /// </summary>
    [PropertyDescription(Name = "breakdownClearSeconds")]
    public double BreakdownClearSeconds { get; set; } = 2460;

    /// <summary>
    /// Edges currently soft-blocked due to breakdowns.
    /// </summary>
    public IReadOnlyCollection<ISpatialEdge> RemovedEdges
    {
        get
        {
            lock (_breakdownSync)
                return _activeBlocks.Keys.ToList();
        }
    }

    public bool IsEdgeBlocked(ISpatialEdge edge)
    {
        if (edge == null)
            return false;
        // Parking aisles are often single-path; soft-blocking them orphans cars (edge=-1).
        // Still track parking breakdowns for clearance / logging, but do not block routing.
        if (IsParkingLotEdge(edge))
            return false;
        lock (_breakdownSync)
            return _activeBlocks.ContainsKey(edge);
    }

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        var initData = new LayerInitData(layerInitData.Context)
        {
            Container = layerInitData.Container,
            LayerInitConfig = layerInitData.LayerInitConfig,
            AgentInitConfigs = layerInitData.AgentInitConfigs
                .Where(config => !string.IsNullOrEmpty(config.File))
                .ToList()
        };

        return base.InitLayer(initData, registerAgentHandle, unregisterAgent);
    }

    public override void PreTick()
    {
        ClearExpiredBreakdowns();
    }

    /// <summary>
    /// Soft-block <paramref name="edge"/> until clearance (no <c>RemoveEdge</c>) and notify other
    /// drivers. Campus parking / OSM edges must stay in the graph: MARS <c>RemoveEdge</c> drops
    /// node adjacency, and SemiTruck-style <c>Edges.Add</c> restore does not rewire it — that
    /// crashed scenario 11 ~clearSeconds later with "outgoing edge does not exist".
    /// </summary>
    public bool RegisterBreakdown(CarletonCarDriver brokenDriver, ISpatialEdge edge)
    {
        if (Environment == null || edge == null || brokenDriver == null)
            return false;

        var isParking = IsParkingLotEdge(edge);
        List<CarletonCarDriver> toNotify;
        lock (_breakdownSync)
        {
            var clearTick = Context.CurrentTick +
                            Math.Max(1, (long)Math.Ceiling(BreakdownClearSeconds));

            if (_activeBlocks.TryGetValue(edge, out var existing))
            {
                if (clearTick > existing.ClearTick)
                    existing.ClearTick = clearTick;
                existing.BrokenDrivers.Add(brokenDriver);
            }
            else
            {
                _activeBlocks[edge] = new BreakdownBlock(clearTick, brokenDriver);
                // Do not Console.WriteLine here — MARS ProgressBar redraws via cursor
                // position; mid-run prints scramble the bar when globals.console is true.
            }

            // Only notify / soft-block for main roads. Parking soft-blocks strand the lot.
            toNotify = isParking
                ? new List<CarletonCarDriver>()
                : Driver.Values.OfType<CarletonCarDriver>()
                    .Where(d => !ReferenceEquals(d, brokenDriver))
                    .ToList();
        }

        AppendBreakdownEvent(brokenDriver, edge, Context.CurrentTick);

        foreach (var driver in toNotify)
        {
            try
            {
                driver.NotifyEdgeBlocked(edge);
            }
            catch
            {
                // Keep going — a single notify failure must not abort the run or spam the console.
            }
        }

        return true;
    }

    /// <summary>
    /// Append one row to <c>breakdowns.csv</c>: time_s, lot, agent_id, edge_id.
    /// </summary>
    private void AppendBreakdownEvent(CarletonCarDriver driver, ISpatialEdge edge, long tick)
    {
        var path = BreakdownLogPath;
        if (string.IsNullOrWhiteSpace(path))
            return;

        var agentId = driver.ID.ToString();
        var lot = driver.ParkingLot ?? "";
        var edgeId = EdgeOsmId(edge);
        var line = string.Format(
            CultureInfo.InvariantCulture,
            "{0},{1},{2},{3}",
            tick,
            EscapeCsv(lot),
            EscapeCsv(agentId),
            EscapeCsv(edgeId));

        lock (_breakdownLogSync)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var needHeader = !_breakdownLogHeaderWritten &&
                             (!File.Exists(path) || new FileInfo(path).Length == 0);
            using var writer = new StreamWriter(path, append: true);
            if (needHeader)
                writer.WriteLine("time_s,lot,agent_id,edge_id");
            _breakdownLogHeaderWritten = true;
            writer.WriteLine(line);
        }
    }

    private static string EdgeOsmId(ISpatialEdge edge)
    {
        if (edge == null)
            return "";
        try
        {
            if (edge.Attributes != null && edge.Attributes.ContainsKey("osmid"))
            {
                var raw = edge.Attributes["osmid"];
                if (raw is System.Collections.IEnumerable enumerable and not string)
                {
                    foreach (var item in enumerable)
                        return item?.ToString() ?? SafeEdgeKey(edge);
                }

                var osmId = raw?.ToString() ?? "";
                if (osmId.Length > 0 && osmId[0] == '[')
                {
                    var inner = osmId.Trim('[', ']');
                    var first = inner.Split(',')[0].Trim();
                    if (!string.IsNullOrEmpty(first))
                        return first;
                }
                else if (!string.IsNullOrEmpty(osmId))
                {
                    return osmId;
                }
            }

            return SafeEdgeKey(edge);
        }
        catch
        {
            return "";
        }
    }

    private static string SafeEdgeKey(ISpatialEdge edge)
    {
        try
        {
            return edge.GetId()?.ToString() ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Campus parking aisles / synthetic connectors (osmid like <c>carleton_parking_p7_svc_*</c>).
    /// </summary>
    internal static bool IsParkingLotEdge(ISpatialEdge edge)
    {
        if (edge?.Attributes == null)
            return false;

        try
        {
            if (edge.Attributes.ContainsKey("carleton_parking"))
            {
                var flag = edge.Attributes["carleton_parking"];
                if (flag is bool b && b)
                    return true;
                if (flag != null && bool.TryParse(flag.ToString(), out var parsed) && parsed)
                    return true;
                if (flag != null && flag.ToString()?.Length > 0)
                    return true;
            }

            if (edge.Attributes.ContainsKey("parking_lot"))
            {
                var lot = edge.Attributes["parking_lot"]?.ToString();
                if (!string.IsNullOrWhiteSpace(lot))
                    return true;
            }

            var osm = EdgeOsmId(edge);
            if (osm.StartsWith("carleton_parking_", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            // ignore
        }

        return false;
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    /// <summary>
    /// Drop expired soft-blocks and finish broken agents. Never mutates the drive graph.
    /// </summary>
    private void ClearExpiredBreakdowns()
    {
        List<(ISpatialEdge Edge, BreakdownBlock Block)> expired;
        lock (_breakdownSync)
        {
            if (_activeBlocks.Count == 0)
                return;

            var now = Context.CurrentTick;
            expired = _activeBlocks
                .Where(kv => now >= kv.Value.ClearTick)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();

            foreach (var (edge, _) in expired)
                _activeBlocks.Remove(edge);
        }

        foreach (var (edge, block) in expired)
        {
            foreach (var driver in block.BrokenDrivers.ToList())
            {
                if (driver == null)
                    continue;
                try
                {
                    driver.FinishBreakdownClearance();
                }
                catch
                {
                    // Clearance must not abort the sim or spam the progress console.
                }
            }
        }
    }

    private sealed class BreakdownBlock
    {
        public BreakdownBlock(long clearTick, CarletonCarDriver driver)
        {
            ClearTick = clearTick;
            BrokenDrivers.Add(driver);
        }

        public long ClearTick { get; set; }
        public HashSet<CarletonCarDriver> BrokenDrivers { get; } = new();
    }
}
