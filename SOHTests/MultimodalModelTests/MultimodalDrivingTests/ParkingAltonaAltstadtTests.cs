using System.Collections.Generic;
using System.Linq;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalDrivingTests;

public class ParkingAltonaAltstadtTests
{
    private readonly CarParkingLayer _carParkingLayer;
    private readonly TestMultimodalLayer _multimodalLayer;

    public ParkingAltonaAltstadtTests()
    {
        var environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
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
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.CarDriving }
                    }
                }
            }
        });
        _carParkingLayer = new CarParkingLayerFixture(new StreetLayer { Environment = environment }).CarParkingLayer;
        _multimodalLayer = new TestMultimodalLayer(environment)
        {
            CarParkingLayer = _carParkingLayer
        };
    }

    private void CheckCarParkingSpacesHaveCarCountOf(int count)
    {
        var carParkingSpaces = _carParkingLayer.Features.OfType<CarParkingSpace>();
        var parkingSpacesWithCars = carParkingSpaces
            .Where(s => s.ParkingVehicles.Any()).ToList();

        Assert.Equal(count, parkingSpacesWithCars
            .Sum(s => s.ParkingVehicles.Count));
    }

    [Fact]
    public void AllCarsOccupyOneParkingSpotAltonaAltstadt()
    {
        Assert.InRange(_carParkingLayer.Features.Count, 3750, 3800);
        CheckCarParkingSpacesHaveCarCountOf(0);

        //TODO has problems with higher agent count < > do not find parking spots?
        const int agentCount = 30;
        var agents = new List<MultimodalAgent<TestMultimodalLayer>>();
        for (var i = 0; i < agentCount; i++)
        {
            var start = Position.CreatePosition(9.9497996, 53.5606333); //_sidewalk.GetRandomNode().Position;
            var goal = Position.CreatePosition(9.9467003, 53.5621657); //FindGoal(start);
            var agent = new TestMultiCapableAgent
            {
                StartPosition = start,
                GoalPosition = goal,
                ModalChoice = ModalChoice.CarDriving
            };
            agent.Init(_multimodalLayer);
            Assert.NotEmpty(agent.MultimodalRoute);
            Assert.Equal(ModalChoice.CarDriving, agent.MultimodalRoute.MainModalChoice);

            agents.Add(agent);
        }

        CheckCarParkingSpacesHaveCarCountOf(agentCount);

        var context = _multimodalLayer.Context;
        for (var tick = 0; tick < 2000 && !agents.TrueForAll(a => a.GoalReached); tick++, context.UpdateStep())
            foreach (var agent in agents)
                agent.Tick();


        Assert.All(agents, agent =>
        {
            Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
            Assert.Equal(0, agent.Velocity);
            Assert.True(agent.GoalReached);
        });
        CheckCarParkingSpacesHaveCarCountOf(agentCount);
    }
}