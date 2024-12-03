using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;

namespace SOHModel.SemiTruck.Model;

public interface ISemiTruckLayer : IModalLayer
{
    /// <summary>
    ///     Holds the environment that can be used for semiTrucks to move.
    /// </summary>
    ISpatialGraphEnvironment Environment { get; }
}