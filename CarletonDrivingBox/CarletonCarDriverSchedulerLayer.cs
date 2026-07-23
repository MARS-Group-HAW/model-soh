using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Layers;
using SOHModel.Car.Model;

namespace SOHCarletonDrivingBox;

/// <summary>
/// Campus evacuation scheduler for <see cref="CarletonCarDriver"/> agents.
/// </summary>
/// <remarks>
/// Stock <c>CarDriverSchedulerLayer</c> (SOHModel) reads spawn coordinates from WKT
/// <c>source</c>/<c>destination</c> columns via <c>SourceGeometry</c>/<c>TargetGeometry</c>
/// and samples a random point in that geometry. Carleton's schedule CSV uses explicit
/// <c>startLat</c>/<c>startLon</c>/<c>destLat</c>/<c>destLon</c> columns (one anchor per
/// lot). To spread cars across the parking lot, this layer maps each schedule row to a
/// lot via the anchor and picks a random interior aisle node from
/// <c>resources/parking_lot_spawn_candidates.json</c>.
/// </remarks>
public class CarletonCarDriverSchedulerLayer : SchedulerLayer
{
    private readonly CarletonCarLayer _carLayer;
    private readonly HashSet<Guid> _unregistered = new();
    // CarLayer.Driver is a plain Dictionary; MARS ticks agents in parallel by default.
    private readonly object _driverSync = new();
    private readonly object _spawnSync = new();
    private Dictionary<string, LotSpawnPool>? _spawnPools;
    private bool _spawnPoolsLoaded;

    public CarletonCarDriverSchedulerLayer(CarletonCarLayer carLayer)
    {
        _carLayer = carLayer;
    }

    private static void Register(ILayer layer, ITickClient tickClient)
    {
        // CarletonCarDriver's constructor already registers via the register callback.
    }

    /// <summary>
    /// Remove finished drivers from the layer and MARS tick list (TrainSchedulerLayer pattern).
    /// Stock CarDriverSchedulerLayer passes a no-op here, which leaks agents after GoalReached.
    /// </summary>
    private void UnregisterDriver(ILayer layer, ITickClient tickClient)
    {
        if (tickClient is not CarletonCarDriver driver)
            return;

        // CarletonCarDriver calls unregister from both Notify(GoalReached) and Tick(); guard double-removal.
        lock (_driverSync)
        {
            if (!_unregistered.Add(driver.ID))
                return;

            _carLayer.Driver.Remove(driver.ID);
        }

        UnregisterAgent(layer, tickClient);
    }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        var required = new[] { "startLat", "startLon", "destLat", "destLon" };
        foreach (var field in required)
        {
            if (!dataRow.Data.TryGetValue(field, out _))
            {
                Console.WriteLine($"[Carleton scheduler] Missing {field}; skipping row.");
                return;
            }
        }

        try
        {
            var startLat = dataRow.Data["startLat"].Value<double>();
            var startLon = dataRow.Data["startLon"].Value<double>();
            var destLat = dataRow.Data["destLat"].Value<double>();
            var destLon = dataRow.Data["destLon"].Value<double>();
            var driveMode = dataRow.Data.TryGetValue("driveMode", out var driveModeVal) ? driveModeVal.Value<int>() : 3;
            var trafficCode = dataRow.Data.TryGetValue("trafficCode", out var trafficCodeVal)
                ? trafficCodeVal.Value<string>() ?? "german"
                : "german";
            var osmRoute = dataRow.Data.TryGetValue("osmRoute", out var osmRouteVal)
                ? osmRouteVal.Value<string>() ?? ""
                : "";

            // Spread cars across the lot aisle network instead of one schedule coordinate.
            (startLat, startLon) = ResolveSpawn(startLat, startLon);

            var cardriver = new CarletonCarDriver(
                _carLayer,
                Register,
                UnregisterDriver,
                driveMode,
                startLat,
                startLon,
                destLat,
                destLon,
                osmRoute: osmRoute,
                trafficCode: trafficCode);

            lock (_driverSync)
            {
                _carLayer.Driver[cardriver.ID] = cardriver;
            }

            RegisterAgent(_carLayer, cardriver);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Carleton scheduler] Spawn failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private (double lat, double lon) ResolveSpawn(double scheduleLat, double scheduleLon)
    {
        EnsureSpawnPools();
        if (_spawnPools == null || _spawnPools.Count == 0)
            return (scheduleLat, scheduleLon);

        LotSpawnPool? best = null;
        var bestDist = double.MaxValue;
        foreach (var pool in _spawnPools.Values)
        {
            var d = HaversineM(scheduleLat, scheduleLon, pool.AnchorLat, pool.AnchorLon);
            if (d < bestDist)
            {
                bestDist = d;
                best = pool;
            }
        }

        // Schedule anchors should match a lot within tens of meters; 250 m is a safe ceiling.
        if (best == null || best.Candidates.Count == 0 || bestDist > 250.0)
            return (scheduleLat, scheduleLon);

        lock (_spawnSync)
        {
            var idx = Random.Shared.Next(best.Candidates.Count);
            var pick = best.Candidates[idx];
            return (pick.Lat, pick.Lon);
        }
    }

