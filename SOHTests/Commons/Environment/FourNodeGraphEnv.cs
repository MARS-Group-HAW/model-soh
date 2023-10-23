using System.Collections.Generic;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;

namespace SOHTests.Commons.Environment;

public class FourNodeGraphEnv
{
    public static readonly Position Node1Pos = Position.CreateGeoPosition(9.92223, 53.54907);
    public static readonly Position Node2Pos = Position.CreateGeoPosition(9.92235, 53.55067);
    public static readonly Position Node3Pos = Position.CreateGeoPosition(9.91261, 53.55113);
    public static readonly Position Node4Pos = Position.CreateGeoPosition(9.912405, 53.550753);

    public readonly ISpatialNode Node1, Node2, Node3, Node4;

    public FourNodeGraphEnv()
    {
        GraphEnvironment = new SpatialGraphEnvironment();

        Node1 = GraphEnvironment.AddNode(ToDict(Node1Pos));
        Node2 = GraphEnvironment.AddNode(ToDict(Node2Pos));
        Node3 = GraphEnvironment.AddNode(ToDict(Node3Pos));
        Node4 = GraphEnvironment.AddNode(ToDict(Node4Pos));

        //Edges
        var e1 = GraphEnvironment.AddEdge(Node1, Node2,
            new Dictionary<string, object> { { "lanes", 3 } });
        AddModalities(e1);
        var e2 = GraphEnvironment.AddEdge(Node2, Node1,
            new Dictionary<string, object> { { "lanes", 3 } });
        AddModalities(e2);
        var e3 = GraphEnvironment.AddEdge(Node2, Node3,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e3);
        var e4 = GraphEnvironment.AddEdge(Node3, Node2,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e4);
        var e5 = GraphEnvironment.AddEdge(Node3, Node4,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e5);
        var e6 = GraphEnvironment.AddEdge(Node4, Node3,
            new Dictionary<string, object> { { "lanes", 2 } });
        AddModalities(e6);
    }

    public ISpatialGraphEnvironment GraphEnvironment { get; }

    private static void AddModalities(ISpatialEdge edge)
    {
        edge.Modalities.Add(SpatialModalityType.Walking);
        edge.Modalities.Add(SpatialModalityType.Cycling);
        edge.Modalities.Add(SpatialModalityType.CarDriving);
    }

    private static IDictionary<string, object> ToDict(Position pos)
    {
        return new Dictionary<string, object> { { "x", pos.X }, { "y", pos.Y } };
    }
}