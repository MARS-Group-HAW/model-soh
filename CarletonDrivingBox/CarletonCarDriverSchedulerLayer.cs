using System;
using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Layers;
using SOHModel.Car.Model;

namespace SOHCarletonDrivingBox;

/// <summary>
/// Campus evacuation scheduler for <see cref="CarDriver"/> agents.
/// </summary>
/// <remarks>
/// Stock <c>CarDriverSchedulerLayer</c> (SOHModel) reads spawn coordinates from WKT
/// <c>source</c>/<c>destination</c> columns via <c>SourceGeometry</c>/<c>TargetGeometry</c>.
/// Carleton's <c>car_driver_schedule.csv</c> uses explicit <c>startLat</c>/<c>startLon</c>/
/// <c>destLat</c>/<c>destLon</c> columns instead — same idea as
/// <c>SemiTruckSchedulerLayer</c>, which reads <c>sourceX</c>/<c>sourceY</c> from
/// <c>dataRow.Data</c> rather than patching the shared library.
/// </remarks>
public class CarletonCarDriverSchedulerLayer : SchedulerLayer
{
    private readonly CarletonCarLayer _carLayer;

    public CarletonCarDriverSchedulerLayer(CarletonCarLayer carLayer)
    {
        _carLayer = carLayer;
    }

    private static void Register(ILayer layer, ITickClient tickClient)
    {
        // CarDriver's constructor already registers via the register callback.
    }

    private static void Unregister(ILayer layer, ITickClient tickClient)
    {
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

            var cardriver = new CarDriver(
                _carLayer,
                Register,
                Unregister,
                driveMode,
                startLat,
                startLon,
                destLat,
                destLon,
                osmRoute: osmRoute,
                trafficCode: trafficCode);

            _carLayer.Driver.Add(cardriver.ID, cardriver);
            RegisterAgent(_carLayer, cardriver);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Carleton scheduler] Spawn failed: {ex.Message}");
        }
    }
}
