using System;
using System.Data;
using System.Linq;
using Mars.Common.Core;
using Mars.Interfaces;
using Mars.Interfaces.Data;
using Mars.Interfaces.Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SOHMultimodalModel.Model;
using Xunit;

namespace SOHTests.FerryModelTests;

public class TravelerScheduleTests
{
    [Fact]
    public void TestImportDockWorkerComplex()
    {
        var travelLayer = new DockWorkerLayer();
        var layer = new DockWorkerSchedulerLayer(travelLayer);

        layer.InitLayer(new LayerInitData
            {
                LayerInitConfig = new LayerMapping
                {
                    File = ResourcesConstants.DockWorkerComplexCsv
                }
            }, (_, _) => { },
            (_, _) => { });

        Assert.NotNull(layer.TimeSeries);
        Assert.NotEmpty(layer.TimeSeries);
        Assert.NotNull(layer.AllDayTimeSeries);
        Assert.Empty(layer.AllDayTimeSeries);
        Assert.NotNull(layer.SchedulingTable);
        Assert.Equal(270, layer.SchedulingTable.Rows.Count);
        Assert.Equal(270, layer.TimeSeries.Count);

        var elements = layer.TimeSeries.Query("19-09-2020T13:00".Value<DateTime>());

        Assert.Contains(elements, entry => entry.SchedulingEventAmounts == 5);

        var context = SimulationContext.Start2020InSeconds;
        context.CurrentTimePoint = new DateTime(2020, 09, 01);
        layer.Context = context;
        travelLayer.Context = layer.Context;
        for (var i = 0; i < 50400; i++)
        {
            layer.PreTick();
            layer.Context.UpdateStep();
        }

        Assert.Equal(604, travelLayer.Agents.Count);
        Assert.NotEmpty(travelLayer.Agents);
    }

    [Fact]
    public void TestInitializeTravelScheduler()
    {
        var travelLayer = new DockWorkerLayer();
        var layer = new DockWorkerSchedulerLayer(travelLayer);

        layer.InitLayer(new LayerInitData
            {
                LayerInitConfig = new LayerMapping
                {
                    File = ResourcesConstants.DockWorkerCsv
                }
            }, (_, _) => { },
            (_, _) => { });

        Assert.NotNull(layer.SchedulingTable);
        Assert.NotNull(layer.TimeSeries);
        Assert.Empty(layer.TimeSeries);
        Assert.NotNull(layer.AllDayTimeSeries);
        Assert.NotEmpty(layer.AllDayTimeSeries);
        Assert.Equal(5, layer.AllDayTimeSeries.Count);
        Assert.Equal(5, layer.SchedulingTable.Rows.Count);

        var results = layer.AllDayTimeSeries.Query(DateTime.MinValue.AddHours(8)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal(53.54836, results[0].Data["sourceY"].Value<double>());

        Assert.Empty(layer.AllDayTimeSeries.Query(DateTime.MinValue));
    }

    [Fact]
    public void TestParseDateTime()
    {
        const string dateTimeString = "01-09-2020T07:00";
        const string timeString = "07:00:10";
        const string dateTimeString2 = "01-10-2020T07:00";
        const string dateTimeString3 = "2020-01-01T00:00:00";

        var dateTime = new DateTime(2020, 09, 01, 7, 0, 0);
        var dateTime2 = new DateTime(2020, 10, 01, 7, 0, 0);
        var dateTime3 = new DateTime(2020, 01, 01, 0, 0, 0);
        var time = DateTime.Today.AddHours(7).AddSeconds(10);

        Assert.Equal(dateTime, dateTimeString.Value<DateTime>());
        Assert.Equal(dateTime2, dateTimeString2.Value<DateTime>());
        Assert.Equal(dateTime3, dateTimeString3.Value<DateTime>());
        Assert.Equal(time, timeString.Value<DateTime>());
    }

    [Fact]
    public void TestCreateDriverBasedOnInput()
    {
        var travelLayer = new DockWorkerLayer();
        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer);


        travelScheduler.InitLayer(new LayerInitData
            {
                LayerInitConfig = new LayerMapping
                {
                    File = ResourcesConstants.DockWorkerCsv
                }
            }, (_, _) => { },
            (_, _) => { });

        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.NotNull(travelScheduler.SchedulingTable);
        Assert.NotEmpty(travelScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(travelScheduler.AllDayTimeSeries);
        Assert.NotEmpty(travelScheduler.AllDayTimeSeries);

        Assert.NotNull(SimulationContext.Start2020InSeconds.CurrentTimePoint);
        var res = travelScheduler.AllDayTimeSeries.Query(
                DateTime.MinValue.AddHours(6))
            .ToList();
        Assert.Equal(2, res.Count);


        for (var i = 0; i < 50400; i++)
        {
            travelScheduler.PreTick();
            travelScheduler.Context.UpdateStep();
        }

        Assert.NotNull(travelLayer.Agents);
        Assert.NotEmpty(travelLayer.Agents);

        Assert.Equal(14, travelLayer.Agents.Count(pair =>
        {
            var position = pair.Value.Position;
            return Math.Abs(position.X - 9.95253) < 0.0000001 && Math.Abs(position.Y - 53.54907) < 0.00000001;
        }));

        Assert.Equal(18, travelLayer.Agents.Count(pair =>
        {
            var position = pair.Value.Position;
            return Math.Abs(position.X - 9.91582) < 0.0000001 && Math.Abs(position.Y - 53.54836) < 0.00000001;
        }));

        Assert.Empty(travelLayer.Agents.Where(pair =>
        {
            var position = pair.Value.Position;
            return Math.Abs(position.X - 9.97033) < 0.0000001 && Math.Abs(position.Y - 9.97033) < 0.00000001;
        }));
    }

    [Fact]
    public void TestExceptionWhenMissingAllDayAndConcreteTime()
    {
        var travelLayer = new DockWorkerLayer();

        var table = new DataTable();

        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("source");
        table.Columns.Add("destination");

        table.LoadDataRow(
            new object[] { "7:00", "01-09-2020 17:00", 30, 1, "Point(9.95253 53.54907)", "Point(9.92812 53.52143)" },
            LoadOption.Upsert);

        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer, table);
        travelScheduler.InitLayer(new LayerInitData(),
            (_, _) => { },
            (_, _) => { });

        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.Throws<ArgumentException>(() => travelScheduler.SchedulingTable);
    }

