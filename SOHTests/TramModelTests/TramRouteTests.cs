using System.Linq;
using SOHModel.Tram.Route;
using SOHModel.Tram.Station;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.TramModelTests;

public class TramRouteTests: IClassFixture<TramStationLayerFixture>
{
    private readonly TramStationLayer _tramStationLayer;

    public TramRouteTests(TramStationLayerFixture tramStationLayerFixture)
    {
        _tramStationLayer = tramStationLayerFixture.TramStationLayer;
    }

    [Fact]
    public void ReadTramRouteTest()
    {
        var routes = TramRouteReader.Read(ResourcesConstants.TramT1LineCsv, _tramStationLayer);
        Assert.Single(routes);

        var (line, route) = routes.First();
        Assert.Equal("T1", line);
        Assert.Equal(6, route.Count());

        Assert.Equal("Mekka", route.Entries.First().From.Name);
        Assert.Equal("Loasis", route.Entries.First().To.Name);

        Assert.Equal("CasaSud", route.Entries.Last().From.Name);
        Assert.Equal("Facultes", route.Entries.Last().To.Name);
    }

    [Fact]
    public void ReverseTramRouteTest()
    {
        var route = new TramRoute();
        var station1 = new TramStation();
        var station2 = new TramStation();
        var station3 = new TramStation();
        route.Entries.Add(new TramRouteEntry(station1, station2, 5));
        route.Entries.Add(new TramRouteEntry(station2, station3, 5));

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
        var routes = TramRouteReader.Read(ResourcesConstants.TramT1LineCsv, _tramStationLayer);
        Assert.Single(routes);

        var (line, route) = routes.First();
        var reversed = route.Reversed();
        Assert.Equal("T1", line);
        Assert.Equal(6, reversed.Count());

        Assert.Equal("Facultes", reversed.Entries.First().From.Name);
        Assert.Equal("CasaSud", reversed.Entries.First().To.Name);

        Assert.Equal("Loasis", reversed.Entries.Last().From.Name);
        Assert.Equal("Mekka", reversed.Entries.Last().To.Name);
    }
}