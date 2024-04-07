using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.Core.Logging;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Layers;
using SOHModel.Multimodal.Model;
using Xunit;

namespace SOHTests.SimulationTests.DailyRoutineTests;

[Collection("SimulationTests")]
public class WalkingDailyRoutineTests
{
    [Fact]
    public void SimulateOneDay()
    {
        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>(new[] { typeof(ISpatialGraphLayer) });

        description.AddLayer<VectorBuildingsLayer>();
        description.AddLayer<VectorLanduseLayer>();
        description.AddLayer<VectorPoiLayer>();
        description.AddLayer<MediatorLayer>();

        description.AddLayer<CitizenLayer>();
        description.AddAgent<Citizen, CitizenLayer>();

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
                EndPoint = startPoint + TimeSpan.FromHours(24),
                DeltaTUnit = TimeSpanUnit.Seconds,
                OutputTarget = OutputTargetType.Csv
            },
            LayerMappings =
            {
                new LayerMapping
                {
                    Name = nameof(VectorBuildingsLayer),
                    File = ResourcesConstants.BuildingsAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(VectorLanduseLayer),
                    File = ResourcesConstants.LanduseAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(VectorPoiLayer),
                    File = ResourcesConstants.PoisAltonaAltstadt
                },
                new LayerMapping
                {
                    Name = nameof(SpatialGraphMediatorLayer),
                    Inputs = new List<Input>
                    {
                        new()
                        {
                            File = ResourcesConstants.WalkGraphAltonaAltstadt,
                            InputConfiguration = new InputConfiguration
                                { Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking } }
                        },
                        new()
                        {
                            File = ResourcesConstants.DriveGraphAltonaAltstadt,
                            InputConfiguration = new InputConfiguration
                            {
                                Modalities = new HashSet<SpatialModalityType>
                                    { SpatialModalityType.Cycling, SpatialModalityType.CarDriving }
                            }
                        }
                    }
                }
            },
            AgentMappings =
            {
                new AgentMapping
                {
                    Name = nameof(Citizen),
                    InstanceCount = 1,
                    IndividualMapping =
                    {
                        new IndividualMapping { Name = "ResultTrajectoryEnabled", Value = true },
                        new IndividualMapping { Name = "worker", Value = true },
                        new IndividualMapping { Name = "partTimeWorker", Value = false },
                        new IndividualMapping { Name = "gender", Value = GenderType.Male },
                        new IndividualMapping
                            { Name = "StartPosition", Value = Position.CreatePosition(9.945432, 53.550941) }
                    },
                    OutputFilter =
                    {
                        new OutputFilter
                        {
                            Name = "StoreTickResult",
                            Values = new object[] { true },
                            Operator = ContainsOperator.In
                        }
                    }
                }
            }
        };

        LoggerFactory.SetLogLevel(LogLevel.Off);
        var application = SimulationStarter.BuildApplication(description, config);
        var simulation = application.Resolve<ISimulation>();

        var state = simulation.StartSimulation();

        Assert.Equal(86400, state.Iterations);

        Assert.Single(state.Model.ExecutionGroups);
        Assert.Single(state.Model.ExecutionGroups[1]);

        var citizen = state.Model.ExecutionGroups[1].OfType<Citizen>().First();

        Assert.NotNull(citizen.Home);
        Assert.NotNull(citizen.Work);
        Assert.NotNull(citizen.Tour);
        Assert.NotNull(citizen.Tour.Current);
        Assert.False(citizen.PartTimeWorker);
        Assert.True(citizen.Worker);
        Assert.Equal(GenderType.Male, citizen.Gender);
    }
}