using System;
using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;

namespace SOHBusModel.Model;

public class BusSchedulerLayer : SchedulerLayer
{
    private readonly BusLayer _busLayer;

    public BusSchedulerLayer(BusLayer busLayer)
    {
        _busLayer = busLayer;
    }

    public BusSchedulerLayer(BusLayer busLayer, DataTable table) : base(table)
    {
        _busLayer = busLayer;
    }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        const string typeKey = "busType";

        var busType = dataRow.Data.TryGetValue(typeKey, out var type) ? type.Value<string>() : "EvoBus";

        if (!dataRow.Data.ContainsKey("line"))
            throw new ArgumentException("Missing line number for bus of field 'line' in input");

        var boardingTime = dataRow.Data.TryGetValue("minimumBoardingTimeInSeconds", out var wait)
            ? wait.Value<int>()
            : 0;
        var reversedRoute = dataRow.Data.TryGetValue("reversedRoute", out var reversed) &&
                            reversed.Value<bool>();
        var driver = new BusDriver(_busLayer, UnregisterAgent, busType)
        {
            Line = dataRow.Data["line"].Value<string>(),
            MinimumBoardingTimeInSeconds = boardingTime,
            ReversedRoute = reversedRoute
        };

        _busLayer.Driver.Add(driver.ID, driver);
        RegisterAgent(_busLayer, driver);
    }
}