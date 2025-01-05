using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.SOHLogisticsTests
{
    /// <summary>
    /// Fixture to initialize and provide the GeoJSON path for tests
    /// </summary>
    public class GeoJsonFullConnectivityFixture
    {
        public string GeoJsonPath { get; }

        public GeoJsonFullConnectivityFixture()
        {
            // Dynamically navigate to the project root
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

            // Construct the full path to the GeoJSON file
            GeoJsonPath = Path.Combine(projectRoot, "SOHLogisticsBox", "resources", "autobahn_und_bundesstrassen_deutschland.geojson");
            Console.WriteLine($"Looking for GeoJSON file at: {Path.GetFullPath(GeoJsonPath)}");
            if (!File.Exists(GeoJsonPath))
            {
                throw new FileNotFoundException("GeoJSON file not found for testing.", GeoJsonPath);
            }

            Console.WriteLine($"GeoJSON file found at: {GeoJsonPath}");
        }
    }

    /// <summary>
    /// Test class for validating the full connectivity of the GeoJSON network
    /// </summary>
    public class GeoJsonFullConnectivityTest : IClassFixture<GeoJsonFullConnectivityFixture>
    {
        private readonly string _geoJsonPath;
        private readonly ITestOutputHelper _output;

        public GeoJsonFullConnectivityTest(GeoJsonFullConnectivityFixture fixture, ITestOutputHelper output)
        {
            _geoJsonPath = fixture.GeoJsonPath ?? throw new ArgumentNullException(nameof(fixture.GeoJsonPath));
            _output = output;
        }

        [Fact]
        public void TestFullConnectivity()
        {
            var helper = new GeoJsonConnectivityHelper(_geoJsonPath, _output);
            helper.FindAndPrintComponents();
        }
    }

    /// <summary>
    /// Helper class for validating full connectivity
    /// </summary>
    public class GeoJsonConnectivityHelper
    {
        private readonly ISpatialGraphEnvironment _environment;
        private readonly ITestOutputHelper _output;

        public GeoJsonConnectivityHelper(string geoJsonPath, ITestOutputHelper output)
        {
            _output = output;
            _environment = InitializeEnvironment(geoJsonPath);
        }

        private ISpatialGraphEnvironment InitializeEnvironment(string geoJsonPath)
        {
            if (!File.Exists(geoJsonPath))
                throw new FileNotFoundException("GeoJSON file not found.", geoJsonPath);

            _output.WriteLine($"Initializing environment from GeoJSON: {geoJsonPath}");

            // Configure Input with bidirectional import
            var input = new Input
            {
                File = geoJsonPath,
                InputConfiguration = new InputConfiguration
                {
                    IsBiDirectedImport = true // Ensure bidirectional import
                }
            };

            return new SpatialGraphEnvironment(input);
        }

        public void FindAndPrintComponents()
        {
            var visited = new HashSet<ISpatialNode>();
            var nodes = _environment.Nodes.ToList();
            var totalNodes = nodes.Count;

            if (totalNodes == 0)
            {
                _output.WriteLine("The graph contains no nodes.");
                return;
            }

            var mainComponent = new List<ISpatialNode>();
            BFS(nodes.First(), visited, mainComponent);
            _output.WriteLine($"Main component size: {mainComponent.Count} nodes.");

            foreach (var node in nodes)
            {
                if (!visited.Contains(node))
                {
                    var additionalComponent = new List<ISpatialNode>();
                    BFS(node, visited, additionalComponent);

                    if (additionalComponent.Count > 1)
                    {
                        _output.WriteLine($"Found an additional component with {additionalComponent.Count} nodes.");
                    }
                }
            }
        }

        private void BFS(ISpatialNode startNode, HashSet<ISpatialNode> visited, List<ISpatialNode> component)
        {
            var queue = new Queue<ISpatialNode>();
            queue.Enqueue(startNode);
            visited.Add(startNode);

            while (queue.Count > 0)
            {
                var currentNode = queue.Dequeue();
                component.Add(currentNode);

                var connectedEdges = _environment.Edges.Values.Where(e => e.From == currentNode || e.To == currentNode);
                foreach (var edge in connectedEdges)
                {
                    ISpatialNode neighbor = edge.From.Equals(currentNode) ? edge.To : edge.From;

                    if (!visited.Contains(neighbor))
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }
}
