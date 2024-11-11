using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;


namespace SOHModel.SemiTruck.Steering
{
    /// <summary>
    ///     A simplified steering handle for moving a semi-truck along a specified route.
    /// </summary>
    public class SemiTruckSteeringHandle : VehicleSteeringHandle<ISemiTruckSteeringCapable, IPassengerCapable, SemiTruckSteeringHandle, SemiTruckPassengerHandle>
    {
        public SemiTruckSteeringHandle(ISpatialGraphEnvironment environment, Model.SemiTruck semiTruck) :
            base(environment, semiTruck, semiTruck.MaxSpeed)
        {
        }

    }
}