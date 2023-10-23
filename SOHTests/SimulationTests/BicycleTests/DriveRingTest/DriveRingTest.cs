using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Components.Services;
using Mars.Components.Starter;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Layers.Initialization;
using Mars.Interfaces.Model;
using SOHMultimodalModel.Output.Trips;
using SOHTests.SimulationTests.BicycleTests.Helper;
using Xunit;

namespace SOHTests.SimulationTests.BicycleTests.DriveRingTest
{
    public class DriveRingTest
    {
        [Fact]
        public void TestSimRun()
        {
            var description = new ModelDescription();
            description.AddLayer<InfiniteCyclistLayer>();
            description.AddAgent<InfiniteCyclist, InfiniteCyclistLayer>();

            var config = new SimulationConfig
            {
                SimulationIdentifier = "infinite_cyclist_test",
                Execution = {MaximalLocalProcess = 1},
                AgentMappings = new List<AgentMapping>
                {
                    new AgentMapping
                    {
                        InstanceCount = 10,
                        Name = nameof(InfiniteCyclist), IndividualMapping = new List<IndividualMapping>
                        {
                            new IndividualMapping {Name = "power", Value = 75},
                            new IndividualMapping {Name = "weight", Value = 80},
                            new IndividualMapping {Name = "width", Value = 0.60}
                        }
                    }
                },
                Globals =
                {
                    StartPoint = DateTime.Today,
                    EndPoint = DateTime.Today.AddMinutes(10),
                    DeltaTUnit = TimeSpanUnit.Seconds,
                    OutputTarget = OutputTargetType.PostgresSql,
                    PostgresSqlOptions =
                    {
                        HostUserName = "postgres", HostPassword = "password"
                    }
                }
            };

            var state = SimulationStarter.Start(description, config).Run();

            Assert.Equal(600, state.Iterations);

            var driver = state.Model.ExecutionGroups[1].OfType<InfiniteCyclist>();
            TripsOutputAdapter.PrintTripResult(driver);
        }

        [Fact]
        public void TestBicycleInteraction()
        {
            var network = Path.Combine("res", "networks", "ring_network.geojson");
            var graph = new SpatialGraphEnvironment(network);

            var context = SimulationContext.Start2020InSeconds;

            var driver = Enumerable.Range(0, 10)
                .Select(i => new InfiniteCyclist(context, i, 75, 80, 0.60, graph)).ToList();

            //driver[1].MaxSpeed = 9.333;
            // driver[2].MaxSpeed = 3.667;
            const int ticks = 3600;

            for (var i = 0; i < ticks; i++, context.UpdateStep(1))
            {
                driver.Shuffle();
                foreach (var infiniteDriver in driver) infiniteDriver.Tick();

                // When there are at least two agents on the lane then we have to find another entity ahead
                if (driver.Count > 1)
                    Assert.All(driver, d =>
                    {
                        Assert.NotNull(d.DriverAhead);
                        Assert.NotEqual(d, d.DriverAhead);
                    });
            }

            Assert.All(driver, infiniteDriver =>
            {
                Assert.True(infiniteDriver.Speed >= 0);
                Assert.True(infiniteDriver.PositionOnCurrentEdge >= 0);
                Assert.NotNull(infiniteDriver.CurrentEdge);
                Assert.Equal(0, infiniteDriver.LaneOnCurrentEdge);
                Assert.NotNull(infiniteDriver.Position);
            });

            TripsOutputAdapter.PrintTripResult(driver);
        }
    }

    public class InfiniteCyclistLayer : AbstractLayer
    {
        public IDictionary<Guid, InfiniteCyclist> Agents { get; set; }

        public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
            UnregisterAgent unregisterAgentHandle)
        {
            base.InitLayer(layerInitData, registerAgentHandle, unregisterAgentHandle);

            var network = Path.Combine("res", "networks", "ring_network.geojson");
            var graph = new SpatialGraphEnvironment(network);

            Agents = AgentManager.SpawnAgents<InfiniteCyclist>(
                layerInitData.AgentInitConfigs.First(
                    mapping => mapping.Type.MetaType == typeof(InfiniteCyclist)),
                registerAgentHandle, unregisterAgentHandle, new List<ILayer> {this},
                new List<IEnvironment> {graph});

            return true;
        }
    }
}