using System;
using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;

namespace SOHTrainModel.Model;

public class TrainSchedulerLayer : SchedulerLayer
{
    private readonly TrainLayer _trainLayer;

    public TrainSchedulerLayer(TrainLayer trainLayer)
    {
        _trainLayer = trainLayer;
    }

    public TrainSchedulerLayer(TrainLayer trainLayer, DataTable table) : base(table)
    {
        _trainLayer = trainLayer;
    }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        const string trainTypeKey = "trainType";

        var trainType = dataRow.Data.TryGetValue(trainTypeKey, out var type) ? type.Value<string>() : "HHA-Typ-DT5";

        if (!dataRow.Data.ContainsKey("line"))
            throw new ArgumentException("Missing line number for train of field 'line' in input");

        var boardingTime = dataRow.Data.TryGetValue("minimumBoardingTimeInSeconds", out var wait)
            ? wait.Value<int>()
            : 0;
        var reversedRoute = dataRow.Data.TryGetValue("reversedRoute", out var reversed) &&
                            reversed.Value<bool>();
        var driver = new TrainDriver(_trainLayer, UnregisterAgent, trainType)
        {
            Line = dataRow.Data["line"].Value<string>(),
            MinimumBoardingTimeInSeconds = boardingTime,
            ReversedRoute = reversedRoute
        };

        _trainLayer.Driver.Add(driver.ID, driver);
        RegisterAgent(_trainLayer, driver);
    }
}