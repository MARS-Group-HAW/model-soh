using System.Collections.Generic;
using Mars.Components.Environments;
using Mars.Interfaces;
using SOHTests.Commons.Agent;
using Xunit;

namespace SOHTests.CarModelTests.OvertakingTests;

public class OvertakingInRingRoadTest
{
    [Fact]
    public void OvertakingDeactivated()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var slowDriver = new InfiniteSteeringDriver(context, 20, graph, 0, 5);
        var fastDriver = new InfiniteSteeringDriver(context, 0, graph, 0, 10);

        Assert.False(fastDriver.OvertakingActivated);
        Assert.False(slowDriver.OvertakingActivated);

        for (var i = 0; i < 1000; i++, context.UpdateStep())
        {
            fastDriver.Tick();
            slowDriver.Tick();

            Assert.InRange(fastDriver.Velocity, 0, slowDriver.MaxSpeed * 1.3);
            Assert.InRange(slowDriver.Velocity, 0, slowDriver.MaxSpeed);

            Assert.True(slowDriver.TotalDistanceDriven > fastDriver.TotalDistanceDriven);
        }
    }

    [Fact]
    public void OvertakeOneCarOnOtherwiseFreeEdge()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var slowDriver = new InfiniteSteeringDriver(context, 30, graph, 0, 5);
        var fastDriver = new InfiniteSteeringDriver(context, 0, graph, 0, 10)
        {
            OvertakingActivated = true
        };

        var slowCarInFront = true;
        for (var i = 0; i < 1000; i++, context.UpdateStep())
        {
            fastDriver.Tick();
            slowDriver.Tick();
            if (slowCarInFront)
            {
                slowCarInFront = slowDriver.TotalDistanceDriven > fastDriver.TotalDistanceDriven;
            }
            else
            {
                // now the fast car stays forever ahead and reaches his max speed
                Assert.True(fastDriver.TotalDistanceDriven > slowDriver.TotalDistanceDriven);
                Assert.InRange(fastDriver.Velocity, fastDriver.MaxSpeed * 0.9, fastDriver.MaxSpeed);
                Assert.InRange(slowDriver.Velocity, slowDriver.MaxSpeed * 0.9, slowDriver.MaxSpeed);
            }
        }
    }

    [Fact]
    public void CarFollowsCarOvertakingDisabled()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var slowDriver = new InfiniteSteeringDriver(context, 30, graph, 0, 5);
        var fastDriver = new InfiniteSteeringDriver(context, 0, graph, 0, 10)
        {
            OvertakingActivated = false
        };
        fastDriver.Car.MaxSpeed = 10;
        var driver = new[] { slowDriver, fastDriver };

        const int ticks = 360;
        for (var i = 0; i < ticks; i++, context.UpdateStep())
            foreach (var infiniteDriver in driver)
                infiniteDriver.Tick();

        Assert.InRange(slowDriver.Velocity, slowDriver.MaxSpeed - 1, slowDriver.MaxSpeed);
        Assert.InRange(fastDriver.Velocity, slowDriver.MaxSpeed - 1, slowDriver.MaxSpeed);
        Assert.Equal(slowDriver.RoundsFinished, fastDriver.RoundsFinished);
    }

    [Fact]
    public void CarFollowsCarOvertakingActivated()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var slowDriver = new InfiniteSteeringDriver(context, 30, graph, 0, 5);
        var fastDriver = new InfiniteSteeringDriver(context, 0, graph, 0, 10)
        {
            OvertakingActivated = true
        };
        Assert.False(slowDriver.OvertakingActivated);
        Assert.True(fastDriver.OvertakingActivated);
        var driver = new[] { slowDriver, fastDriver };

        const int ticks = 360;
        for (var i = 0; i < ticks; i++, context.UpdateStep())
            foreach (var infiniteDriver in driver)
                infiniteDriver.Tick();

        Assert.InRange(slowDriver.Velocity, slowDriver.MaxSpeed - 1, slowDriver.MaxSpeed);
        Assert.InRange(fastDriver.Velocity, fastDriver.MaxSpeed - 1, fastDriver.MaxSpeed);
        Assert.InRange(slowDriver.RoundsFinished, 0, fastDriver.RoundsFinished - 1);
    }

    [Fact]
    public void CarOvertakingMultipleCarsOnDifferentLanes()
    {
        var graph = new SpatialGraphEnvironment(ResourcesConstants.RingNetwork);
        var context = SimulationContext.Start2020InSeconds;

        var driver = new List<InfiniteSteeringDriver>
        {
            new(context, 120, graph, 2, 5),
            new(context, 80, graph, 1, 5),
            new(context, 30, graph, 0, 5),
            new(context, 0, graph, 0, 15) { OvertakingActivated = true }

            // new InfiniteSteeringDriver(context, 120, graph, 5) {DesiredLane = 0},
            // new InfiniteSteeringDriver(context, 1, graph, 5) {DesiredLane = 1},
            // new InfiniteSteeringDriver(context, 3, graph, 5) {DesiredLane = 2},
            // new InfiniteSteeringDriver(context, 5, graph, 5) {DesiredLane = 0},
            // new InfiniteSteeringDriver(context, 7, graph, 5) {DesiredLane = 1},
            // new InfiniteSteeringDriver(context, 9, graph, 5) {DesiredLane = 2},
            // new InfiniteSteeringDriver(context, 11, graph, 5) {DesiredLane = 0},
            // new InfiniteSteeringDriver(context, 13, graph, 5) {DesiredLane = 1},
            // new InfiniteSteeringDriver(context, 15, graph, 5) {DesiredLane = 2},
            // new InfiniteSteeringDriver(context, 17, graph, 5) {DesiredLane = 0},
            // new InfiniteSteeringDriver(context, 19, graph, 5) {DesiredLane = 1},
            // new InfiniteSteeringDriver(context, 21, graph, 5) {DesiredLane = 2},
        };

        const int ticks = 300;
        for (var i = 0; i < ticks; i++, context.UpdateStep())
            foreach (var infiniteDriver in driver)
                infiniteDriver.Tick();
    }
}