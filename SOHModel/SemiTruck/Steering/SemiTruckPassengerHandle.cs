using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;

namespace SOHModel.SemiTruck.Steering
{
    /// <summary>
    ///     Provides the possibility to leave a semi-truck as driver or passenger.
    /// </summary>
    public class SemiTruckPassengerHandle : VehiclePassengerHandle<ISemiTruckSteeringCapable, IPassengerCapable, SemiTruckSteeringHandle, SemiTruckPassengerHandle>
    {
        public SemiTruckPassengerHandle(SemiTruck.Model.SemiTruck semiTruck) : base(semiTruck)
        {
        }
    }
}