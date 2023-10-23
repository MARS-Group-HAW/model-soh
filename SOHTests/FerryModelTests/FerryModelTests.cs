using System.Linq;
using Mars.Interfaces.Model;
using SOHFerryModel.Model;
using Xunit;

namespace SOHTests.FerryModelTests;

public class FerryModelTests
{
    [Fact]
    public void TestGetOutputPropertiesForFerry()
    {
        var type = new EntityType(typeof(Ferry))
        {
            Mapping = new EntityMapping { OutputKind = OutputKind.FullWithIgnored }
        };

        var outputProperties = type.OutputProperties.Select(propertyType => propertyType.Name).ToArray();

        Assert.Contains("Length", outputProperties);
        Assert.Contains("MaxSpeed", outputProperties);
        Assert.Contains("Velocity", outputProperties);
    }
}