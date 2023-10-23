using Mars.Interfaces.Environments;

namespace SOHDomain.Steering.Handles;

/// <summary>
///     The <class>ISteeringHandle</class> extends the <class>IPassengerHandle</class> by the possibility to move a
///     vehicle.
/// </summary>
public interface ISteeringHandle : IPassengerHandle
{
    /// <summary>
    ///     The main environment on which the steering operates.
    /// </summary>
    ISpatialGraphEnvironment Environment { get; }

    /// <summary>
    ///     The current route on which the steering operates.
    /// </summary>
    Route Route { get; set; }

    /// <summary>
    ///     Determines if the goal of the current route has be reached.
    /// </summary>
    bool GoalReached { get; }

    /// <summary>
    ///     Gives the current velocity of the vehicle and thus all its passengers.
    /// </summary>
    double Velocity { get; }

    /// <summary>
    ///     Provides the possibility to tick the moving road user.
    /// </summary>
    void Move();
}