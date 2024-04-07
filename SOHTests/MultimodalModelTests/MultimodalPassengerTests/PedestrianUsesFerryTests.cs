using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using SOHModel.Domain.Steering.Common;
using SOHModel.Ferry.Model;
using SOHModel.Ferry.Station;
using SOHModel.Multimodal.Multimodal;
using SOHTests.Commons.Agent;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.MultimodalModelTests.MultimodalPassengerTests;

public class PedestrianUsesFerryTests
{
    private readonly ISpatialGraphEnvironment _environment;
    private readonly Ferry _ferry;
    private readonly FerryDriver _ferryDriver;
    private readonly FerryStationLayer _ferryStationLayer;
    private readonly TestMultimodalLayer _layer;

    private readonly Position _start, _goal;

    public PedestrianUsesFerryTests()
    {
        var routeLayerFixture = new FerryRouteLayerFixture();
        _ferryStationLayer = routeLayerFixture.FerryStationLayer;

        _environment = new SpatialGraphEnvironment(new SpatialGraphOptions
        {
            GraphImports = new List<Input>
            {
                new()
                {
                    File = ResourcesConstants.WalkGraphLandungsbruecken,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.Walking }
                    }
                },
                new()
                {
                    File = ResourcesConstants.FerryGraph,
                    InputConfiguration = new InputConfiguration
                    {
                        IsBiDirectedImport = true,
                        NodeIntegrationKind = NodeIntegrationKind.MergeNode,
                        NodeToleranceInMeter = 10,
                        Modalities = new HashSet<SpatialModalityType> { SpatialModalityType.ShipDriving }
                    }
                }
            }
        });

        _layer = new TestMultimodalLayer(_environment)
        {
            FerryStationLayer = _ferryStationLayer
        };
        _start = Position.CreateGeoPosition(9.9700038, 53.5464827); //near Landungsbrücken
        _goal = Position.CreateGeoPosition(9.9498396, 53.5451737); //near Fischmarkt

        var ferryLayer = new FerryLayer(routeLayerFixture.FerryRouteLayer)
        {
            Context = SimulationContext.Start2020InSeconds,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.FerryCsv)),
            GraphEnvironment = _environment
        };
        _ferryDriver = new FerryDriver(ferryLayer, (_, _) => { })
        {
            Line = 62,
            MinimumBoardingTimeInSeconds = 10
        };
        _ferryDriver.Ferry.Position = _ferryDriver.Position;
        _ferry = _ferryDriver.Ferry;
    }

    [Fact]
    public void EnterWaitingFerryAtStation()
    {
        Assert.Null(_ferry.FerryStation);
        var ferryStation = _ferryStationLayer.Nearest(_start);
        Assert.False(ferryStation.Ferries.Any());

        _ferryDriver.Tick();
        Assert.True(ferryStation.Ferries.Any());
        Assert.NotNull(_ferry.FerryStation);
        Assert.Equal("Landungsbrücken Brücke 1", _ferry.FerryStation.Name);

        var agent = new TestPassengerPedestrian
        {
            StartPosition = _start
        };
        agent.Init(_layer);
        agent.MultimodalRoute = _layer.Search(agent, _start, _goal, ModalChoice.Ferry);

        // enter ferry station
        Assert.Equal(_start, agent.Position);
        for (var tick = 0; tick < 10000 && !agent.HasUsedFerry; tick++, _layer.Context.UpdateStep())
            agent.Tick();

        Assert.True(agent.HasUsedFerry);
        Assert.Equal(_ferry.Position, agent.Position);
    }

    [Fact]
    public void EnterLaterArrivingFerryAtStation()
    {
        var agent = new TestPassengerPedestrian
        {
            StartPosition = _start
        };
        agent.Init(_layer);
        agent.MultimodalRoute = _layer.Search(agent, _start, _goal, ModalChoice.Ferry);

        Assert.Equal(_start, agent.Position);

        for (var tick = 0; tick < 500; tick++, _layer.Context.UpdateStep()) agent.Tick();

        Assert.False(agent.HasUsedFerry);

        _ferryDriver.Tick();
        Assert.NotNull(_ferry.FerryStation);

        Assert.InRange(_ferry.Position.DistanceInMTo(agent.Position), 0, 7);

        for (var tick = 0; tick < 200; tick++, _layer.Context.UpdateStep()) agent.Tick();

        Assert.True(agent.HasUsedFerry);
    }

    [Fact]
    public void EnterCommutingFerryAndLeaveItAtGoal()
    {
        var agent = new TestPassengerPedestrian { StartPosition = _start };
        agent.Init(_layer);
        agent.MultimodalRoute = _layer.Search(agent, _start, _goal, ModalChoice.Ferry);
        Assert.Equal(3, agent.MultimodalRoute.Count);
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        var station = _ferryStationLayer.Nearest(_start);
        Assert.Empty(station.Ferries);
        var ferryStationNodePosition = _environment.NearestNode(station.Position).Position;

        // walk to station
        Assert.Equal(_start, agent.Position);
        Assert.NotEqual(_start, ferryStationNodePosition);
        for (var tick = 0; tick < 300; tick++, _layer.Context.UpdateStep())
            agent.Tick();
        Assert.Equal(ferryStationNodePosition, agent.Position);
        Assert.Equal(1, agent.MultimodalRoute.PassedStops);
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);

        // agent enters ferry
        _ferryDriver.Tick(); //ferry docks in station
        for (var tick = 0; tick < 200; tick++, _layer.Context.UpdateStep())
            agent.Tick();
        Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
        Assert.Equal(1, agent.MultimodalRoute.PassedStops);
        Assert.Equal(ModalChoice.Ferry, agent.MultimodalRoute.CurrentModalChoice);
        Assert.True(agent.HasUsedFerry);

        // ferry leaves start station and moves to goal station
        Assert.True(station.Leave(_ferry));
        var goalStation = _ferryStationLayer.Nearest(agent.MultimodalRoute.CurrentRoute.Goal);
        _ferry.Position = _environment.NearestNode(goalStation.Position).Position;
        Assert.True(goalStation.Enter(_ferry));
        _ferry.NotifyPassengers(PassengerMessage.GoalReached);

        // walk to goal
        for (var tick = 0; tick < 1; tick++, _layer.Context.UpdateStep()) agent.Tick();
        Assert.Equal(2, agent.MultimodalRoute.PassedStops);

        Assert.Equal(Whereabouts.Sidewalk, agent.Whereabouts);
    }

    [Fact]
    public void WalkFerryWalkToGoal()
    {
        var agent = new TestPassengerPedestrian { StartPosition = _start };
        agent.Init(_layer);
        agent.MultimodalRoute = _layer.Search(agent, _start, _goal, ModalChoice.Ferry);

        var station = _ferryStationLayer.Nearest(_start);
        Assert.Empty(station.Ferries);

        Assert.Equal(_start, agent.Position);
        for (var tick = 0; tick < 250; tick++, _layer.Context.UpdateStep())
            agent.Tick();

        _ferryDriver.Tick();

        var ferryStationNode = _environment.NearestNode(_ferry.Position);
        Assert.Equal(ferryStationNode.Position, agent.Position);
        Assert.Equal(1, agent.MultimodalRoute.PassedStops);

        for (var tick = 0; tick < 200; tick++, _layer.Context.UpdateStep())
            agent.Tick();
        Assert.Equal(Whereabouts.Vehicle, agent.Whereabouts);
        Assert.True(agent.HasUsedFerry);

        Assert.True(station.Leave(_ferry));

        Assert.Equal(ModalChoice.Ferry, agent.MultimodalRoute.CurrentModalChoice);
        var goalStation = _ferryStationLayer.Nearest(agent.MultimodalRoute.CurrentRoute.Goal);
        _ferry.Position = _environment.NearestNode(goalStation.Position).Position;

        Assert.True(goalStation.Enter(_ferry));
        _ferry.NotifyPassengers(PassengerMessage.GoalReached);

        for (var tick = 0; tick < 300; tick++, _layer.Context.UpdateStep())
            agent.Tick();
        Assert.Equal(Whereabouts.Offside, agent.Whereabouts);
        Assert.True(agent.GoalReached);
        Assert.InRange(agent.Position.DistanceInMTo(_goal), 0, 0.01);
    }
}