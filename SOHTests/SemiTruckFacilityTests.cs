using System;
using System.Collections.Generic;
using System.Reflection;
using Mars.Components.Environments;
using Mars.Interfaces;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Moq;
using SOHModel.Domain.Model;
using SOHModel.Database;
using SOHModel.SemiTruck.Model;
using SOHModel.SemiTruck.Model.Driver.Services;
using SOHModel.SemiTruck.Model.Driver.State;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;
using Xunit;

namespace SOHTests;

public class SemiTruckFacilityTests
{
    [Fact]
    public void EmptyTruckCannotMoveEvenWithNonzeroVelocity()
    {
        var environment = new Mock<ISpatialGraphEnvironment>(MockBehavior.Strict);
        var layer = new SemiTruckLayer { _tickDuration = TimeSpan.FromSeconds(1) };
        var (driver, _) = MakeDriver(layer, 0);
        var truck = driver.SemiTruck;
        truck.Environment = environment.Object;
        truck.Velocity = 20;
        Assert.True(truck.TryEnterDriver(driver, out var steering));
        steering.Move();
        Assert.Equal(0, truck.Velocity);
        environment.VerifyNoOtherCalls();
    }

    [Fact]
    public void EmptyTruckRecordsOneTerminalOutcomeAndCannotResumeOnLaterTicks()
    {
        var environment = new Mock<ISpatialGraphEnvironment>(MockBehavior.Strict);
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1) };
        var (driver, _) = MakeDriver(layer, 0);
        var truck = driver.SemiTruck;
        truck.Environment = environment.Object;
        Assert.True(truck.TryEnterDriver(driver, out var steering));
        SetDriverField(driver, "_steeringHandle", steering);
        SetDriverField(driver, "_environment", environment.Object);
        SetDriverField(driver, "_layer", layer);
        int unregistered = 0;
        SetDriverField(driver, "_unregister", new UnregisterAgent((_, _) => unregistered++));
        environment.Setup(e => e.Remove(truck)).Returns(true);
        driver.Tick();
        driver.Tick();
        Assert.Equal(TruckTripOutcome.ZeroEnergy, driver.TripOutcome);
        Assert.Equal(1, unregistered);
        environment.Verify(e => e.Remove(truck), Times.Once);
        environment.VerifyNoOtherCalls();
    }

    [Fact]
    public void LegalLimitStopsMovementBeforeAnotherTick()
    {
        var environment = new Mock<ISpatialGraphEnvironment>(MockBehavior.Strict);
        var layer = new SemiTruckLayer { _tickDuration = TimeSpan.FromSeconds(1) };
        var (driver, _) = MakeDriver(layer, 5);
        driver.DrivingTime.AddDriving(TimeSpan.FromHours(4.5));
        driver.SemiTruck.Environment = environment.Object;
        driver.SemiTruck.Velocity = 20;
        Assert.True(driver.SemiTruck.TryEnterDriver(driver, out var steering));
        steering.Move();
        Assert.Equal(0, driver.SemiTruck.Velocity);
        Assert.Throws<InvalidOperationException>(() => driver.DrivingTime.AddDriving(TimeSpan.FromSeconds(1)));
        environment.VerifyNoOtherCalls();
    }

    [Fact]
    public void RoutedApproachIncludesDetourSpeedLimitsAndTickQuantization()
    {
        var truck = new SemiTruck
        {
            MaxSpeed = 25,
            MaxFuelCarrierAmount = 100,
            FuelConsumptionPer100Km = 100,
            FuelCarrierType = FuelCarrierType.Battery,
            Tank2WheelEfficiency = 0.9
        };
        var fuel = new FuelConsumptionTracker(truck);
        var time = new DrivingTimeTracker();
        time.AddDriving(TimeSpan.FromHours(4.5) - TimeSpan.FromSeconds(60));
        ISpatialEdge Edge(double length, double speed)
        {
            var edge = new Mock<ISpatialEdge>();
            edge.SetupGet(e => e.Length).Returns(length);
            edge.SetupGet(e => e.MaxSpeed).Returns(speed);
            return edge.Object;
        }
        var main = Edge(700, 25); // 40s
        var detour = Edge(210, 10); // 30s, total 70s
        Assert.True(StopReachability.CanReach(new[] { main }, 0, truck, fuel, time, TimeSpan.FromSeconds(1)));
        Assert.False(StopReachability.CanReach(new[] { main, detour }, 0, truck, fuel, time, TimeSpan.FromSeconds(1)));
        Assert.False(StopReachability.CanReach(new[] { main }, 0, truck, fuel, time, TimeSpan.FromSeconds(61)));
        fuel.FuelCarrierAmount = 0.5; // only 500m range
        Assert.False(StopReachability.CanReach(new[] { main }, 0, truck, fuel, time, TimeSpan.FromSeconds(1)));
        Assert.True(StopReachability.CanReach(new[] { main }, 350, truck, fuel, time, TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void MissingRestFacilitiesNeverFallBackToShoulder()
    {
        var graph = new SpatialGraphEnvironment();
        var from = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.0, ["y"] = 53.0 });
        var to = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.01, ["y"] = 53.0 });
        var edge = graph.AddEdge(from, to, new Dictionary<string, object> { ["length"] = 700, ["lanes"] = 1 });
        var layer = new SemiTruckLayer { _tickDuration = TimeSpan.FromSeconds(1) };
        var (driver, fuel) = MakeDriver(layer, 10);
        driver.SemiTruck.Environment = graph;
        var steering = new SemiTruckSteeringHandle(graph, driver.SemiTruck);
        steering.Route.Add(edge);
        var state = new RestState();
        state.Request(new RestRequirement(StopFacility.RestArea, TimeSpan.FromMinutes(45), false));
        state.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => Assert.Fail("A roadside rest must not be planned."), driver.DrivingTime);
        Assert.True(state.PlanningFailed);
        Assert.False(state.IsActive);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CombinedStopFallsBackToReachableRestAreaBeforeDeclaringNoLegalStop(bool restAreaAvailable)
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = 53.0 });
        var start = Node(10);
        var rest = Node(10.01);
        var goal = Node(12);
        var route = new Route();
        route.Add(graph.AddEdge(start, rest,
            new Dictionary<string, object> { ["length"] = 1000, ["lanes"] = 1, ["maxspeed"] = 90 }));
        route.Add(graph.AddEdge(rest, goal,
            new Dictionary<string, object> { ["length"] = 200000, ["lanes"] = 1, ["maxspeed"] = 90 }));
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        if (restAreaAvailable)
            layer.AllRestAreas.Add(new SemiTruckLayer.RestArea { Id = 1, Lat = 53, Lon = 10.01 });
        var (driver, _) = MakeDriver(layer, 5);
        var truck = driver.SemiTruck;
        truck.Environment = graph;
        truck.FuelConsumptionPer100Km = 5;
        truck.MaxAcceleration = 1;
        truck.MaxDeceleration = 2;
        truck.Length = 5;
        truck.RestStrategy = RestStrategyType.ChargeWhileResting;
        truck.InitializeRestStrategy();
        graph.Insert(truck, start);
        Assert.True(truck.TryEnterDriver(driver, out var steering));
        steering.Route = route;
        SetDriverField(driver, "_steeringHandle", steering);
        SetDriverField(driver, "_environment", graph);
        SetDriverField(driver, "_layer", layer);
        SetDriverField(driver, "_routeManager", new RouteManager(layer, graph, truck, 3, null));
        SetDriverField(driver, "_unregister", new UnregisterAgent((_, _) => { }));
        driver.DrivingTime.AddDriving(TimeSpan.FromHours(3));

        driver.Tick();

        if (restAreaAvailable)
        {
            Assert.Null(driver.TripOutcome);
            Assert.Same(rest, steering.StopAtNode);
            Assert.True(route.RemainingRouteDistanceToGoal < 201000);
        }
        else
        {
            Assert.Equal(TruckTripOutcome.NoLegalStop, driver.TripOutcome);
            Assert.Equal(201000, route.RemainingRouteDistanceToGoal);
        }
    }

    [Fact]
    public void RejectedApproachDoesNotExcludeOtherApproachesToSameFacility()
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = 53.0 });
        var start = Node(10);
        var first = Node(10.01);
        var second = Node(10.02);
        var goal = Node(10.03);
        var route = new Route();
        foreach (var (from, to) in new[] { (start, first), (first, second), (second, goal) })
            route.Add(graph.AddEdge(from, to,
                new Dictionary<string, object> { ["length"] = 1000, ["lanes"] = 1 }));
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1) };
        layer.AllChargingStations.Add(new SemiTruckLayer.RefuelStation { Id = "charger", Lat = 53, Lon = 10.02 });
        var (driver, fuel) = MakeDriver(layer, 5);
        driver.SemiTruck.FuelConsumptionPer100Km = 5;
        var steering = new SemiTruckSteeringHandle(graph, driver.SemiTruck) { Route = route };
        var stop = new RefuelState();
        var requirement = new RestRequirement(StopFacility.ChargingStation, TimeSpan.FromMinutes(45), false);
        stop.RequestCombinedRest(requirement);
        ISpatialNode rejected = null;
        ISpatialNode accepted = null;
        stop.CheckAndPlan(steering, layer, driver.SemiTruck, fuel, (_, _, approach) =>
        {
            if (rejected == null)
            {
                rejected = approach;
                stop.CancelPlanned();
            }
            else
            {
                accepted = approach;
                stop.MarkPlanned();
            }
        }, driver.DrivingTime);

        Assert.NotNull(rejected);
        Assert.NotNull(accepted);
        Assert.NotSame(rejected, accepted);
        Assert.False(stop.PlanningFailed);

        stop.CancelPlanned();
        stop.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => stop.CancelPlanned(), driver.DrivingTime);
        Assert.True(stop.PlanningFailed);
        Assert.Equal(requirement with { Facility = StopFacility.RestArea }, stop.TakeRestAreaFallback());
        Assert.False(stop.PlanningFailed);
        Assert.False(stop.HasCombinedRestRequest);

        stop.RequestCombinedRest(requirement);
        stop.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => stop.MarkPlanned(), driver.DrivingTime);
        Assert.True(stop.IsActive);
        Assert.False(stop.PlanningFailed);
    }

    [Fact]
    public void CombinedRestMayUseEnergyReserveToReachALegalCharger()
    {
        var graph = new SpatialGraphEnvironment();
        var start = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.0, ["y"] = 53.0 });
        var charger = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.3, ["y"] = 53.0 });
        var goal = graph.AddNode(new Dictionary<string, object> { ["x"] = 12.0, ["y"] = 53.0 });
        var route = new Route();
        route.Add(graph.AddEdge(start, charger,
            new Dictionary<string, object> { ["length"] = 20000, ["lanes"] = 1 }));
        route.Add(graph.AddEdge(charger, goal,
            new Dictionary<string, object> { ["length"] = 200000, ["lanes"] = 1 }));
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1) };
        layer.AllChargingStations.Add(new SemiTruckLayer.RefuelStation { Id = "charger", Lat = 53, Lon = 10.3 });
        var (driver, fuel) = MakeDriver(layer, 3);
        driver.SemiTruck.FuelConsumptionPer100Km = 5; // 60 km range, including the optional 50 km reserve
        driver.SemiTruck.CurrentEdge = route[0].Edge;
        var steering = new SemiTruckSteeringHandle(graph, driver.SemiTruck) { Route = route };
        var stop = new RefuelState();
        stop.RequestCombinedRest(new RestRequirement(StopFacility.ChargingStation, TimeSpan.FromMinutes(45), false, 50000));
        stop.CheckAndPlan(steering, layer, driver.SemiTruck, fuel, (_, _, _) => stop.MarkPlanned(), driver.DrivingTime);
        Assert.True(stop.IsActive);
        Assert.False(stop.PlanningFailed);
    }
    [Fact]
    public void TruckStopsAtFacilityWithoutOvershootAndResumes()
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = 53.0 });
        var start = Node(10);
        var middle = Node(10.00015);
        var facility = Node(10.00030);
        var goal = Node(10.00045);
        ISpatialEdge Edge(ISpatialNode from, ISpatialNode to) => graph.AddEdge(from, to,
            new Dictionary<string, object> { ["length"] = 10, ["lanes"] = 1, ["maxspeed"] = 90 });
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        var (driver, fuel) = MakeDriver(layer, 5);
        var truck = driver.SemiTruck;
        truck.Environment = graph;
        truck.MaxAcceleration = 1;
        truck.MaxDeceleration = 2;
        truck.Length = 5;
        var route = new Route();
        route.Add(Edge(start, middle));
        route.Add(Edge(middle, facility));
        route.Add(Edge(facility, goal));
        graph.Insert(truck, start);
        Assert.True(truck.TryEnterDriver(driver, out var steering));
        steering.Route = route;
        steering.StopAtNode = facility;
        truck.Velocity = 25;
        steering.Move();
        Assert.Equal(10, route.RemainingRouteDistanceToGoal, 6);

        var stop = new RefuelState();
        stop.SetTargetNode(facility);
        stop.MarkPlanned();
        Assert.True(stop.HandlePause(steering, layer, truck, fuel, driver));
        Assert.Equal(0, truck.Velocity);
        layer._tickDuration = TimeSpan.FromMinutes(1);
        layer._simulationTime += layer._tickDuration;
        Assert.False(stop.HandlePause(steering, layer, truck, fuel, driver));
        Assert.Equal(8, fuel.FuelCarrierAmount, 6);
        steering.StopAtNode = null;
        layer._tickDuration = TimeSpan.FromSeconds(1);
        steering.Move();
        Assert.True(route.RemainingRouteDistanceToGoal < 10, "The return leg must continue, without teleporting to the approach edge.");
    }

    [Fact]
    public void DriverDebitsEveryMovementIncludingFinalTickBeforeCompletion()
    {
        var graph = new SpatialGraphEnvironment();
        var start = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.0, ["y"] = 53.0 });
        var goal = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.00045, ["y"] = 53.0 });
        var edge = graph.AddEdge(start, goal,
            new Dictionary<string, object> { ["length"] = 30, ["lanes"] = 1, ["maxspeed"] = 90 });
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        var (driver, fuel) = MakeDriver(layer, 5);
        var truck = driver.SemiTruck;
        truck.Environment = graph;
        truck.FuelConsumptionPer100Km = 100;
        truck.MaxAcceleration = 1;
        truck.MaxDeceleration = 2;
        truck.Length = 5;
        graph.Insert(truck, start);
        Assert.True(truck.TryEnterDriver(driver, out var steering));
        steering.Route.Add(edge);
        SetDriverField(driver, "_steeringHandle", steering);
        SetDriverField(driver, "_environment", graph);
        SetDriverField(driver, "_layer", layer);
        SetDriverField(driver, "_routeManager", new RouteManager(layer, graph, truck, 3, null));
        int unregistered = 0;
        SetDriverField(driver, "_unregister", new UnregisterAgent((_, _) => unregistered++));
        for (int tick = 0; tick < 100 && !driver.TripOutcome.HasValue; tick++)
        {
            driver.Tick();
            if (tick == 0) Assert.True(fuel.FuelCarrierAmount < 5);
            layer._simulationTime += layer._tickDuration;
        }
        Assert.Equal(TruckTripOutcome.Completed, driver.TripOutcome);
        Assert.Equal(4.97, fuel.FuelCarrierAmount, 6);
        Assert.Equal(1, unregistered);
    }

    private static (SemiTruckDriver Driver, FuelConsumptionTracker Fuel) MakeDriver(SemiTruckLayer layer, double amount)
    {
        var truck = new SemiTruck
        {
            ID = Guid.NewGuid(),
            Layer = layer,
            FuelCarrierType = FuelCarrierType.Battery,
            MaxFuelCarrierAmount = 10,
            MaxChargePowerKw = 1000,
            ChargeTargetSoc = 0.8,
            Tank2WheelEfficiency = 0.9,
            MaxSpeed = 25,
            Position = Position.CreatePosition(10, 53)
        };
        truck.InitializeFuelStrategy();
        var fuel = new FuelConsumptionTracker(truck) { FuelCarrierAmount = amount };
        var driver = new SemiTruckDriver { SemiTruck = truck };
        SetDriverField(driver, "_fuelTracker", fuel);
        return (driver, fuel);
    }

    private static void SetDriverField(SemiTruckDriver driver, string field, object value) =>
        typeof(SemiTruckDriver).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(driver, value);

    private sealed class TestTruckLayer : SemiTruckLayer
    {
        public TestTruckLayer()
        {
            Context = SimulationContext.Start2020InSeconds;
            _simulationTime = new DateTime(2026, 1, 1);
        }
    }
}
