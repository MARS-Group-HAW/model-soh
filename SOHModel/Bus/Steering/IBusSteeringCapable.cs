using SOHModel.Bus.Route;
using SOHModel.Domain.Steering.Capables;

namespace SOHModel.Bus.Steering;

/// <summary>
///     A capable subclass is able to drive a bus.
/// </summary>
public interface IBusSteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     Describes the route along that the driver moves his/her bus
    /// </summary>
    public BusRoute BusRoute { get; }
}