using System;
using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;

namespace SOHMultimodalModel.Model;

/// <summary>
///     Provides the scheduling of <see cref="CycleTraveler" /> utilizing the cycle and the walking modality.
/// </summary>
public class CycleTravelerSchedulerLayer : SchedulerLayer
{
    private readonly Random _random;
    private readonly CycleTravelerLayer _travelerLayer;

    public CycleTravelerSchedulerLayer(CycleTravelerLayer travelerLayer)
    {
        _travelerLayer = travelerLayer;
        _random = new Random();
    }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        var source = dataRow.SourceGeometry.RandomPositionFromGeometry();
        var target = dataRow.TargetGeometry.RandomPositionFromGeometry();

        var ownBike = dataRow.Data["hasOwnBikeProbability"].Value<double>();
        var hasBike = _random.NextDouble() < ownBike;

        var traveler = new CycleTraveler
        {
            HasBike = hasBike, StartPosition = source, GoalPosition = target
        };
        traveler.Init(_travelerLayer);

        RegisterAgent(_travelerLayer, traveler);
    }
}