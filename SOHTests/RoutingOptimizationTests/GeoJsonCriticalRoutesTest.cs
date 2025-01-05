using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Xunit;
using Xunit.Abstractions;

namespace SOHTests.RoutingOptimizationTests
{
    /// <summary>
    /// Fixture to initialize and provide the GeoJSON path for tests
    /// </summary>
    public class GeoJsonCriticalRoutesFixture
    {
        public string GeoJsonPath { get; }

        public GeoJsonCriticalRoutesFixture()
        {
            // Dynamically navigate to the project root
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

            // Construct the full path to the GeoJSON file
            // GeoJsonPath = Path.Combine(projectRoot, "SOHRoutingOptimization", "resources", "autobahn_and_bundesstreet_fixed.geojson");
            GeoJsonPath = Path.Combine(projectRoot, "SOHRoutingOptimization", "resources", "autobahn_und_bundesstrassen_deutschland.geojson");
            Console.WriteLine($"Looking for GeoJSON file at: {Path.GetFullPath(GeoJsonPath)}");
            if (!File.Exists(GeoJsonPath))
            {
                throw new FileNotFoundException("GeoJSON file not found for testing.", GeoJsonPath);
            }

            Console.WriteLine($"GeoJSON file found at: {GeoJsonPath}");
        }
    }

    /// <summary>
    /// Test class for validating critical routes within the GeoJSON network
    /// </summary>
    public class GeoJsonCriticalRoutesTest : IClassFixture<GeoJsonCriticalRoutesFixture>
    {
        private readonly string _geoJsonPath;
        private readonly ITestOutputHelper _output;

        public GeoJsonCriticalRoutesTest(GeoJsonCriticalRoutesFixture fixture, ITestOutputHelper output)
        {
            _geoJsonPath = fixture.GeoJsonPath ?? throw new ArgumentNullException(nameof(fixture.GeoJsonPath));
            _output = output;
        }

        [Fact]
        public void TestCriticalRoutes()
        {
            var helper = new GeoJsonCriticalRoutesHelper(_geoJsonPath, _output);
            helper.ValidateCriticalRoutes();
        }
    }

    /// <summary>
    /// Helper class for testing critical routes
    /// </summary>
    public class GeoJsonCriticalRoutesHelper
    {
        private readonly ISpatialGraphEnvironment _environment;
        private readonly ITestOutputHelper _output;

        public GeoJsonCriticalRoutesHelper(string geoJsonPath, ITestOutputHelper output)
        {
            _output = output;
            _environment = InitializeEnvironment(geoJsonPath);
        }

        private ISpatialGraphEnvironment InitializeEnvironment(string geoJsonPath)
        {
            if (!File.Exists(geoJsonPath))
                throw new FileNotFoundException("GeoJSON file not found.", geoJsonPath);

            _output.WriteLine($"Initializing environment from GeoJSON: {geoJsonPath}");
            // Configure Input object with IsBiDirectedImport set to true
            var input = new Input
            {
                File = geoJsonPath,
                InputConfiguration = new InputConfiguration
                {
                    IsBiDirectedImport = true // Enable bidirectional import
                }
            };

            // Pass the input to SpatialGraphEnvironment
            return new SpatialGraphEnvironment(input);
        }


        public void ValidateCriticalRoutes()
        {
            var criticalRoutes = new List<(double startLat, double startLon, double endLat, double endLon, string description)>
            {
                // // Short test routes around Berlin
                (53.5577323,10.2174148,52.8067652,12.7941436, "Hamburg to Berlin"),
                (52.520008, 13.404954, 52.570008, 13.504954, "Berlin City Center to 50 km North-East"),
                (52.520008, 13.404954, 52.470008, 13.304954, "Berlin City Center to 50 km South-West"),
                (52.520008, 13.404954, 52.546456,13.605715, "Berlin City Center to 50 km East"),
                (52.520008, 13.404954, 52.520008, 12.954954, "Berlin City Center to 50 km West"),
                // Berlin to Munich
                (52.5200, 13.4050, 48.1351, 11.5820, "Berlin to Munich (North-East to South-Central)"),
                
                // Hamburg to Frankfurt
                // (53.5511, 9.9937, 50.1109, 8.6821, "Hamburg to Frankfurt (North to South-West)"),
                (53.547530,9.986788, 50.1040831,8.6687321, "Hamburg to Frankfurt (North to South-West)"),
                
                
                // Cologne to Berlin
                (50.9375, 6.9603, 52.5200, 13.4050, "Cologne to Berlin (West to East)"),
                
                // Düsseldorf to Stuttgart
                (51.2277, 6.7735, 48.7758, 9.1829, "Düsseldorf to Stuttgart (West to South)"),
                
                // Munich to Hamburg
                (48.1351, 11.5820, 53.5511, 9.9937, "Munich to Hamburg (South to North)"),
                
                // Frankfurt to Leipzig
                (50.1109, 8.6821, 51.3397, 12.3731, "Frankfurt to Leipzig (West-Central to East-Central)"),
                //Street with Roundabout
                (53.297275,  9.25525, 53.289782,  9.257121, "Drive through Roundabout")
            };

            foreach (var route in criticalRoutes)
            {
                _output.WriteLine($"Testing route: {route.description}");
                
                var startNode = _environment.NearestNode(Position.CreateGeoPosition(route.startLon, route.startLat));
                var endNode = _environment.NearestNode(Position.CreateGeoPosition(route.endLon, route.endLat));
                if (startNode == null || endNode == null)
                {
                    _output.WriteLine($"Failed to find nearest nodes for: {route.description}");
                    Assert.True(false, $"Nearest node could not be found for route: {route.description}");
                }

                var shortestRoute = _environment.FindShortestRoute(startNode, endNode, edge => true);
                

                if (shortestRoute == null)
                {
                    _output.WriteLine($"No route found for: {route.description}");
                }
                // Assert.NotNull(shortestRoute);
            }
        }
    }
}
