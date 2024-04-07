using System;
using System.Data;
using System.Linq;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Model;
using SOHModel.Ferry.Model;
using SOHModel.Ferry.Route;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.FerryModelTests;

public class FerryRouteReaderTests : IClassFixture<FerryRouteLayerFixture>
{
    private readonly FerryRouteLayerFixture _fixture;

    public FerryRouteReaderTests(FerryRouteLayerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ImportFerryLineCsvIntoFerrySchedule()
    {
        var ferryLayer = new FerryRouteLayerFixture().FerryStationLayer;
        var routes = FerryRouteReader.Read(ResourcesConstants.FerryLineCsv, ferryLayer);

        Assert.Contains(61, routes.Keys);
        var ferrySchedule61 = routes[61];
        Assert.Equal(8, ferrySchedule61.Entries.Count);
        Assert.NotNull(ferrySchedule61.Entries.First().From);

        Assert.Contains(62, routes.Keys);
        var ferrySchedule62 = routes[62];
        Assert.Equal(10, ferrySchedule62.Entries.Count);
        Assert.NotNull(ferrySchedule62.Entries.First().From);
    }

    [Fact]
    public void TestDateTimeFormatParsing()
    {
        var time = "7:00".Value<DateTime>();

        Assert.Equal(7, time.Hour);
        Assert.Equal(0, time.Minute);
        Assert.Equal(DateTime.Now.Year, time.Year);
    }

    [Fact]
    public void TestCreateDriverBasedOnInput()
    {
        var manager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.FerryCsv));
        var ferryLayer = new FerryLayer(_fixture.FerryRouteLayer) { EntityManager = manager };
        var ferryScheduler = new FerrySchedulerLayer(ferryLayer);

        ferryScheduler.InitLayer(new LayerInitData
            {
                LayerInitConfig = new LayerMapping { File = ResourcesConstants.TestFerryDriverCsv }
            }, (_, _) => { },
            (_, _) => { });

        ferryScheduler.Context = new SimulationContext(TimeSpan.FromSeconds(1), DateTime.Today);

        Assert.NotNull(ferryScheduler.SchedulingTable);
        Assert.NotEmpty(ferryScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(ferryScheduler.AllDayTimeSeries);
        Assert.NotEmpty(ferryScheduler.AllDayTimeSeries);
        Assert.Empty(ferryScheduler.TimeSeries);

        Assert.NotNull(SimulationContext.Start2020InSeconds.CurrentTimePoint);
        var res = ferryScheduler.AllDayTimeSeries.Query(
                DateTime.MinValue.AddHours(20))
            .ToList();
        Assert.Equal(2, res.Count);


        for (var i = 0; i < 50400; i++)
        {
            ferryScheduler.PreTick();
            ferryScheduler.Context.UpdateStep();
        }

        Assert.NotNull(ferryLayer.Driver);
        Assert.NotEmpty(ferryLayer.Driver);
        Assert.All(ferryLayer.Driver, pair => { Assert.NotNull(pair.Value.Ferry); });

        Assert.Equal(1, ferryLayer.Driver.Count(pair => pair.Value.Line == 50));
        Assert.Equal(9, ferryLayer.Driver.Count(pair => pair.Value.Line == 62));
        Assert.Equal(30, ferryLayer.Driver.Count(pair => pair.Value.Line == 60));

        Assert.Empty(ferryLayer.Driver.Where(pair => pair.Value.Line == 71));
    }

