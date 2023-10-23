using System.Collections.Generic;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;

namespace SOHTests.Commons.Environment;

public class TwoNodeGraphEnv
{
    public readonly ISpatialNode Node1, Node2;

    public TwoNodeGraphEnv()
    {
        GraphEnvironment = new SpatialGraphEnvironment();

        //Nodes; Node1  == FourNodeGraphEnv.Node2
        Node1 = GraphEnvironment.AddNode(new Dictionary<string, object> { { "x", 9.92235 }, { "y", 53.55067 } });
        Node2 = GraphEnvironment.AddNode(new Dictionary<string, object> { { "x", 9.925249 }, { "y", 53.550097 } });

        GraphEnvironment.AddEdge(Node1, Node2,
            new Dictionary<string, object> { { "length", 108.89 }, { "lanes", 1 } });
    }

    public ISpatialGraphEnvironment GraphEnvironment { get; }
}