using SOHModel.Domain.Steering.Capables;
using SOHModel.Tram.Route;
namespace SOHModel.Tram.Steering;

public interface ITramSteeringCapable : ISteeringCapable
{
    /// <summary>
    ///     Describes the route along that the driver moves his/her tram
    /// </summary>
    public TramRoute TramRoute { get; }
    
}