    [Fact]
    public void TestMixingAllDayTimeSeriesWithConcreteTime()
    {
        var travelLayer = new DockWorkerLayer();

        var table = new DataTable();

        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("source");
        table.Columns.Add("destination");

        table.LoadDataRow(
            new object[] { "7:00", "18:00", 30, 1, "Point(9.95253 53.54907)", "Point(9.92812 53.52143)" },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[]
            {
                "01-09-2020T17:00", "01-10-2020T07:00", 60, 2, "Point(9.91582 53.54836)",
                "Point(9.92797 53.5067499)"
            },
            LoadOption.Upsert);

        var dateTime = "01-09-2020T17:00".Value<DateTime>();
        Assert.Equal(new DateTime(2020, 09, 01, 17, 00, 00), dateTime);

        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer, table);
        travelScheduler.InitLayer(new LayerInitData(),
            (_, _) => { },
            (_, _) => { });

        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.NotNull(travelScheduler.SchedulingTable);
        Assert.NotEmpty(travelScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(travelScheduler.AllDayTimeSeries);
        Assert.NotEmpty(travelScheduler.AllDayTimeSeries);
        Assert.NotNull(travelScheduler.TimeSeries);
        Assert.NotEmpty(travelScheduler.TimeSeries);
        Assert.Single(travelScheduler.TimeSeries);
        Assert.Single(travelScheduler.AllDayTimeSeries);

        Assert.Single(travelScheduler.AllDayTimeSeries.Query(DateTime.MinValue.AddHours(10)));
        Assert.Single(travelScheduler.TimeSeries.Query(new DateTime(2020, 09, 28)));
    }

    [Fact]
    public void TestCreateDriverBasedOnGeometryInput()
    {
        var travelLayer = new DockWorkerLayer();

        var table = new DataTable();

        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("source");
        table.Columns.Add("destination");
        table.Columns.Add("gender");
        table.Columns.Add("mass");
        table.Columns.Add("perceptionInMeter");

        table.LoadDataRow(
            new object[]
                { "7:00", "18:00", 30, 1, "Point(9.95253 53.54907)", "Point(9.92812 53.52143)", "female", 100, 1.1 },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[] { "5:00", "14:00", 60, 2, "Point(9.91582 53.54836)", "Point(9.92797 53.5067499)" },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[] { "16:00", "23:00", 60, 2, "Point(9.97033 53.54898)", "Point(9.949668 53.531397)" },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[]
            {
                "9:00", "20:00", 10, 2, "Point(9.98911 53.54531)",
                "LineString(9.891536 53.534310, 9.97969 53.54480)"
            },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[] { "6:00", "9:00", 10, 4, "Point(9.87707 53.53461)", "Point(9.97969 53.54480)" },
            LoadOption.Upsert);

        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer, table);


