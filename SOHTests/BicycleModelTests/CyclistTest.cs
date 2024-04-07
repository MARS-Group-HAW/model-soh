using System;
using Mars.Common.IO.Csv;
using Mars.Core.Data;
using SOHModel.Bicycle.Common;
using SOHModel.Bicycle.Model;
using Xunit;

namespace SOHTests.BicycleModelTests;

public class CyclistTest
{
    [Fact]
    public void TestCreateBicycleByEntityManager()
    {
        var dataTable = CsvReader.MapData(ResourcesConstants.BicycleCsv);
        var manager = new EntityManagerImpl(dataTable);

        var bicycle = manager.Create<Bicycle>("type", "city");

        Assert.NotNull(bicycle);
        Assert.Equal(BicycleType.City, bicycle.Type);
        Assert.Equal(0.60, bicycle.Width);
        Assert.NotEqual(0, bicycle.Weight);
        Assert.Equal(75, bicycle.DriverMass);
        Assert.Equal(3.0, bicycle.MaxAcceleration);
        Assert.Equal(3.0, bicycle.MaxDeceleration);
    }

    [Fact]
    public void TestEntityManagerException()
    {
        var dataTable = CsvReader.MapData(ResourcesConstants.BicycleCsv);
        var manager = new EntityManagerImpl(dataTable);

        Assert.Throws<ArgumentException>(() =>
            manager.Create<Bicycle>("bicycleType", "city"));
    }
}