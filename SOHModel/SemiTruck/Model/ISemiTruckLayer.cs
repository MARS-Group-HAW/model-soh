using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;
namespace SOHModel.SemiTruck.Model;

/// <summary>
///     Provides access to truck-relevant resources and environment.
/// </summary>
public interface ISemiTruckLayer : IModalLayer
{
    /// <summary>
    ///     Holds the environment that trucks use to navigate.
    /// </summary>
    ISpatialGraphEnvironment GraphEnvironment { get; }

    /// <summary>
    ///     Adds a new truck driver to the layer.
    /// </summary>
    /// <param name="driver">The truck driver to add.</param>
    void AddDriver(SemiTruckDriver driver);

    /// <summary>
    ///     Removes a truck driver from the layer.
    /// </summary>
    /// <param name="driver">The truck driver to remove.</param>
    void RemoveDriver(SemiTruckDriver driver);
}