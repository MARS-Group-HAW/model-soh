using System;
using System.Collections.Generic;
using System.Linq;
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

    [Theory]
    [InlineData("[100,100]", true)]
    [InlineData("[100,600]", false)]
    [InlineData("[100,600,100]", false)] // the summit must be reachable, too
    [InlineData("[600,100]", true)]
    public void ReachabilityAccountsForElevationGainOnTheApproach(string elevation, bool reachable)
    {
        var truck = new SemiTruck
        {
            MaxSpeed = 25,
            Mass = 36000,
            MaxFuelCarrierAmount = 100,
            FuelConsumptionPer100Km = 100,
            FuelCarrierType = FuelCarrierType.Battery,
            Tank2WheelEfficiency = 0.9
        };
        var fuel = new FuelConsumptionTracker(truck) { FuelCarrierAmount = 20 }; // flat range 20 km
        var edge = new Mock<ISpatialEdge>();
        edge.SetupGet(e => e.Length).Returns(10000);
        edge.SetupGet(e => e.MaxSpeed).Returns(25);
        edge.SetupGet(e => e.Attributes).Returns(new Dictionary<string, object> { ["elevation"] = elevation });
        // Climbing 500 m with 36 t takes about 49 kWh at the wheels on top of the 10 km.
        Assert.Equal(reachable, StopReachability.CanReach(new[] { edge.Object }, 0, truck, fuel,
            new DrivingTimeTracker(), TimeSpan.FromSeconds(1)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingRestFacilitiesNeverFallBackToShoulder(bool routed)
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
            (_, _, _) => Assert.Fail("A roadside rest must not be planned."), driver.DrivingTime,
            routed ? driver.CreateStopSearch(state) : null);
        Assert.True(state.PlanningFailed);
        Assert.False(state.IsActive);
        if (routed)
        {
            Assert.Contains("0 candidate approaches", state.PlanningFailureDetails);
            Assert.DoesNotContain("Rejections:", state.PlanningFailureDetails);
        }
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
        var restState = new RecordingRestState();
        var refuelState = new TestRefuelState();
        SetDriverField(driver, "_restState", restState);
        SetDriverField(driver, "_refuelState", refuelState);

        driver.Tick();

        Assert.Empty(refuelState.Events);
        if (restAreaAvailable)
        {
            var planned = Assert.Single(restState.Events);
            Assert.Equal(RestEventType.Planned, planned.EventType);
            Assert.Equal(RestStateType.Rest, planned.StateType);
            Assert.Null(driver.TripOutcome);
            Assert.Same(rest, steering.StopAtNode);
            Assert.True(steering.Route.RemainingRouteDistanceToGoal < 201000);

            for (int tick = 0; tick < 200 && !restState.IsPausing; tick++)
            {
                layer._simulationTime += layer._tickDuration;
                driver.Tick();
            }
            Assert.True(restState.IsPausing);
            layer._simulationTime += TimeSpan.FromMinutes(45);
            Assert.False(restState.HandlePause(steering, layer, driver));
            Assert.Collection(restState.Events,
                entry => Assert.Equal(RestEventType.Planned, entry.EventType),
                entry => Assert.Equal(RestEventType.Start, entry.EventType),
                entry => Assert.Equal(RestEventType.End, entry.EventType));
            Assert.All(restState.Events, entry => Assert.Equal(RestStateType.Rest, entry.StateType));
        }
        else
        {
            Assert.Empty(restState.Events);
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
        var stop = new TestRefuelState();
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
        var planned = Assert.Single(stop.Events);
        Assert.Equal(RestEventType.Planned, planned.EventType);
        Assert.Equal(RestStateType.RestAndRefuel, planned.StateType);
        stop.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => Assert.Fail("An accepted stop must not be planned again."), driver.DrivingTime);
        Assert.Single(stop.Events);

        stop.CancelPlanned();
        stop.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => stop.CancelPlanned(), driver.DrivingTime);
        Assert.True(stop.PlanningFailed);
        Assert.Single(stop.Events);
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
        // The edge leading to the stop is not driven a second time after resuming.
        Assert.Same(facility, steering.Route[0].Edge.From);
        Assert.Equal(10, steering.Route.RemainingRouteDistanceToGoal, 6);
        steering.Move();
        Assert.True(steering.Route.RemainingRouteDistanceToGoal < 10, "The return leg must continue from the facility.");
    }

    [Theory]
    [InlineData(53.005, true)]
    [InlineData(53.02, false)]
    public void FacilityBesideTheRouteIsEnteredWithoutDetour(double facilityLat, bool roadside)
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon, double lat = 53) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = lat });
        ISpatialEdge Edge(ISpatialNode from, ISpatialNode to, double length) =>
            graph.AddEdge(from, to, new Dictionary<string, object>
            { ["length"] = length, ["lanes"] = 1, ["maxspeed"] = 90 });
        var start = Node(10);
        var exit = Node(10.01);
        var passing = Node(10.02);
        var goal = Node(10.03);
        // The only other road near the facility lies on the far side (e.g. the opposite carriageway).
        var far = Node(10.017, 53.03);
        var first = Edge(start, exit, 700);
        var beside = Edge(exit, passing, 700);
        var last = Edge(passing, goal, 700);
        Edge(exit, far, 3000);
        Edge(far, passing, 3000);
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        var (driver, _) = MakeDriver(layer, 5);
        var truck = driver.SemiTruck;
        truck.FuelConsumptionPer100Km = 5;
        truck.CurrentEdge = first;
        var steering = new SemiTruckSteeringHandle(graph, truck) { Route = new Route { first, beside, last } };
        var manager = new RouteManager(layer, graph, truck, 3, null);

        var plan = manager.CreateStopRoute(facilityLat, 10.017, exit, steering, driver);

        Assert.NotNull(plan);
        Assert.Same(roadside ? passing : far, plan.StopNode);
        if (!roadside) return; // a facility with its own network node is still reached via that node
        Assert.Equal(new[] { first, beside, last }, plan.Route.Stops.Select(step => step.Edge));
        Assert.Equal(2100, plan.Route.RemainingRouteDistanceToGoal, 6);
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

    [Theory]
    [InlineData("forward")]
    [InlineData("corridor")]
    [InlineData("return")]
    [InlineData("energy")]
    [InlineData("legal")]
    [InlineData("closed")]
    [InlineData("height")]
    [InlineData("dead-end")]
    public void StopRoutingRejoinsForwardOnlyWithACompleteSafeRoute(string scenario)
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon, double lat = 53) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = lat });
        ISpatialEdge Edge(ISpatialNode from, ISpatialNode to, double length) =>
            graph.AddEdge(from, to, new Dictionary<string, object>
            { ["length"] = length, ["lanes"] = 1, ["maxspeed"] = 90 });
        var start = Node(10);
        var exit = Node(10.01);
        var rejoin = Node(10.02);
        var goal = Node(10.03);
        var facility = Node(10.015, 53.001);
        var first = Edge(start, exit, 1000);
        var skipped = Edge(exit, rejoin, 1000);
        var last = Edge(rejoin, goal, scenario == "corridor" ? 200000 : 1000);
        var entry = Edge(exit, facility, 100);
        var forward = Edge(facility, rejoin, 100);
        var reverse = scenario == "return" ? Edge(facility, exit, 100) : null;
        if (scenario == "corridor") Edge(facility, goal, 500);
        if (scenario == "height") entry.Attributes["maxheight"] = "3";
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        if (scenario == "closed" || scenario == "return") layer.RemovedEdges.Add(forward);
        if (scenario == "dead-end") layer.RemovedEdges.Add(last);
        var (driver, _) = MakeDriver(layer, scenario == "energy" ? 0.001 : 5);
        var truck = driver.SemiTruck;
        truck.Height = 4;
        truck.FuelConsumptionPer100Km = 5;
        truck.CurrentEdge = first;
        truck.PositionOnCurrentEdge = 100;
        if (scenario == "legal")
            driver.DrivingTime.AddDriving(TimeSpan.FromHours(4.5) - TimeSpan.FromSeconds(10));
        var original = new Route { first, skipped, last };
        var steering = new SemiTruckSteeringHandle(graph, truck) { Route = original };
        var manager = new RouteManager(layer, graph, truck, 3, null);

        var rejections = new List<StopRouteRejection>();
        var plan = manager.CreateStopRoute(facility.Position.Latitude, facility.Position.Longitude,
            exit, steering, driver, reportRejection: rejections.Add);

        Assert.Same(original, steering.Route);
        if (scenario != "forward" && scenario != "corridor" && scenario != "return")
        {
            Assert.Null(plan);
            Assert.Equal(scenario switch
            {
                "energy" or "legal" => StopRouteRejection.EnergyOrDrivingLimit,
                "height" => StopRouteRejection.NoApproachRoute,
                _ => StopRouteRejection.NoRejoinRoute
            }, Assert.Single(rejections));
            return;
        }
        Assert.NotNull(plan);
        Assert.Empty(rejections);
        Assert.Same(facility, plan.StopNode);
        Assert.Equal(scenario == "return" ? new[] { first, entry, reverse, skipped, last } :
            new[] { first, entry, forward, last }, plan.Route.Stops.Select(step => step.Edge));
        Assert.Equal(scenario == "corridor" ? 201100 : scenario == "return" ? 3100 : 2100,
            plan.Route.RemainingRouteDistanceToGoal, 6);
        ISpatialNode accepted = null;
        manager.ApplyStopRoute(plan, steering, driver, node => accepted = node);
        Assert.Same(facility, accepted);
        Assert.Same(first, steering.Route[0].Edge);
        Assert.Equal(100, truck.PositionOnCurrentEdge);
    }

    [Theory]
    [InlineData(false, 10.01)]
    [InlineData(true, 10.02)]
    public void StopSelectionMinimizesElapsedTimeThenCharging(bool equalTime, double expectedLongitude)
    {
        var graph = new SpatialGraphEnvironment();
        var start = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.0, ["y"] = 53.0 });
        var first = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.01, ["y"] = 53.0 });
        var second = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.02, ["y"] = 53.0 });
        var goal = graph.AddNode(new Dictionary<string, object> { ["x"] = 10.03, ["y"] = 53.0 });
        var route = new Route();
        foreach (var (from, to) in new[] { (start, first), (first, second), (second, goal) })
            route.Add(graph.AddEdge(from, to,
                new Dictionary<string, object> { ["length"] = 1000, ["lanes"] = 1, ["maxspeed"] = 90 }));
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        layer.AllChargingStations.Add(new SemiTruckLayer.RefuelStation { Id = "fast", Lat = 53, Lon = 10.01 });
        layer.AllChargingStations.Add(new SemiTruckLayer.RefuelStation { Id = "less-charge", Lat = 53, Lon = 10.02 });
        var (driver, fuel) = MakeDriver(layer, 5);
        var steering = new SemiTruckSteeringHandle(graph, driver.SemiTruck) { Route = route };
        var state = new CostedRefuelState(equalTime);
        state.RequestCombinedRest(new RestRequirement(StopFacility.ChargingStation, TimeSpan.FromMinutes(45), false));
        StopRoutePlan chosen = null;
        var routing = new StopRouteSearch((_, lon, node) =>
        {
            if (node.Position.Longitude != lon) return null;
            return new StopRoutePlan(route, node, 60, 1000, 60, 1000);
        }, plan =>
        {
            chosen = plan;
            state.SetTargetNode(plan.StopNode);
            state.MarkPlanned();
        });

        state.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => Assert.Fail("Only the selected, evaluated route should be applied."),
            driver.DrivingTime, routing);

        Assert.NotNull(chosen);
        Assert.Equal(expectedLongitude, chosen.StopNode.Position.Longitude);
    }

    [Fact]
    public void DailyRestChargesToFullOnlyWithinTheRestTime()
    {
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromMinutes(1) };
        var (driver, fuel) = MakeDriver(layer, 0);
        driver.SemiTruck.ChargeTargetSoc = 0;
        var state = new TestRefuelState();
        state.RequestCombinedRest(new RestRequirement(StopFacility.ChargingStation,
            SemiTruckDriverConstants.DailyRestDuration, true));
        state.Arrive(layer, driver, fuel);
        layer._simulationTime += layer._tickDuration;
        state.Advance(layer, driver, fuel);
        // Charging beyond the default target is free while the rest still runs.
        Assert.Equal(10, fuel.FuelCarrierAmount, 6);
        Assert.False(state.Completed(layer, driver.SemiTruck));
    }

    [Fact]
    public void FacilityChoiceUsesRoutedTimeRatherThanFarthestApproach()
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon, double lat = 53) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = lat });
        ISpatialEdge Edge(ISpatialNode from, ISpatialNode to, double length) =>
            graph.AddEdge(from, to, new Dictionary<string, object>
            { ["length"] = length, ["lanes"] = 1, ["maxspeed"] = 90 });
        var start = Node(10);
        var exit = Node(10.01);
        var forward = Node(10.02);
        var goal = Node(12);
        var quickRest = Node(10.015, 53.001);
        var slowRest = Node(10.025, 53.001);
        var route = new Route { Edge(start, exit, 1000), Edge(exit, forward, 1000), Edge(forward, goal, 200000) };
        Edge(exit, quickRest, 100);
        Edge(quickRest, forward, 100);
        Edge(forward, slowRest, 5000);
        Edge(slowRest, goal, 200000);
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        layer.AllRestAreas.Add(new SemiTruckLayer.RestArea { Id = 1, Lat = 53.001, Lon = 10.015 });
        layer.AllRestAreas.Add(new SemiTruckLayer.RestArea { Id = 2, Lat = 53.001, Lon = 10.025 });
        var (driver, fuel) = MakeDriver(layer, 5);
        driver.DrivingTime.AddDriving(TimeSpan.FromHours(3));
        var steering = new SemiTruckSteeringHandle(graph, driver.SemiTruck) { Route = route };
        var manager = new RouteManager(layer, graph, driver.SemiTruck, 3, null);
        var state = new RecordingRestState();
        state.Request(new RestRequirement(StopFacility.RestArea, TimeSpan.FromMinutes(45), false));
        var routing = new StopRouteSearch(
            (lat, lon, node) => manager.CreateStopRoute(lat, lon, node, steering, driver),
            plan => manager.ApplyStopRoute(plan, steering, driver, node =>
            {
                state.SetTargetNode(node);
                state.MarkPlanned();
            }));

        state.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => Assert.Fail("Only an evaluated route should be applied."), driver.DrivingTime, routing);

        Assert.Same(quickRest, state.TargetNode);
        Assert.Equal(201200, steering.Route.RemainingRouteDistanceToGoal, 6);
        Assert.Single(state.Events);
    }

    [Fact]
    public void SafeChargerBeyondSearchHorizonCanUseTheOptionalArrivalReserve()
    {
        var graph = new SpatialGraphEnvironment();
        ISpatialNode Node(double lon) =>
            graph.AddNode(new Dictionary<string, object> { ["x"] = lon, ["y"] = 53.0 });
        var start = Node(10);
        var charger = Node(11);
        var goal = Node(11.1);
        var route = new Route();
        route.Add(graph.AddEdge(start, charger,
            new Dictionary<string, object> { ["length"] = 120000, ["lanes"] = 1, ["maxspeed"] = 90 }));
        route.Add(graph.AddEdge(charger, goal,
            new Dictionary<string, object> { ["length"] = 30000, ["lanes"] = 1, ["maxspeed"] = 90 }));
        var layer = new TestTruckLayer { _tickDuration = TimeSpan.FromSeconds(1), Environment = graph };
        layer.AllChargingStations.Add(new SemiTruckLayer.RefuelStation { Id = "charger", Lat = 53, Lon = 11 });
        var (driver, fuel) = MakeDriver(layer, 7);
        driver.SemiTruck.FuelConsumptionPer100Km = 5; // 140 km range
        var steering = new SemiTruckSteeringHandle(graph, driver.SemiTruck) { Route = route };
        var manager = new RouteManager(layer, graph, driver.SemiTruck, 3, null);
        var state = new TestRefuelState();
        var routing = new StopRouteSearch(
            (lat, lon, node) => manager.CreateStopRoute(lat, lon, node, steering, driver),
            plan => manager.ApplyStopRoute(plan, steering, driver, node =>
            {
                state.SetTargetNode(node);
                state.MarkPlanned();
            }));

        state.CheckAndPlan(steering, layer, driver.SemiTruck, fuel,
            (_, _, _) => Assert.Fail("Only an evaluated route should be applied."), driver.DrivingTime, routing);

        Assert.False(state.PlanningFailed);
        Assert.Same(charger, state.TargetNode);
        Assert.Single(state.Events);
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

    private sealed class TestRefuelState : RefuelState
    {
        public List<RestEntity> Events { get; } = new();

        protected override void LogRestEvent(RestEntity entry) => Events.Add(entry);

        public void Arrive(SemiTruckLayer layer, SemiTruckDriver driver, FuelConsumptionTracker fuel)
        {
            _isPausing = true;
            _stopStartTime = layer._simulationTime;
            FacilityLat = 53;
            FacilityLon = 10;
        }

        public void Advance(SemiTruckLayer layer, SemiTruckDriver driver, FuelConsumptionTracker fuel) =>
            OnPauseTick(layer, driver.SemiTruck, fuel, driver);

        public bool Completed(SemiTruckLayer layer, SemiTruck truck) => IsPauseCompleted(layer, truck);
    }

    private sealed class RecordingRestState : RestState
    {
        public List<RestEntity> Events { get; } = new();

        protected override void LogRestEvent(RestEntity entry) => Events.Add(entry);
    }

    private sealed class CostedRefuelState : RefuelState
    {
        private readonly bool _equalTime;

        public CostedRefuelState(bool equalTime) => _equalTime = equalTime;

        protected override (double Seconds, double Charge) EstimateStopCost(StopRoutePlan plan,
            SemiTruckLayer layer, SemiTruck truck, FuelConsumptionTracker fuel, object facility) =>
            plan.StopNode.Position.Longitude < 10.02 ? (2700, 5) : (_equalTime ? 2700 : 2800, 1);
    }

    private sealed class TestTruckLayer : SemiTruckLayer
    {
        public TestTruckLayer()
        {
            Context = SimulationContext.Start2020InSeconds;
            _simulationTime = new DateTime(2026, 1, 1);
        }
    }
}
