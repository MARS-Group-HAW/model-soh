using System.Collections.Generic;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using ServiceStack;
using SOHModel.Bus.Model;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.BigEventTests.PublicTransportTests;

public class BigEventBusLayerFixture : BusLayerFixture
{
    public BigEventBusLayerFixture() : base(ResourcesConstants.BigEventBusLinesCsv, ResourcesConstants.BigEventBusGraph) { }
}
public class BusTests : IClassFixture<BigEventBusLayerFixture>
{
    private readonly BusLayer _layer;
    private readonly BigEventBusLayerFixture _layerFixture;

    const string line380 = "380(Shuttle)";
    const string line180 = "180";

    public BusTests(BigEventBusLayerFixture layerFixture)
    {
        _layerFixture = layerFixture;
        _layer = new BusLayer
        {
            BusRouteLayer = _layerFixture.BusRouteLayer,
            Context = SimulationContext.Start2020InSeconds,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.BusCsv)),
            GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.BigEventBusGraph,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true,
                            GeometryAsNodesEnabled = true
                        }
                    }
                }
            })
        };
    }


    /// <summary>
    /// Test if the bus driver visits all stations along the bus route.
    /// </summary>
    /// <param name="line">The bus line identifier, e.g., "380(Shuttle)".</param>
    /// <param name="routeSections">The expected number of route sections for the given bus line.</param>
    [Theory]
    [InlineData(line380, 1)]
    [InlineData(line180, 3)]
    public void VisitAllStationsAlongBusRoute(string line, int routeSections)
    {
        var driver = GetDriverByLine(line);

        // Check if the bus route is available
        Assert.True(_layer.BusRouteLayer.TryGetRoute(driver.Line, out var schedule));
        Assert.NotNull(schedule);

        // Check if the bus route has the expected number of route sections
        Assert.Equal(routeSections, schedule.Count());
        var unvisitedRouteSections = schedule.Entries.ToList();
        var tick = 0;
        var goalReached = false; // driver.GoalReached is initially true, therefore we need to first use a separate variable
        while (!goalReached && tick++ < 9000)
        {
            _layer.Context.UpdateStep();
            driver.Tick();
            if (driver.Boarding)
            {
                var routeEntry = driver.BusRouteEnumerator.Current;
                unvisitedRouteSections.Remove(routeEntry);
            }
            goalReached = driver.GoalReached;
        }
        // Check if the bus driver visited all stations along the bus route
        Assert.Empty(unvisitedRouteSections);

    }

    private BusDriver GetDriverByLine(string line)
    {
        if (string.IsNullOrEmpty(line))
        {
            throw new System.ArgumentException("Please pass a valid line.", nameof(line));
        }
        return new BusDriver(_layer, (_, _) => { })
        {
            Line = line,
            MinimumBoardingTimeInSeconds = 10
        };
    }

}