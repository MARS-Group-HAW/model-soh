using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHDomain.Common;
using SOHDomain.Steering.Capables;
using SOHDomain.Steering.Handles;

namespace SOHDomain.Model;

/// <summary>
///     The <code>Vehicle</code> is the definition of all <code>RoadUser</code>s that are usable for drivers and their
///     passengers.
/// </summary>
/// <typeparam name="TSteeringCapable">Defines the capabilities of someone who is able to steer this vehicle.</typeparam>
/// <typeparam name="TPassengerCapable">Defines the capabilities of someone who is able to co-drive in this vehicle.</typeparam>
/// <typeparam name="TSteeringHandle">
///     Defines the "cockpit" of this vehicle. The steering handle allows to navigate the
///     vehicle.
/// </typeparam>
/// <typeparam name="TPassengerHandle">Defines the actions that a passenger have within this vehicle.</typeparam>
public abstract partial class Vehicle<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle> :
    RoadUser, IVehicleEntrance<TSteeringCapable, TPassengerCapable, TSteeringHandle, TPassengerHandle>
    where TPassengerHandle : IPassengerHandle
    where TSteeringHandle : ISteeringHandle
    where TPassengerCapable : IPassengerCapable
    where TSteeringCapable : ISteeringCapable
{
    private string _trafficCode;

    /// <summary>
    ///     Get or sets the intersection behaviour model identified by code when no traffic signals are available
    ///     "german" = right before left rule
    ///     "southAfrica" = first in first out (FIFO) rule
    /// </summary>
    [PropertyDescription(Name = "trafficCode", Ignore = true)]
    public string TrafficCode
    {
        get => _trafficCode ??= "german";
        set => _trafficCode = value;
    }

    /// <summary>
    ///     Gets or sets the maximum moving speed limit of this vehicle in <c>meter per second (m/s)</c>.
    /// </summary>
    [PropertyDescription(Name = "maxSpeed", Ignore = true)]
    public double MaxSpeed { get; set; } = 13.89;

    /// <summary>
    ///     Gets or sets the maximum acceleration to <c>increase</c> the speed of this
    ///     vehicle in <c>meter per squared second (m/s²)</c>.
    /// </summary>
    [PropertyDescription(Name = "maxAcceleration", Ignore = true)]
    public double MaxAcceleration { get; set; } = 1000;

    /// <summary>
    ///     Gets or sets the maximum deceleration to <c>decrease</c> the speed of this
    ///     vehicle in <c>meter per squared second (m/s²)</c>.
    /// </summary>
    [PropertyDescription(Name = "maxDeceleration", Ignore = true)]
    public double MaxDeceleration { get; set; } = 1000;

    /// <summary>
    ///     Gets or sets the maximum amount of passenger (without the driver itself) in this vehicle.
    /// </summary>
    /// <remarks>
    ///     Suitable parameter values are 0, 2, 4,5 seats for default bicycle, or car vehicles.
    /// </remarks>
    [PropertyDescription(Name = "passengerCapacity", Ignore = true)]
    public int PassengerCapacity { get; set; } = 1;

    /// <summary>
    ///     Gets or sets the factor >= 0 to explore/look forward or backward for other entiy
    ///     in order to interacting with them.
    /// </summary>
    /// <remarks>
    ///     This factor need only suitable value in order to check for the most recent case.
    ///     As greater this value is, as worse execution performance.
    /// </remarks>
    [PropertyDescription(Name = "exploreDistanceFactor", Ignore = true)]
    public int ExploreDistanceFactor { get; set; } = 6;

    /// <summary>
    ///     Gets or sets the maximum speed when turning at intersection very sharp,
    ///     defined by the <see cref="DirectionType.DownLeft" /> or <see cref="DirectionType.DownRight" />.
    /// </summary>
    [PropertyDescription(Name = "sharpTurnSpeed", Ignore = true)]
    public double SharpTurnSpeed { get; set; } = VehicleConstants.SharpTurnSpeed;

    /// <summary>
    ///     Gets or sets the maximum speed when turning at a default intersection,
    ///     defined by the <see cref="DirectionType.Left" /> or <see cref="DirectionType.Right" />.
    /// </summary>
    [PropertyDescription(Name = "regularTurnSpeed", Ignore = true)]
    public double RegularTurnSpeed { get; set; } = VehicleConstants.RegularTurnSpeed;

    /// <summary>
    ///     Gets or sets the maximum speed when turning at intersection very wide,
    ///     defined by the <see cref="DirectionType.UpLeft" /> or <see cref="DirectionType.UpRight" />.
    /// </summary>
    [PropertyDescription(Name = "wideTurnSpeed", Ignore = true)]
    public double WideTurnSpeed { get; set; } = VehicleConstants.WideTurnSpeed;

    /// <summary>
    ///     Gets or sets the maximum speed when making a U-turn at an intersection,
    ///     defined by the <see cref="DirectionType.Down" />.
    /// </summary>
    [PropertyDescription(Name = "uTurnSpeed", Ignore = true)]
    public double UTurnSpeed { get; set; } = VehicleConstants.UTurnSpeed;

    /// <summary>
    ///     Gets or sets the maximum speed when moving forward and passing an intersection,
    ///     defined by the <see cref="DirectionType.Up" />.
    /// </summary>
    [PropertyDescription(Name = "intersectionSpeed", Ignore = true)]
    public double IntersectionSpeed { get; set; } = VehicleConstants.IntersectionSpeed;

    /// <summary>
    ///     Gets or sets the driver of this vehicle, which acts at the active agent entity, using the vehicle.
    /// </summary>
    public ISteeringCapable Driver { get; set; }

    /// <summary>
    ///     Gets the turning speed when passing an intersection for the given relative target
    ///     <para>direction</para>
    ///     .
    /// </summary>
    /// <param name="direction">The relative direction compass with </param>
    /// <returns>
    ///     Returns the maximum speed to adjust in <c>meter per seconds (m/s)</c>,
    ///     used when driving into the target relative
    ///     <para>direction</para>
    ///     .
    /// </returns>
    public double TurningSpeedFor(DirectionType direction)
    {
        switch (direction)
        {
            case DirectionType.Down:
                return UTurnSpeed;
            case DirectionType.DownLeft:
            case DirectionType.DownRight:
                return SharpTurnSpeed;
            case DirectionType.Left:
            case DirectionType.Right:
                return RegularTurnSpeed;
            case DirectionType.UpLeft:
            case DirectionType.UpRight:
                return WideTurnSpeed;
        }

        return 0.0;
    }

    public virtual bool HasFreeCapacity()
    {
        return PassengerCapacity > Passengers.Count;
    }

    /// <summary>
    ///     Creates a passenger handle that provides access to passenger functionality.
    /// </summary>
    /// <returns>A handle for passenger functionality.</returns>
    protected abstract TPassengerHandle CreatePassengerHandle();

    /// <summary>
    ///     Creates a steering handle that provides access to steering functionality.
    /// </summary>
    /// <returns>A handle for steering functionality.</returns>
    protected abstract TSteeringHandle CreateSteeringHandle(TSteeringCapable driver);
}