namespace SOHModel.Domain.Steering.Capables;

/// <summary>
///     The driver provides some properties that influence the driving.
/// </summary>
public interface ISteeringCapable : IPassengerCapable
{
    /// <summary>
    ///     Determines if the driver tries to overtake other cars when possible.
    /// </summary>
    bool OvertakingActivated { get; }


    /// <summary>
    ///     Determines if the driver did a brake. So the Vehicle comes to a stop.
    /// </summary>
    bool BrakingActivated { get; set; }
}