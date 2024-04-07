using System;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Routing;
using SOHTests.Commons.Environment;
using Xunit;

namespace SOHTests.MultimodalModelTests.RoutingTests;

public class MultimodalRouteTests
{
    private static Route GetRandomRoute(ISpatialGraphEnvironment environment)
    {
        var start = environment.GetRandomNode();
        var goal = environment.GetRandomNode();

        var randomRoute = environment.FindRoute(start, goal);

        var counter = 0;
        while (goal.Equals(start) || randomRoute == null)
        {
            if (counter++ > 100)
                throw new ApplicationException("Could not find random route. Counter exceeded 100 tries.");
            start = environment.GetRandomNode();
            goal = environment.GetRandomNode();
            randomRoute = environment.FindRoute(start, goal);
        }

        return randomRoute;
    }

    [Fact]
    public void AppendAndDeleteTail()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphAltonaAltstadt);
        var multimodalRoute = new MultimodalRoute
        {
            { GetRandomRoute(environment), ModalChoice.CyclingRentalBike },
            { GetRandomRoute(environment), ModalChoice.Walking },
            { GetRandomRoute(environment), ModalChoice.CyclingRentalBike }
        };
        Assert.Equal(3, multimodalRoute.Count);

        Assert.Equal(ModalChoice.CyclingRentalBike, multimodalRoute.CurrentModalChoice);
        multimodalRoute.Next();

        Assert.Equal(2, multimodalRoute.Count);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);

        var append = new MultimodalRoute
        {
            { GetRandomRoute(environment), ModalChoice.CarDriving },
            { GetRandomRoute(environment), ModalChoice.Walking },
            { GetRandomRoute(environment), ModalChoice.CarDriving }
        };

        multimodalRoute.AppendAndDeleteTail(append);
        Assert.Equal(3, multimodalRoute.Count);
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);
    }

    [Fact]
    public void AppendAtEnd()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphAltonaAltstadt);
        var multimodalRoute = new MultimodalRoute
        {
            { GetRandomRoute(environment), ModalChoice.CyclingRentalBike }
        };
        Assert.Equal(1, multimodalRoute.Count);
        Assert.Equal(0, multimodalRoute.PassedStops);
        Assert.Equal(ModalChoice.CyclingRentalBike, multimodalRoute.CurrentModalChoice);
        Assert.False(multimodalRoute.CurrentRoute.GoalReached);

        multimodalRoute.Next();
        Assert.Equal(1, multimodalRoute.Count);
        Assert.Equal(0, multimodalRoute.PassedStops);
        Assert.Equal(ModalChoice.CyclingRentalBike, multimodalRoute.CurrentModalChoice);
        Assert.False(multimodalRoute.CurrentRoute.GoalReached);

        var append = new MultimodalRoute
        {
            { GetRandomRoute(environment), ModalChoice.CarDriving },
            { GetRandomRoute(environment), ModalChoice.Walking },
            { GetRandomRoute(environment), ModalChoice.CarDriving }
        };
        multimodalRoute.AppendAndDeleteTail(append);
        Assert.Equal(3, multimodalRoute.Count);
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);
    }

    [Fact]
    public void AppendToEmpty()
    {
        var environment = new SpatialGraphEnvironment(ResourcesConstants.DriveGraphAltonaAltstadt);
        var multimodalRoute = new MultimodalRoute();
        Assert.Equal(0, multimodalRoute.Count);

        var append = new MultimodalRoute
        {
            { GetRandomRoute(environment), ModalChoice.CarDriving },
            { GetRandomRoute(environment), ModalChoice.Walking },
            { GetRandomRoute(environment), ModalChoice.CarDriving }
        };
        multimodalRoute.AppendAndDeleteTail(append);
        Assert.Equal(3, multimodalRoute.Count);
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);
    }

    [Fact]
    public void EmptyRouteHasMaxTravelTime()
    {
        var multimodalRoute = new MultimodalRoute();
        Assert.Equal(int.MaxValue, multimodalRoute.ExpectedTravelTime());
    }

    [Fact]
    public void FindMainModalType()
    {
        var fourNodeGraphEnv = new FourNodeGraphEnv();
        var environment = fourNodeGraphEnv.GraphEnvironment;

        var multimodalRoute = new MultimodalRoute();
        Assert.Equal(ModalChoice.Walking, multimodalRoute.MainModalChoice);

        var route1 = environment.FindRoute(fourNodeGraphEnv.Node1, fourNodeGraphEnv.Node2);
        var route2 = environment.FindRoute(fourNodeGraphEnv.Node2, fourNodeGraphEnv.Node3);
        var route3 = environment.FindRoute(fourNodeGraphEnv.Node3, fourNodeGraphEnv.Node4);

        multimodalRoute = new MultimodalRoute { { route1, ModalChoice.Walking } };
        Assert.Equal(ModalChoice.Walking, multimodalRoute.MainModalChoice);

        multimodalRoute.Add(route2, ModalChoice.CarDriving);
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.MainModalChoice);

        multimodalRoute.Add(route3, ModalChoice.Walking);
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.MainModalChoice);

        multimodalRoute = new MultimodalRoute
        {
            { route1, ModalChoice.Walking }, { route2, ModalChoice.CyclingRentalBike }, { route2, ModalChoice.Walking }
        };
        Assert.Equal(ModalChoice.CyclingRentalBike, multimodalRoute.MainModalChoice);
    }

    [Fact]
    public void NewMultimodalRouteHasReasonableParameters()
    {
        var multimodalRoute = new MultimodalRoute();
        Assert.True(multimodalRoute.GoalReached);
        Assert.Empty(multimodalRoute.CurrentRoute);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);

        //nothing changes
        multimodalRoute.Next();
        Assert.True(multimodalRoute.GoalReached);
        Assert.Empty(multimodalRoute.CurrentRoute);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);
    }

    [Fact]
    public void SwitchThroughMultimodalRoute()
    {
        var fourNodeGraph = new FourNodeGraphEnv();
        var environment = fourNodeGraph.GraphEnvironment;
        var route1 = environment.FindRoute(fourNodeGraph.Node1, fourNodeGraph.Node2);
        var route2 = environment.FindRoute(fourNodeGraph.Node1, fourNodeGraph.Node3);
        var route3 = environment.FindRoute(fourNodeGraph.Node1, fourNodeGraph.Node4);

        var multimodalRoute = new MultimodalRoute
        {
            { route1, ModalChoice.Walking }, { route2, ModalChoice.CarDriving }, { route3, ModalChoice.CoDriving }
        };
        Assert.Equal(3, multimodalRoute.Count);
        Assert.False(multimodalRoute.GoalReached);

        Assert.Equal(route1, multimodalRoute.CurrentRoute);
        Assert.Equal(ModalChoice.Walking, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(route2, multimodalRoute.CurrentRoute);
        Assert.NotEqual(route1, multimodalRoute.CurrentRoute);
        Assert.Equal(ModalChoice.CarDriving, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(route3, multimodalRoute.CurrentRoute);
        Assert.NotEqual(route2, multimodalRoute.CurrentRoute);
        Assert.Equal(ModalChoice.CoDriving, multimodalRoute.CurrentModalChoice);

        multimodalRoute.Next();
        Assert.Equal(route3, multimodalRoute.CurrentRoute);
        Assert.Equal(ModalChoice.CoDriving, multimodalRoute.CurrentModalChoice);
    }
}