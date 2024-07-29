using Mars.Common;
using Mars.Components.Layers;

namespace SOHModel.Demonstration;

public class DemonstratorSchedulerLayer(DemonstrationLayer demonstrationLayer) : SchedulerLayer
{
    private DemonstrationLayer DemonstrationLayer { get; set; } = demonstrationLayer;

    protected override void Schedule(SchedulerEntry dataRow)
    {
        if (RegisterAgent == null) return;
        
        var source = dataRow.SourceGeometry.RandomPositionFromGeometry();
        var target = dataRow.TargetGeometry.RandomPositionFromGeometry();
        var isRadical = Convert.ToBoolean(dataRow.Data["isRadical"]);

        if (isRadical)
        {
            var demonstrator = new RadicalDemonstrator { Source = source, Target = target };
            demonstrator.Init(DemonstrationLayer);
            DemonstrationLayer.RadicalDemonstratorMap[demonstrator.ID] = demonstrator;
            RegisterAgent(DemonstrationLayer, demonstrator);
        }
        else
        {
            var demonstrator = new Demonstrator { Source = source, Target = target };
            demonstrator.Init(DemonstrationLayer);
            DemonstrationLayer.DemonstratorMap[demonstrator.ID] = demonstrator;
            RegisterAgent(DemonstrationLayer, demonstrator);
        }
    }
}