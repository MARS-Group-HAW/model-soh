using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;
namespace SOHModel.Tram.Model;

public class TramSchedulerLayer: SchedulerLayer
{
    private readonly TramLayer _tramLayer;

    public TramSchedulerLayer(TramLayer tramLayer)
    {
        _tramLayer = tramLayer;
    }

    public TramSchedulerLayer(TramLayer tramLayer, DataTable table) : base(table)
    {
        _tramLayer = tramLayer;
    }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        const string tramTypeKey = "tramType";

        var tramType = dataRow.Data.TryGetValue(tramTypeKey, out var type) ? type.Value<string>() : "HHA-Typ-DT5";

        if (!dataRow.Data.ContainsKey("line"))
            throw new ArgumentException("Missing line number for tram of field 'line' in input");

        var boardingTime = dataRow.Data.TryGetValue("minimumBoardingTimeInSeconds", out var wait)
            ? wait.Value<int>()
            : 0;
        var reversedRoute = dataRow.Data.TryGetValue("reversedRoute", out var reversed) &&
                            reversed.Value<bool>();
        var driver = new TramDriver(_tramLayer, UnregisterAgent, tramType)
        {
            Line = dataRow.Data["line"].Value<string>(),
            MinimumBoardingTimeInSeconds = boardingTime,
            ReversedRoute = reversedRoute
        };

        _tramLayer.Driver.Add(driver.ID, driver);
        RegisterAgent(_tramLayer, driver);
    }
}