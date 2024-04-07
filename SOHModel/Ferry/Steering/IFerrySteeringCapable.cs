using SOHModel.Domain.Steering.Capables;
using SOHModel.Ferry.Route;

namespace SOHModel.Ferry.Steering;

/// <summary>
///     A capable subclass is able to drive a ferry.
/// </summary>
public interface IFerrySteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     Describes the route along that the driver moves his/her ferry
    /// </summary>
    public FerryRoute FerryRoute { get; }
}