namespace SOHCarModel.Parking;

/// <summary>
///     Any vehicle that may park on the parking layer must implement this interface.
/// </summary>
public interface IParkingCar
{
    /// <summary>
    ///     Holds a reference to the parking space on that the vehicle is parked (if following the protocol correctly)
    /// </summary>
    CarParkingSpace CarParkingSpace { get; set; }
}