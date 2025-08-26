using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Tram.Station;
using SOHModel.Tram.Steering;
namespace SOHModel.Tram.Model;

public class Tram: Vehicle<ITramSteeringCapable, IPassengerCapable, TramSteeringHandle, TramPassengerHandle>
{
    public Tram()
    {
        IsCollidingEntity = false;
        ModalityType = SpatialModalityType.TrainDriving;
    }

    public TramLayer Layer { get; set; }

    /// <summary>
    ///     Where the <see cref="Tram" /> is located right now. Null if tram is not at any station right now.
    /// </summary>
    public TramStation TramStation { get; set; }

    protected override TramPassengerHandle CreatePassengerHandle()
    {
        return new TramPassengerHandle(this);
    }

    protected override TramSteeringHandle CreateSteeringHandle(ITramSteeringCapable driver)
    {
        return new TramSteeringHandle(Layer.GraphEnvironment, driver, this);
    }
}