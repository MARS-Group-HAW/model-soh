using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;

namespace SOHModel.Ferry.Model;

public class FerrySchedulerLayer : SchedulerLayer
{
    private readonly FerryLayer _ferryLayer;

    public FerrySchedulerLayer(FerryLayer ferryLayer)
    {
        _ferryLayer = ferryLayer;
    }

    public FerrySchedulerLayer(FerryLayer ferryLayer, DataTable table) : base(table)
    {
        _ferryLayer = ferryLayer;
    }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        const string ferryTypeKey = "ferryType";

        var ferryType = dataRow.Data.TryGetValue(ferryTypeKey, out var type) ? type.Value<string>() : "Typ2000";

        if (!dataRow.Data.ContainsKey("line"))
            throw new ArgumentException("Missing line number for ferry of field 'line' in input");

        var driver = new FerryDriver(_ferryLayer, UnregisterAgent, ferryType)
        {
            Line = dataRow.Data["line"].Value<int>(),
            MinimumBoardingTimeInSeconds = dataRow.Data.TryGetValue("minimumBoardingTimeInSeconds", out var wait)
                ? wait.Value<int>()
                : 0
        };

        _ferryLayer.Driver.Add(driver.ID, driver);
        RegisterAgent(_ferryLayer, driver);
    }
}