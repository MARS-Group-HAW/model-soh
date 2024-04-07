using SOHModel.Bicycle.Model;
using SOHModel.Bicycle.Steering;

namespace SOHModel.Bicycle.Rental;

/// <summary>
///     This is a specific <see cref="Bicycle" /> that can be leased from rental stations.
/// </summary>
public class RentalBicycle : Model.Bicycle, IRentalBicycle
{
    public BicycleRentalStation BicycleRentalStation { get; set; }
    public IBicycleRentalLayer BicycleRentalLayer { get; set; }

    protected override BicycleSteeringHandle CreateSteeringHandle(IBicycleSteeringCapable driver)
    {
        var handle = new BicycleSteeringHandle(Environment, this, driver);
        return handle;
    }
}