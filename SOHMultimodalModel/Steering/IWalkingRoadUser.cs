using Mars.Interfaces;
using SOHDomain.Model;

namespace SOHMultimodalModel.Steering;

/// <summary>
///     TODO docu
/// </summary>
public interface IWalkingRoadUser
{
    ISimulationContext Context { get; }
    WalkingShoes WalkingShoes { get; }
    double Bearing { get; set; }
    double Velocity { get; set; }
    double PreferredSpeed { get; }
    double PerceptionInMeter { get; }
}