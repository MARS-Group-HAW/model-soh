using Mars.Interfaces.Environments;
using SOHBicycleModel.Model;
using SOHDomain.Model;

namespace SOHBicycleModel.Parking;

public interface IBicycleParkingLayer : IModalLayer
{
    /// <summary>
    ///     Creates a <code>Bicycle</code> at a node or at a <code>BicycleParkingLot</code> close to given position.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="radius"></param>
    /// <param name="useBikeAndRideParkingPercentage"></param>
    /// <param name="keyAttribute"></param>
    /// <param name="type"></param>
    /// <returns></returns>
    Bicycle CreateOwnBicycleNear(Position position, double radius, double useBikeAndRideParkingPercentage,
        string keyAttribute = "type", string type = "city");
}