        travelScheduler.InitLayer(new LayerInitData(),
            (_, _) => { },
            (_, _) => { });


        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.NotNull(travelScheduler.SchedulingTable);
        Assert.NotEmpty(travelScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(travelScheduler.AllDayTimeSeries);
        Assert.NotEmpty(travelScheduler.AllDayTimeSeries);
        Assert.NotNull(travelScheduler.TimeSeries);
        Assert.Empty(travelScheduler.TimeSeries);

        Assert.NotNull(SimulationContext.Start2020InSeconds.CurrentTimePoint);
        var res = travelScheduler.AllDayTimeSeries.Query(
                DateTime.MinValue.AddHours(6))
            .ToList();
        Assert.Equal(2, res.Count);


        for (var i = 0; i < 50400; i++)
        {
            travelScheduler.PreTick();
            travelScheduler.Context.UpdateStep();
        }

        Assert.NotNull(travelLayer.Agents);
        Assert.NotEmpty(travelLayer.Agents);

        Assert.Equal(14, travelLayer.Agents.Count(pair =>
        {
            var position = pair.Value.Position;

            var condition = pair.Value.Gender == GenderType.Female
                            && Math.Abs(pair.Value.PerceptionInMeter - 1.1) < 0.0000001
                            && Math.Abs(pair.Value.Mass - 100) < 0.00000001;

            return condition && Math.Abs(position.X - 9.95253) < 0.0000001 &&
                   Math.Abs(position.Y - 53.54907) < 0.00000001;
        }));

        Assert.Equal(18, travelLayer.Agents.Count(pair =>
        {
            var position = pair.Value.Position;
            return Math.Abs(position.X - 9.91582) < 0.0000001 && Math.Abs(position.Y - 53.54836) < 0.00000001;
        }));

        Assert.Empty(travelLayer.Agents.Where(pair =>
        {
            var position = pair.Value.Position;
            return Math.Abs(position.X - 9.97033) < 0.0000001 && Math.Abs(position.Y - 9.97033) < 0.00000001;
        }));
    }

    [Fact]
    public void TestNoAgentForNegativeAgentAmount()
    {
        var travelLayer = new DockWorkerLayer();

        var table = new DataTable();

        table.Columns.Add("id");
        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("source");
        table.Columns.Add("destination");

        table.LoadDataRow(
            new object[] { 1, "7:00", "12:00", 5, -1, "Point(9.95253 53.54907)", "Point(9.92812 53.52143)" },
            LoadOption.Upsert);

        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer, table);
        travelScheduler.InitLayer(new LayerInitData(),
            (_, _) => { },
            (_, _) => { });

        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.NotNull(travelScheduler.SchedulingTable);
        Assert.NotEmpty(travelScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(travelScheduler.TimeSeries);
        Assert.Empty(travelScheduler.TimeSeries);
        Assert.NotNull(travelScheduler.AllDayTimeSeries);
        Assert.NotEmpty(travelScheduler.AllDayTimeSeries);

        for (var i = 0; i < 50400; i++)
        {
            travelScheduler.PreTick();
            travelScheduler.Context.UpdateStep();
        }

        Assert.NotNull(travelLayer.Agents);
        Assert.Empty(travelLayer.Agents);
    }

    [Fact]
    public void TestCreateOnlyOneAgentForNegativeSpawningInterval()
    {
        var travelLayer = new DockWorkerLayer();

        var table = new DataTable();

        table.Columns.Add("id");
        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("source");
        table.Columns.Add("destination");


        table.LoadDataRow(
            new object[] { 1, "7:00", "12:00", -30, 1, "Point(9.95253 53.54907)", "Point(9.92812 53.52143)" },
            LoadOption.Upsert);

        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer, table);
        travelScheduler.InitLayer(new LayerInitData(), (_, _) => { });


        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.NotNull(travelScheduler.SchedulingTable);
        Assert.NotEmpty(travelScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(travelScheduler.AllDayTimeSeries);
        Assert.NotEmpty(travelScheduler.AllDayTimeSeries);

        for (var i = 0; i < 50400; i++)
        {
            travelScheduler.PreTick();
            travelScheduler.Context.UpdateStep();
        }

        Assert.NotNull(travelLayer.Agents);
        Assert.Single(travelLayer.Agents);
    }

