using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHDomain.Model;

namespace SOHBicycleModel.Rental;

/// <summary>
///     The <code>IBicycleRentalLayer</code> capsules the access to all <code>BicycleRentalStation</code>s.
/// </summary>
public interface IBicycleRentalLayer : IVectorLayer<BicycleRentalStation>, IModalLayer
{
    /// <summary>
    ///     Tries to find the nearest <code>BicycleRentalStation</code> for given parameters.
    /// </summary>
    /// <param name="position">Start point to search by range.</param>
    /// <param name="notEmpty">Only finds rental points that have bicycles available.</param>
    /// <returns>The corresponding <code>BicycleRentalStation</code> if one is found, null otherwise.</returns>
    BicycleRentalStation Nearest(Position position, bool notEmpty);
}