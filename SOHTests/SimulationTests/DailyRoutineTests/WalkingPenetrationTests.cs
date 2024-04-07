using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Components.Starter;
using Mars.Core.Data;
using Mars.Core.Simulation;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.SimulationTests.DailyRoutineTests;

[Collection("SimulationTests")]
public class WalkingPenetrationTests
{
    [Fact]
    [Trait("Category", "External")]
    public void SimulateOneDay()
    {
        var description = new ModelDescription();
        description.AddLayer<TestPenetrationPedestrianLayer>();
        description.AddAgent<TestPenetrationPedestrian, TestPenetrationPedestrianLayer>();

        var environment = new FourNodeGraphEnv().GraphEnvironment;
        // var environment = new SpatialGraphEnvironment(SimulationTestConstants.AltonaWalkGraph);

        var startPoint = DateTime.Parse("2020-01-01T00:00:00");
        var config = new SimulationConfig
        {
            Execution =
            {
                MaximalLocalProcess = 1
            },
            Globals =
            {
                StartPoint = startPoint,
                EndPoint = startPoint + TimeSpan.FromMinutes(3),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.None
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(TestPenetrationPedestrianLayer),
                    IndividualMapping =
                    {
                        new IndividualMapping
                        {
                            Name = "environment",
                            Value = environment
                        }
                    }
                }
            },
            AgentMappings =
            {
                new AgentMapping
                {
                    Name = nameof(TestPenetrationPedestrian),
                    InstanceCount = 1,
                    // File = Path.Combine("res", "agent_inits", "CitizenInit10k.csv"),
                    // Options = {{"csvSeparator", ';'}},
                    IndividualMapping =
                    {
                        new IndividualMapping { Name = "gender", Value = GenderType.Male },
                        new IndividualMapping { Name = "startPosition", Value = environment.GetRandomNode().Position }
                    },
                    OutputFilter =
                    {
                        new OutputFilter
                        {
                            Name = "StableId",
                            Values = new object[] { 0 },
                            Operator = ContainsOperator.In
                        }
                    }
                }
            }
        };

        var application = SimulationStarter.BuildApplication(description, config);
        var simulation = application.Resolve<ISimulation>();
        simulation.StartSimulation();
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public class TestPenetrationPedestrian : MultiCapableAgent<TestPenetrationPedestrianLayer>
{
    public double RemainingDistanceToGoal => MultimodalRoute?.First()?.Route.RemainingRouteDistanceToGoal ?? -1;
    public double Latitude => Position.Latitude;
    public double Longitude => Position.Longitude;

    public override void Init(TestPenetrationPedestrianLayer layer)
    {
        base.Init(layer);

        EnvironmentLayer = layer.SpatialGraphMediatorLayer;
        MultimodalRoute = SearchMultimodalRoute();
    }

    public override void Tick()
    {
        if (GoalReached) MultimodalRoute = SearchMultimodalRoute();

        Move();
    }

    private MultimodalRoute SearchMultimodalRoute()
    {
        var start = EnvironmentLayer.Environment.NearestNode(StartPosition);
        var route = EnvironmentLayer.Environment.FindShortestRoute(start,
            EnvironmentLayer.Environment.GetRandomNode(),
            edge => edge.Modalities.Contains(SpatialModalityType.Walking));
        return new MultimodalRoute(route, ModalChoice.Walking);
    }
}

// ReSharper disable once ClassNeverInstantiated.Global
public class TestPenetrationPedestrianLayer : AbstractMultimodalLayer
{
    public TestPenetrationPedestrianLayer(ISpatialGraphEnvironment environment)
    {
        SidewalkEnvironment = environment;
    }

    public IList<TestPenetrationPedestrian> Agents { get; set; }

    public ISpatialGraphEnvironment SidewalkEnvironment { get; private set; }

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        if (Mapping.File != null) SidewalkEnvironment = new SpatialGraphEnvironment(Mapping.File);

        SpatialGraphMediatorLayer ??= new SpatialGraphMediatorLayer
        {
            Environment = SidewalkEnvironment,
            Context = layerInitData.Context
        };

        var agentInitConfig = layerInitData.AgentInitConfigs.FirstOrDefault();
        if (agentInitConfig?.IndividualMapping == null) return false;

        var agentManager = layerInitData.Container.Resolve<IAgentManager>();
        Agents = agentManager.Spawn<TestPenetrationPedestrian, TestPenetrationPedestrianLayer>().ToList();

        return Agents.Count != 0;
    }
}