    private void EnsureSpawnPools()
    {
        if (_spawnPoolsLoaded)
            return;

        lock (_spawnSync)
        {
            if (_spawnPoolsLoaded)
                return;

            _spawnPoolsLoaded = true;
            _spawnPools = LoadSpawnPools();
            if (_spawnPools.Count > 0)
            {
                var total = 0;
                foreach (var pool in _spawnPools.Values)
                    total += pool.Candidates.Count;
                Console.WriteLine(
                    $"[Carleton scheduler] Lot spawn pools loaded: {_spawnPools.Count} lots, {total} aisle nodes");
            }
            else
            {
                Console.WriteLine(
                    "[Carleton scheduler] No parking_lot_spawn_candidates.json — using schedule startLat/startLon.");
            }
        }
    }

    private static Dictionary<string, LotSpawnPool> LoadSpawnPools()
    {
        var pools = new Dictionary<string, LotSpawnPool>(StringComparer.OrdinalIgnoreCase);
        var path = FindCandidatesPath();
        if (path == null)
            return pools;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            foreach (var lotProp in doc.RootElement.EnumerateObject())
            {
                var lot = lotProp.Name;
                var obj = lotProp.Value;
                if (!obj.TryGetProperty("anchor", out var anchorEl) ||
                    !obj.TryGetProperty("candidates", out var candsEl))
                    continue;

                var anchor = anchorEl.EnumerateArray();
                if (!anchor.MoveNext()) continue;
                var aLat = anchor.Current.GetDouble();
                if (!anchor.MoveNext()) continue;
                var aLon = anchor.Current.GetDouble();

                var candidates = new List<SpawnPoint>();
                foreach (var c in candsEl.EnumerateArray())
                {
                    var arr = c.EnumerateArray();
                    if (!arr.MoveNext()) continue;
                    var lat = arr.Current.GetDouble();
                    if (!arr.MoveNext()) continue;
                    var lon = arr.Current.GetDouble();
                    candidates.Add(new SpawnPoint(lat, lon));
                }

                if (candidates.Count == 0)
                    continue;

                pools[lot] = new LotSpawnPool(aLat, aLon, candidates);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Carleton scheduler] Failed to load spawn candidates: {ex.Message}");
        }

        return pools;
    }

    private static string? FindCandidatesPath()
    {
        var names = new[]
        {
            Path.Combine("resources", "parking_lot_spawn_candidates.json"),
            "parking_lot_spawn_candidates.json",
        };
        foreach (var name in names)
        {
            var full = Path.GetFullPath(name);
            if (File.Exists(full))
                return full;
        }

        // Walk up from the executable / cwd for nested run folders.
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 5 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "resources", "parking_lot_spawn_candidates.json");
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static double HaversineM(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat1 - lat2) * 111_000.0;
        var dLon = (lon1 - lon2) * 85_000.0;
        return Math.Sqrt(dLat * dLat + dLon * dLon);
    }

    private sealed class LotSpawnPool
    {
        public LotSpawnPool(double anchorLat, double anchorLon, List<SpawnPoint> candidates)
        {
            AnchorLat = anchorLat;
            AnchorLon = anchorLon;
            Candidates = candidates;
        }

        public double AnchorLat { get; }
        public double AnchorLon { get; }
        public List<SpawnPoint> Candidates { get; }
    }

    private readonly record struct SpawnPoint(double Lat, double Lon);
}
