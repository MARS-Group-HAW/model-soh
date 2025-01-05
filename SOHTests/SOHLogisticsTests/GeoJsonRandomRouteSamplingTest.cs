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
    public class GeoJsonRouteSamplingFixture
    {
        public string GeoJsonPath { get; }

        public GeoJsonRouteSamplingFixture()
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
    /// Test class for validating random route sampling within the GeoJSON network
    /// </summary>
    public class GeoJsonRandomRouteSamplingTest : IClassFixture<GeoJsonRouteSamplingFixture>
    {
        private readonly string _geoJsonPath;
        private readonly ITestOutputHelper _output;

        public GeoJsonRandomRouteSamplingTest(GeoJsonRouteSamplingFixture fixture, ITestOutputHelper output)
        {
            _geoJsonPath = fixture.GeoJsonPath ?? throw new ArgumentNullException(nameof(fixture.GeoJsonPath));
            _output = output;
        }

        [Fact]
        public void TestRandomRouteSampling()
        {
            var helper = new GeoJsonRouteSamplingHelper(_geoJsonPath, _output);
            var issues = helper.CheckRandomRoutes(sampleSize: 500);

            if (issues.Any())
            {
                _output.WriteLine($"Found {issues.Count} routing issues:");
                foreach (var issue in issues)
                {
                    _output.WriteLine(issue);
                }
                Assert.True(false, "Random route sampling found missing routes.");
            }
            else
            {
                _output.WriteLine("No issues found in random route sampling.");
            }
        }
    }


    /// <summary>
    /// Helper class for performing random route sampling
    /// </summary>
    public class GeoJsonRouteSamplingHelper
{
    private readonly ISpatialGraphEnvironment _environment;
    private readonly ITestOutputHelper _output;

    public GeoJsonRouteSamplingHelper(string geoJsonPath, ITestOutputHelper output)
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

    public List<string> CheckRandomRoutes(int sampleSize)
    {
        var issues = new List<string>();
        var nodes = _environment.Nodes.ToList();
        var random = new Random();

        _output.WriteLine($"Starting random route sampling with a sample size of {sampleSize}...");

        for (int i = 0; i < sampleSize; i++)
        {
            // Randomly pick two distinct nodes
            var startNode = nodes[random.Next(nodes.Count)];
            var endNode = nodes[random.Next(nodes.Count)];
            if (startNode.Equals(endNode)) continue;

            // Check for route
            var route = _environment.FindShortestRoute(startNode, endNode, edge => true);
            if (route == null)
            {
                issues.Add($"No route found between {startNode.Position} and {endNode.Position}");
            }

            // Output progress every 100 samples or at the end
            if ((i + 1) % 100 == 0 || (i + 1) == sampleSize)
            {
                _output.WriteLine($"Progress: {i + 1}/{sampleSize} routes sampled.");
            }
        }

        _output.WriteLine($"Random route sampling completed. Total issues found: {issues.Count}");
        return issues;
    }
}

}
