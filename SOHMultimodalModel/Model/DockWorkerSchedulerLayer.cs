using System.Data;
using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Annotations;
using SOHDomain.Graph;
using SOHFerryModel.Station;

namespace SOHMultimodalModel.Model;

public class DockWorkerSchedulerLayer : SchedulerLayer
{
    public DockWorkerSchedulerLayer(DockWorkerLayer dockWorkerLayer)
    {
        WorkerLayer = dockWorkerLayer;
    }


    public DockWorkerSchedulerLayer(DockWorkerLayer dockWorkerLayer, DataTable table) : base(table)
    {
        WorkerLayer = dockWorkerLayer;
    }

    [PropertyDescription] public DockWorkerLayer WorkerLayer { get; }

    [PropertyDescription] public FerryStationLayer StationLayer { get; set; }

    [PropertyDescription] public SpatialGraphMediatorLayer Environment { get; set; }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        var start = dataRow.SourceGeometry.RandomPositionFromGeometry();
        var goal = dataRow.TargetGeometry.RandomPositionFromGeometry();

        var dockWorker = new DockWorker
        {
            FerryStationLayer = StationLayer,
            EnvironmentLayer = Environment,
            StartPosition = start,
            GoalPosition = goal,
            TravelScheduleId = dataRow.Data.TryGetValue("id", out var id) ? id.Value<int>() : 0
        };
        dockWorker.Init(WorkerLayer);

        if (dataRow.Data.TryGetValue("gender", out var gender) && gender != null)
            dockWorker.Gender = gender.Value<GenderType>();
        if (dataRow.Data.TryGetValue("mass", out var mass) && mass != null)
            dockWorker.Mass = mass.Value<double>();
        if (dataRow.Data.TryGetValue("perceptionInMeter", out var perception) && perception != null)
            dockWorker.PerceptionInMeter = perception.Value<double>();

        WorkerLayer.Agents.Add(dockWorker.ID, dockWorker);
        RegisterAgent(WorkerLayer, dockWorker);
    }
}