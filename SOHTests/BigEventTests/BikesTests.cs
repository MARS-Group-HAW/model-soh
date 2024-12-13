using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Domain.Steering.Acceleration;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.BigEventTests
{

    public class BikesTests
    {
        private readonly ITestOutputHelper _output;

        public BikesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Test_UnimodalRouteBetweenCoordinates()
        {
            string currentDirectory = Directory.GetCurrentDirectory();
            _output.WriteLine("Current working directory: " + currentDirectory);

            var environment = new SpatialGraphEnvironment(new Input
            {
                File = Path.Combine(
                    "BigEventTests", "resources", "walk_v8.geojson"),
                InputConfiguration = new InputConfiguration
                {
                    GeometryAsNodesEnabled = true,
                    IsBiDirectedImport = true, 
                    Modalities = new HashSet<SpatialModalityType>
                    {
                        SpatialModalityType.Walking,
                        SpatialModalityType.Cycling
                    }
                }
            });
            //_output.WriteLine(environment.Nodes.Count.ToString());

            //File.WriteAllText("walking_and_cycling_base.geojson", environment.ToGeoJson());

            foreach (var startNode in environment.Nodes)
            {
                foreach (var goalNode in environment.Nodes)
                {
                    if (startNode != goalNode)
                    {
                        _output.WriteLine($"Calculating route from Node {startNode.Position} to Node {goalNode.Position}");

                        var selectedRoute = environment.FindShortestRoute(startNode, goalNode,
                            edge => edge.Modalities.Contains(SpatialModalityType.Walking));

                        Assert.NotNull(selectedRoute);
                    }
                }
            }
        }
    }
}
