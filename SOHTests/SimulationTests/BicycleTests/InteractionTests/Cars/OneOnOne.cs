using System;
using System.Collections.Generic;
using System.IO;
using Mars.Components.Starter;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHBicycleModel.Model;
using SOHCarModel.Model;
using SOHResources;
using Xunit;

namespace SOHTests.SimulationTests.BicycleTests.InteractionTests.Cars
{
    [Collection("SimulationTests")]
    public class OneOnOne
    {
        [Fact]
        public void OneBicycleOneSlowCarTest()
        {
            var modelDescription = new ModelDescription();
            modelDescription.AddEntity<Car>();
            modelDescription.AddEntity<Bicycle>();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var start = SimulationContext.Start2020InSeconds.StartTimePoint;
            var config = new SimulationConfig
            {
                Globals =
                {
                    StartPoint = start,
                    EndPoint = start + TimeSpan.FromMinutes(10),
                    DeltaTUnit = TimeSpanUnit.Seconds
                },
                LayerMappings =
                {
                    new LayerMapping
                    {
                        Name = nameof(CarLayer),
                        File = Path.Combine("res", "networks", "square2lanes.graphml")
                    }
                },
                AgentMappings = new List<AgentMapping>
                {
                    new AgentMapping
                    {
                        Name = nameof(Cyclist),
                        InstanceCount = 1,
                        File = Path.Combine("res", "agent_inits", "OneFastBicycle.csv")
                    },
                    new AgentMapping
                    {
                        Name = nameof(CarDriver),
                        InstanceCount = 1,
                        File = Path.Combine("res", "agent_inits", "OneSlowCar.csv")
                    }
                },
                EntityMappings = new List<EntityMapping>
                {
                    new EntityMapping
                    {
                        Name = nameof(Car),
                        File = ResourcesConstants.CarCsv
                    },
                    new EntityMapping
                    {
                        Name = nameof(Bicycle),
                        File = ResourcesConstants.BicycleCsv
                    }
                }
            };

            var starter = SimulationStarter.Start(modelDescription, config);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);
        }

        [Fact]
        public void OneOnOneTest()
        {
            var modelDescription = new ModelDescription();
            modelDescription.AddEntity<Car>();
            modelDescription.AddEntity<Bicycle>();
            modelDescription.AddLayer<CarLayer>();
            modelDescription.AddAgent<CarDriver, CarLayer>();
            modelDescription.AddAgent<Cyclist, CarLayer>();

            var start = SimulationContext.Start2020InSeconds.StartTimePoint;
            var config = new SimulationConfig
            {
                Globals =
                {
                    StartPoint = start,
                    EndPoint = start + TimeSpan.FromMinutes(10),
                    DeltaTUnit = TimeSpanUnit.Seconds
                },
                LayerMappings =
                {
                    new LayerMapping
                    {
                        Name = nameof(CarLayer),
                        File = Path.Combine("res", "networks", "square2lanes.graphml")
                    }
                },
                AgentMappings = new List<AgentMapping>
                {
                    new AgentMapping
                    {
                        Name = nameof(Cyclist),
                        InstanceCount = 1,
                        File = Path.Combine("res", "agent_inits", "OneBicycle.csv")
                    },
                    new AgentMapping
                    {
                        Name = nameof(CarDriver),
                        InstanceCount = 1,
                        File = Path.Combine("res", "agent_inits", "OneCar.csv")
                    }
                },
                EntityMappings = new List<EntityMapping>
                {
                    new EntityMapping
                    {
                        Name = nameof(Car),
                        File = ResourcesConstants.CarCsv
                    },
                    new EntityMapping
                    {
                        Name = nameof(Bicycle),
                        File = ResourcesConstants.BicycleCsv
                    }
                }
            };

            var starter = SimulationStarter.Start(modelDescription, config);
            var workflowState = starter.Run();

            Assert.Equal(600, workflowState.Iterations);
        }
    }
}