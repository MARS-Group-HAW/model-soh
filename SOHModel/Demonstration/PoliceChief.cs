using SOHModel.Multimodal.Model;

namespace SOHModel.Demonstration;

public class PoliceChief : MultiCapableAgent<DemonstrationLayer>
{
    private DemonstrationLayer? _demonstrationLayer;

    public override void Init(DemonstrationLayer layer)
    {
        base.Init(layer);
        _demonstrationLayer = layer;

        EnvironmentLayer = _demonstrationLayer.SpatialGraphMediatorLayer;

        // StartPosition = new Position(9.955743, 53.570198);
        // GoalPosition = new Position(9.952852, 53.545340);

        // Get roadblock positions on each side of the demonstration route
        var leftNodes = _demonstrationLayer.LeftPoliceRouteNodes.ToList();
        var rightNodes = _demonstrationLayer.RightPoliceRouteNodes.ToList();
        
        // Calculate some counts for later calculations
        var leftNodeCount = leftNodes.Count;
        var rightNodeCount = rightNodes.Count;
        var nodeCount = leftNodeCount + rightNodeCount;
        var policeCount = _demonstrationLayer.PoliceMap.Count;
        
        // Calculate how many Police agents can be positioned on each side of the demonstration route
        var leftPoliceRatio = (double)leftNodeCount / nodeCount;
        var leftPoliceCount = (int)Math.Round(policeCount * leftPoliceRatio);
        var rightPoliceCount = policeCount - leftPoliceCount;

        // Calculate the spacing of the available Police agents on each side of the demonstration route
        // Goal: distribute police units as evenly along each side of the demonstration route as possible
        var leftPoliceDist = Math.Pow((double)leftPoliceCount / leftNodeCount, -1);
        var rightPoliceDist = Math.Pow((double)rightPoliceCount / rightNodeCount, -1);

        //0.5 -> 2
        //2 -> 0.5
        // Get an enumerator to iterate over Police agents
        using var policeEnum = _demonstrationLayer.PoliceMap.Values.GetEnumerator();
        
        // Distribute Police agents on left side of demonstration route
        for (double i = 0; i < _demonstrationLayer.LeftPoliceRouteNodes.Count; i += leftPoliceDist)
        {
            var index = (int)Math.Floor(i);
            if (policeEnum.MoveNext())
            {
                policeEnum.Current.Source = leftNodes[index].Position;
                policeEnum.Current.Position = policeEnum.Current.Source;
            }
        }

        // Distribute Police agents on right side of demonstration route
        for (double i = 0; i < _demonstrationLayer.RightPoliceRouteNodes.Count; i += rightPoliceDist)
        {
            var index = (int)Math.Floor(i);
            if (policeEnum.MoveNext())
            {
                policeEnum.Current.Source = rightNodes[index].Position;
                policeEnum.Current.Position = policeEnum.Current.Source;
            }
        }
    }

    public override void Tick()
    {
        // do nothing
    }
}