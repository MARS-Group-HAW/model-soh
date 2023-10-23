using Mars.Interfaces.Environments;
using SOHMultimodalModel.Layers.TrafficLight;
using Xunit;

namespace SOHTests.MultimodalModelTests.TrafficLightTests;

public class TrafficLightTests
{
    [Fact]
    public void TrafficLightCtorTest()
    {
        var trafficLight = new TrafficLight(TrafficLightPhase.Red, 1, 2, 3);

        Assert.Equal(TrafficLightPhase.Red, trafficLight.TrafficLightPhase);
        Assert.Equal(1, trafficLight.StartGreenTick);
        Assert.Equal(2, trafficLight.StartYellowTick);
        Assert.Equal(3, trafficLight.StartRedTick);

        trafficLight = new TrafficLight(TrafficLightPhase.Yellow, 1, 2, 3);
        Assert.Equal(TrafficLightPhase.Yellow, trafficLight.TrafficLightPhase);
        trafficLight = new TrafficLight(TrafficLightPhase.Green, 1, 2, 3);
        Assert.Equal(TrafficLightPhase.Green, trafficLight.TrafficLightPhase);
        trafficLight = new TrafficLight(TrafficLightPhase.None, 1, 2, 3);

        Assert.Equal(TrafficLightPhase.None, trafficLight.TrafficLightPhase);
    }
}