    [Fact]
    public void TestCreateDriverBasedOnInputWithType()
    {
        var ferryTable = new DataTable();

        ferryTable.Columns.Add("passengerCapacity");
        ferryTable.Columns.Add("type");
        ferryTable.LoadDataRow(new object[] { 21, "Type1" }, LoadOption.Upsert);
        ferryTable.LoadDataRow(new object[] { 22, "Type2" }, LoadOption.Upsert);

        var manager = new EntityManagerImpl(ferryTable);

        var table = new DataTable();

        table.Columns.Add("id");
        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("line");
        table.Columns.Add("ferryType");

        table.LoadDataRow(new object[] { 1, "7:00", "18:00", 30, 1, 10, "Type1" }, LoadOption.Upsert);
        table.LoadDataRow(new object[] { 2, "5:00", "14:00", 60, 2, 11, "Type2" }, LoadOption.Upsert);


        var ferryLayer = new FerryLayer(_fixture.FerryRouteLayer) { EntityManager = manager };
        var ferryScheduler = new FerrySchedulerLayer(ferryLayer, table);


        ferryScheduler.InitLayer(new LayerInitData(), (_, _) => { },
            (_, _) => { });
        ferryScheduler.Context = new SimulationContext(TimeSpan.FromSeconds(1), DateTime.Today);

        Assert.NotNull(ferryScheduler.SchedulingTable);
        Assert.NotEmpty(ferryScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(ferryScheduler.AllDayTimeSeries);
        Assert.NotEmpty(ferryScheduler.AllDayTimeSeries);

        Assert.NotNull(SimulationContext.Start2020InSeconds.CurrentTimePoint);
        var res = ferryScheduler.AllDayTimeSeries.Query(
                DateTime.MinValue.AddHours(8))
            .ToList();
        Assert.Equal(2, res.Count);


        for (var i = 0; i < 50400; i++)
        {
            ferryScheduler.PreTick();
            ferryScheduler.Context.UpdateStep();
        }

        Assert.NotNull(ferryLayer.Driver);
        Assert.NotEmpty(ferryLayer.Driver);

        Assert.Equal(14,
            ferryLayer.Driver.Count(pair => pair.Value.Line == 10 && pair.Value.Ferry.PassengerCapacity == 21));
        Assert.Equal(18,
            ferryLayer.Driver.Count(pair => pair.Value.Line == 11 && pair.Value.Ferry.PassengerCapacity == 22));
    }

    [Fact]
    public void TestExceptionForMissingReference()
    {
        var ferryTable = new DataTable();

        ferryTable.Columns.Add("capacity");
        ferryTable.Columns.Add("type");
        ferryTable.LoadDataRow(new object[] { 1, "Type1" }, LoadOption.Upsert);
        ferryTable.LoadDataRow(new object[] { 2, "Type2" }, LoadOption.Upsert);

        var manager = new EntityManagerImpl(ferryTable);

        var table = new DataTable();

        table.Columns.Add("id");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("line");
        table.Columns.Add("ferryType");

        table.LoadDataRow(new object[] { 3, "20:00", 0, 1, 12, "Type45" }, LoadOption.Upsert);


        var ferryLayer = new FerryLayer(_fixture.FerryRouteLayer) { EntityManager = manager };
        var ferryScheduler = new FerrySchedulerLayer(ferryLayer, table);

        ferryScheduler.InitLayer(new LayerInitData(), (_, _) => { },
            (_, _) => { });

        ferryScheduler.Context = SimulationContext.Start2020InSeconds;

        Assert.Throws<ArgumentException>(() => ferryScheduler.SchedulingTable);
    }

    [Fact]
    public void TestInitializeFerryScheduler()
    {
        var ferryScheduler = new FerrySchedulerLayer(new FerryLayer(_fixture.FerryRouteLayer));


        ferryScheduler.InitLayer(new LayerInitData
            {
                LayerInitConfig = new LayerMapping { File = ResourcesConstants.TestFerryDriverCsv }
            }, (_, _) => { },
            (_, _) => { });

        Assert.NotNull(ferryScheduler.SchedulingTable);
        Assert.NotEmpty(ferryScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(ferryScheduler.AllDayTimeSeries);
        Assert.NotEmpty(ferryScheduler.AllDayTimeSeries);

        var dataRows = ferryScheduler.AllDayTimeSeries.Query(
            DateTime.MinValue.AddHours(20)).ToList();

        Assert.Equal(2, dataRows.Count);
        Assert.Equal(71, dataRows[1].Data["line"].Value<int>());
        Assert.Equal(60, dataRows[0].Data["line"].Value<int>());
    }
}