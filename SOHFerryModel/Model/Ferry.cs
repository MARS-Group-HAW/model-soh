using Mars.Interfaces.Environments;
using SOHDomain.Model;
using SOHDomain.Steering.Capables;
using SOHFerryModel.Station;
using SOHFerryModel.Steering;

namespace SOHFerryModel.Model;

public class Ferry : Vehicle<IFerrySteeringCapable, IPassengerCapable, FerrySteeringHandle, FerryPassengerHandle>
{
    public Ferry()
    {
        IsCollidingEntity = false;
        ModalityType = SpatialModalityType.ShipDriving;
    }

    public FerryLayer Layer { get; set; }

    /// <summary>
    ///     Where the <see cref="Ferry" /> is located right now. Null if ferry is not at any station right now.
    /// </summary>
    public FerryStation FerryStation { get; set; }

    protected override FerryPassengerHandle CreatePassengerHandle()
    {
        return new FerryPassengerHandle(this);
    }

    protected override FerrySteeringHandle CreateSteeringHandle(IFerrySteeringCapable driver)
    {
        return new FerrySteeringHandle(Layer.GraphEnvironment, driver, this);
    }
}