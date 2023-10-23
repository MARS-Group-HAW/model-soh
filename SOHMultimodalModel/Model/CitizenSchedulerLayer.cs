using System;
using System.Data;
using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Annotations;
using SOHMultimodalModel.Layers;

namespace SOHMultimodalModel.Model;

public class CitizenSchedulerLayer : SchedulerLayer
{
    private readonly CitizenLayer _citizenLayer;

    public CitizenSchedulerLayer(CitizenLayer citizenLayer)
    {
        _citizenLayer = citizenLayer;
    }

    public CitizenSchedulerLayer(CitizenLayer citizenLayer, DataTable table) : base(table)
    {
        _citizenLayer = citizenLayer;
    }

    [PropertyDescription] public MediatorLayer MediatorLayer { get; set; }

    protected override void Schedule(SchedulerEntry dataRow)
    {
        if (dataRow.SourceGeometry == null)
            throw new ArgumentException("No source geometry provided for citizen scheduling input");

        var source = dataRow.SourceGeometry.RandomPositionFromGeometry();

        var isWorker = dataRow.Data.TryGetValue("worker", out var worker) && worker.Value<bool>();
        var isPartTimeWorker =
            dataRow.Data.TryGetValue("partTimeWorker", out var partTime) && partTime.Value<bool>();


        var citizen = new Citizen
        {
            StartPosition = source, Worker = isWorker, PartTimeWorker = isPartTimeWorker, MediatorLayer = MediatorLayer
        };
        citizen.Init(_citizenLayer);

        if (dataRow.Data.TryGetValue("gender", out var gender)) citizen.Gender = gender.Value<GenderType>();
        if (dataRow.Data.TryGetValue("mass", out var mass)) citizen.Mass = mass.Value<double>();
        if (dataRow.Data.TryGetValue("speed", out var speed)) citizen.Velocity = speed.Value<double>();
        if (dataRow.Data.TryGetValue("height", out var height)) citizen.Height = height.Value<double>();
        if (dataRow.Data.TryGetValue("width", out var width)) citizen.Width = width.Value<double>();

        RegisterAgent(_citizenLayer, citizen);
    }
}