using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Xunit;
using Xunit.Abstractions;

public class BikesTests
{
    private readonly ITestOutputHelper _output;

    public BikesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public static readonly Position Node1Pos = Position.CreateGeoPosition(9.899291554980337, 53.588794556630731);
    public static readonly Position Node2Pos = Position.CreateGeoPosition(9.901537343190636, 53.588367979554995);

    [Fact]
    public void Test_UnimodalRouteBetweenCoordinates()
    {
        string currentDirectory = Directory.GetCurrentDirectory();
        _output.WriteLine("Current working directory: " + currentDirectory);
        
        var environment = new SpatialGraphEnvironment("");
        _output.WriteLine(environment.Nodes.Count.ToString());
            
        bool node1Exists = environment.Nodes.Any(node => node.Position.Equals(Node1Pos));
        Assert.True(node1Exists, "Node1 with the specified position does not exist.");
        bool node2Exists = environment.Nodes.Any(node => node.Position.Equals(Node2Pos));
        Assert.True(node2Exists, "Node2 with the specified position does not exist.");

        var startNode = environment.Nodes.First(node => node.Position.Equals(Node1Pos));
        var goalNode = environment.Nodes.First(node => node.Position.Equals(Node2Pos));

        var route = environment.FindShortestRoute(startNode, goalNode, edge => edge.Modalities.Contains(SpatialModalityType.Walking));
        Assert.NotNull(route);

    }
}
