using Mars.Interfaces.Environments;
using SOHModel.Car.Model;
using SOHModel.Domain.Model;

namespace SOHModel.Car.Rental;

/// <summary>
///     The <code>ICarRentalLayer</code> capsules the access to all <code>RentalCar</code>s.
/// </summary>
public interface ICarRentalLayer : IModalLayer
{
    /// <summary>
    ///     Tries to find the nearest <code>RentalCar</code> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <returns>The corresponding <code>RentalCar</code> if one is found, null otherwise.</returns>
    RentalCar Nearest(Position position);

    /// <summary>
    ///     Inserts the given rentalCar at its current position.
    /// </summary>
    /// <param name="rentalCar">That is to be inserted.</param>
    /// <returns>True, if insertion was successful, false otherwise.</returns>
    bool Insert(RentalCar rentalCar);

    /// <summary>
    ///     Removes the given rentalCar from the index.
    /// </summary>
    /// <param name="rentalCar">That is to be removed.</param>
    /// <returns>True, if remove was successful, false otherwise.</returns>
    bool Remove(RentalCar rentalCar);
}