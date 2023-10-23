using System.Linq;
using SOHTests.Commons.Layer;
using SOHTrainModel.Route;
using SOHTrainModel.Station;
using Xunit;

namespace SOHTests.TrainModelTests;

public class TrainRouteTests : IClassFixture<TrainStationLayerFixture>
{
    private readonly TrainStationLayer _trainStationLayer;

    public TrainRouteTests(TrainStationLayerFixture trainStationLayerFixture)
    {
        _trainStationLayer = trainStationLayerFixture.TrainStationLayer;
    }

    [Fact]
    public void ReadTrainRouteTest()
    {
        var routes = TrainRouteReader.Read(ResourcesConstants.TrainU1LineCsv, _trainStationLayer);
        Assert.Single(routes);

        var (line, route) = routes.First();
        Assert.Equal("U1", line);
        Assert.Equal(37, route.Count());

        Assert.Equal("Ochsenzoll", route.Entries.First().From.Name);
        Assert.Equal("Kiwittsmoor", route.Entries.First().To.Name);

        Assert.Equal("Hoisbüttel", route.Entries.Last().From.Name);
        Assert.Equal("Ohlstedt", route.Entries.Last().To.Name);
    }

    [Fact]
    public void ReverseTrainRouteTest()
    {
        var route = new TrainRoute();
        var station1 = new TrainStation();
        var station2 = new TrainStation();
        var station3 = new TrainStation();
        route.Entries.Add(new TrainRouteEntry(station1, station2, 5));
        route.Entries.Add(new TrainRouteEntry(station2, station3, 5));

        Assert.Equal(station1, route.Entries.First().From);
        Assert.Equal(station2, route.Entries.First().To);
        Assert.Equal(station2, route.Entries.Last().From);
        Assert.Equal(station3, route.Entries.Last().To);

        var reversed = route.Reversed();
        Assert.Equal(station3, reversed.Entries.First().From);
        Assert.Equal(station2, reversed.Entries.First().To);
        Assert.Equal(station2, reversed.Entries.Last().From);
        Assert.Equal(station1, reversed.Entries.Last().To);
    }

    [Fact]
    public void ReverseReadTrainRouteTest()
    {
        var routes = TrainRouteReader.Read(ResourcesConstants.TrainU1LineCsv, _trainStationLayer);
        Assert.Single(routes);

        var (line, route) = routes.First();
        var reversed = route.Reversed();
        Assert.Equal("U1", line);
        Assert.Equal(37, reversed.Count());

        Assert.Equal("Ohlstedt", reversed.Entries.First().From.Name);
        Assert.Equal("Hoisbüttel", reversed.Entries.First().To.Name);

        Assert.Equal("Kiwittsmoor", reversed.Entries.Last().From.Name);
        Assert.Equal("Ochsenzoll", reversed.Entries.Last().To.Name);
    }
}