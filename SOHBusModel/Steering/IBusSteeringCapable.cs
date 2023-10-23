using SOHBusModel.Route;
using SOHDomain.Steering.Capables;

namespace SOHBusModel.Steering;

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