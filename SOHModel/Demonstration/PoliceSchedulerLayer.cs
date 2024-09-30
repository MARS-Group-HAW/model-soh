using Mars.Common;
using Mars.Components.Layers;

namespace SOHModel.Demonstration;

public class PoliceSchedulerLayer(DemonstrationLayer demonstrationLayer) : SchedulerLayer
{
    private DemonstrationLayer DemonstrationLayer { get; set; } = demonstrationLayer;

    protected override void Schedule(SchedulerEntry dataRow)
    {
        if (RegisterAgent == null) return;
        
        var source = dataRow.SourceGeometry.RandomPositionFromGeometry();
        var target = dataRow.TargetGeometry.RandomPositionFromGeometry();
        var squadSize = Convert.ToInt32(dataRow.Data["squadSize"]);

        var police = new Police
        {
            Source = source,
            Target = target,
            SquadSize = squadSize
        };
        police.Init(DemonstrationLayer);
        DemonstrationLayer.PoliceMap[police.ID] = police;

        RegisterAgent(DemonstrationLayer, police);
    }
}