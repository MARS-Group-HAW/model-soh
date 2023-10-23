namespace SOHCarModel.Rental;

/// <summary>
///     Defines a capable that may use rental cars.
/// </summary>
public interface ICarRentalCapable
{
    /// <summary>
    ///     Access platform to rental cars.
    /// </summary>
    ICarRentalLayer CarRentalLayer { get; }
}