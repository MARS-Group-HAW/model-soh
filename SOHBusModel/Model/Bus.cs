using Mars.Interfaces.Environments;
using SOHBusModel.Station;
using SOHBusModel.Steering;
using SOHDomain.Model;
using SOHDomain.Steering.Capables;

namespace SOHBusModel.Model;

public class Bus : Vehicle<IBusSteeringCapable, IPassengerCapable, BusSteeringHandle, BusPassengerHandle>
{
    public Bus()
    {
        IsCollidingEntity = true;
        ModalityType = SpatialModalityType.CarDriving;
    }

    public BusLayer Layer { get; set; }

    /// <summary>
    ///     Where the <see cref="Bus" /> is located right now. Null if train is not at any station right now.
    /// </summary>
    public BusStation BusStation { get; set; }

    protected override BusPassengerHandle CreatePassengerHandle()
    {
        return new BusPassengerHandle(this);
    }

    protected override BusSteeringHandle CreateSteeringHandle(IBusSteeringCapable driver)
    {
        return new BusSteeringHandle(Layer.GraphEnvironment, driver, this);
    }
}