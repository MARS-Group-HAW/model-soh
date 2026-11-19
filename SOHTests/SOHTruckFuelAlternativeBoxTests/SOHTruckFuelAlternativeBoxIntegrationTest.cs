using System;
using System.IO;
using System.Text.Json.Nodes;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces.Model;
using SOHModel.SemiTruck.Model;
using Xunit;

namespace SOHTests.SOHTruckFuelAlternativeBoxTests
{
    [Collection("CurrentDirectory")]
    public class SOHTruckFuelAlternativeBoxIntegrationTest
    {
        private static string BoxDirectory => Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SOHTruckFuelAlternativeBox"));

        [SkippableFact]
        public void BoxConfigSimulationRunsWithoutDatabase()
        {
            var boxDirectory = BoxDirectory;
            var boxResources = Path.Combine(boxDirectory, "resources");
            Skip.IfNot(
                File.Exists(Path.Combine(boxResources, "autobahn_und_bundesstrassen_deutschland_elevation_21.geojson")),
                "road network GeoJSON is not present.");

            // The layer resolves its CSV resources relative to the output directory
            var linkedResources = Path.Combine(AppContext.BaseDirectory, "resources");
            if (!Directory.Exists(linkedResources))
                Directory.CreateSymbolicLink(linkedResources, boxResources);

            var config = JsonNode.Parse(File.ReadAllText(Path.Combine(boxDirectory, "config.json")))!;

            // Short run, no Postgres output
            config["globals"]!["endPoint"] = "2026-01-08T10:01:00";
            config["globals"]!["console"] = false;
            config["globals"]!.AsObject().Remove("npgSqlOptions");
            config["agents"]![0]!.AsObject().Remove("outputs");

            var description = new ModelDescription();
            description.AddLayer<SemiTruckLayer>();
            description.AddAgent<SemiTruckDriver, SemiTruckLayer>();
            description.AddEntity<SemiTruck>();

            var previousDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(boxDirectory);
            try
            {
                var simConfig = SimulationConfig.Deserialize(config.ToJsonString());
                var application = SimulationStarter.BuildApplication(description, simConfig);
                var state = application.Resolve<ISimulation>().StartSimulation();
                application.Dispose();

                Assert.Equal(60, state.Iterations);
            }
            finally
            {
                Directory.SetCurrentDirectory(previousDirectory);
            }
        }
    }

    [CollectionDefinition("CurrentDirectory", DisableParallelization = true)]
    public class CurrentDirectoryCollection
    {
    }
}