    [Fact]
    public void TestTravelScheduleBasedOnGeometryPolygon()
    {
        const string lineString =
            "MULTILINESTRING ((9.95043430749314 53.5447180901045,9.95303605992674 53.5457759454896," +
            "9.95678143980368 53.5461476244087,9.96038386625021 53.5466908474443,9.96527287357049 53.5466908474443," +
            "9.96693113336334 53.5464049405835,9.96693113336334 53.5464049405835))";
        const string sourcePolygon =
            "MULTIPOLYGON (((9.97859976984082 53.543765731511,9.98209851718183 53.5428480272904,9.98651496874344 53.5427906707766,9.98565462103663 53.5456584964659,9.98261472580592 53.5477233309623,9.97791149167537 53.54617470509,9.97538780506874 53.546461487659,9.97859976984082 53.543765731511)))";
        const string targetPolygon =
            "MULTIPOLYGON (((9.9038642323762 53.4969628162607,9.91063230100308 53.4898506085511," +
            "9.92686419440484 53.4863518612101,9.92760982908407 53.502813180667,9.92072704742962 53.5180700133344," +
            "9.90220089347638 53.5195612826928,9.89617845952873 53.5129652836073,9.89331063383937 53.498224659564," +
            "9.9038642323762 53.4969628162607)))";

        var travelLayer = new DockWorkerLayer();

        var table = new DataTable();

        table.Columns.Add("id");
        table.Columns.Add("startTime");
        table.Columns.Add("endTime");
        table.Columns.Add("spawningIntervalInMinutes");
        table.Columns.Add("spawningAmount");
        table.Columns.Add("source");
        table.Columns.Add("destination");

        table.LoadDataRow(
            new object[] { 1, "7:00", "18:00", 30, 1, $"{lineString}", "Point(9.958469 53.517680)" },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[] { 2, "5:00", "14:00", 60, 2, $"{sourcePolygon}", $"{targetPolygon}" },
            LoadOption.Upsert);
        table.LoadDataRow(
            new object[] { 3, "9:00", "20:00", 0, 1, "Point(9.98911 53.54531)", "Point(9.958469 53.517680)" },
            LoadOption.Upsert);

        var travelScheduler = new DockWorkerSchedulerLayer(travelLayer, table);
        travelScheduler.InitLayer(new LayerInitData(), (_, _) => { }, (_, _) => { });

        travelScheduler.Context = SimulationContext.Start2020InSeconds;
        travelLayer.Context = travelScheduler.Context;
        Assert.NotNull(travelScheduler.SchedulingTable);
        Assert.NotEmpty(travelScheduler.SchedulingTable.AsEnumerable());
        Assert.NotNull(travelScheduler.TimeSeries);
        Assert.Empty(travelScheduler.TimeSeries);
        Assert.NotNull(travelScheduler.AllDayTimeSeries);
        Assert.NotEmpty(travelScheduler.AllDayTimeSeries);

        Assert.NotNull(SimulationContext.Start2020InSeconds.CurrentTimePoint);
        var res = travelScheduler.AllDayTimeSeries.Query(
                DateTime.MinValue.AddHours(6))
            .ToList();

        Assert.Single(res);


        for (var i = 0; i < 50400; i++)
        {
            travelScheduler.PreTick();
            travelScheduler.Context.UpdateStep();
        }

        Assert.NotNull(travelLayer.Agents);
        Assert.NotEmpty(travelLayer.Agents);

        Assert.All(travelScheduler.TimeSeries.Values, entry =>
        {
            Assert.Contains("id", entry.Data.Keys);
            Assert.NotNull(entry.SourceGeometry);
            Assert.NotNull(entry.TargetGeometry);
            Assert.NotEqual(DateTime.MinValue, entry.EndValidTime);
            Assert.NotEqual(DateTime.MinValue, entry.StartValidTime);

            if (entry.Data["id"].Value<int>() == 3)
                Assert.Equal(TimeSpan.Zero, entry.SchedulingInterval);
            else
                Assert.True(entry.SchedulingInterval.Minutes > 0);
        });

        Assert.Single(travelLayer.Agents.Values.Where(t => t.TravelScheduleId == 3));
        var polygonTraveler = travelLayer.Agents.Values.Where(t => t.TravelScheduleId == 2);

        // Check that the randomly selected source and target are in their respective polygon description as well. 
        var source = new WKTReader().Read(sourcePolygon);
        var target = new WKTReader().Read(targetPolygon);

        Assert.All(polygonTraveler, traveler =>
        {
            Assert.True(source.Intersects(new Point(traveler.Position.X, traveler.Position.Y)));
            Assert.True(target.Intersects(new Point(traveler.GoalPosition.X, traveler.GoalPosition.Y)));
        });
    }
}