using Mars.Common.IO.Csv;
using Mars.Core.Data;
using SOHModel.Car.Model;
using Xunit;

namespace SOHTests.CarModelTests;

public class CarEntityTests
{
    [Fact]
    public void ReadEntityCsv()
    {
        var data = CsvReader.MapData(ResourcesConstants.CarCsv);
        var dataRow = data.Rows[0];
        var index = data.Columns.IndexOf("maxSpeed");

        Assert.Equal("13.89", dataRow.ItemArray[index]);
    }

    [Fact]
    public void ReadEntityCsvByEntityManager()
    {
        var data = CsvReader.MapData(ResourcesConstants.CarCsv);

        var entityManagerImpl = new EntityManagerImpl(data);
        var car = entityManagerImpl.Create<Car>("type", "Golf");

        Assert.Equal(13.89, car.MaxSpeed);
    }
}