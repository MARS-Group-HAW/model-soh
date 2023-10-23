namespace SOHBicycleModel.Rental;

/// <summary>
///     This bicycle can be rented at a rental station.
/// </summary>
public interface IRentalBicycle
{
    /// <summary>
    ///     Holds a reference to the <see cref="BicycleRentalStation" />, where the vehicle is currently located.
    /// </summary>
    BicycleRentalStation BicycleRentalStation { get; set; }

    /// <summary>
    ///     Holds a reference to the <see cref="IBicycleRentalLayer" />, which provides access to all
    ///     <see cref="BicycleRentalStation" />
    /// </summary>
    IBicycleRentalLayer BicycleRentalLayer { get; set; }
}