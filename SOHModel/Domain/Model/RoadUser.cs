using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;

namespace SOHModel.Domain.Model;

/// <summary>
///     The <see cref="RoadUser" /> is the the most abstract implementation of the <see cref="ISpatialGraphEntity" />
///     within the SmartOpenHamburg model. It can be anything from a person to a vehicle that
///     moves on a graph managed by the <see cref="ISpatialGraphEnvironment" />.
/// </summary>
public abstract class RoadUser : ISpatialGraphEntity
{
    /// <summary>
    ///     Standard constructor for type-initializing.
    /// </summary>
    protected RoadUser()
    {
        ID = Guid.NewGuid();
    }

    /// <summary>
    ///     Gets or sets the width of this entity in <c>meter (m)</c>.
    /// </summary>
    [PropertyDescription(Name = "width", Ignore = true)]
    public virtual double Width { get; set; }

    /// <summary>
    ///     Gets or sets the height of this entity in <c>meter (m)</c>.
    /// </summary>
    [PropertyDescription(Name = "height", Ignore = true)]
    public virtual double Height { get; set; }

    /// <summary>
    ///     Gets or set the mass of this entity in <c>kilogram (kg)</c>.
    /// </summary>
    [PropertyDescription(Name = "mass", Ignore = true)]
    public virtual double Mass { get; set; }

    /// <summary>
    ///     Gets or sets the acceleration of this entity in <c>meter per second square (m/s²)</c>
    /// </summary>
    [PropertyDescription(Name = "acceleration", Ignore = true)]
    public virtual double Acceleration { get; set; }

    /// <summary>
    ///     Gets or sets the current velocity of this entity in <c>meter per seconds (m/s)</c>.
    /// </summary>
    [PropertyDescription(Name = "velocity", Ignore = true)]
    public double Velocity { get; set; }

    /// <summary>
    ///     Gets the remaining distance in <c>meter (m)</c>
    ///     until the <see cref="CurrentEdge" /> is passed.
    /// </summary>
    public double RemainingDistanceOnEdge
    {
        get
        {
            if (CurrentEdge != null && PositionOnCurrentEdge >= 0)
                return CurrentEdge.Length - PositionOnCurrentEdge;
            return -1;
        }
    }

    /// <summary>
    ///     Gets or sets the bearing of this entity in degree 0-360.
    /// </summary>
    public double Bearing { get; set; }

    /// <summary>
    ///     Gets or sets the flag indicating that the entity is moving on an opposite edge.
    /// </summary>
    public bool IsWrongWayDriving { get; set; }

    /// <summary>
    ///     Gets or set a unique identifier of this entity
    ///     used within the runtime- and agent-environment
    ///     as well as for differentiation within the output.
    /// </summary>
    [PropertyDescription(Name = "id")]
    public Guid ID { get; set; }

    /// <summary>
    ///     Gets or sets the length of this entity in <c>meter (m)</c>.
    /// </summary>
    [PropertyDescription(Name = "length", Ignore = true)]
    public double Length { get; set; }

    /// <summary>
    ///     Gets or sets the maxIncline of this entity in <c>percentage (%)</c>.
    /// </summary>
    [PropertyDescription(Name = "maxIncline", Ignore = true)]
    public virtual int MaxIncline { get; set; }
    
    /// <summary>
    /// Statistical probability of a truck accident per kilometer (or per tick, depending on usage).
    /// </summary>
    [PropertyDescription(Name = "accidentsPerYear", Ignore = true)]
    public double AccidentsPerYear { get; set; }
    
    /// <summary>
    /// The type of fuel source/energy carrier used by the vehicle (Fuel, Battery, Hydrogen, etc.)
    /// Note the different units for energy types, such as liters for fuel, kWh for batteries, kg for hydrogen.
    /// </summary>
    [PropertyDescription(Name = "fuelCarrierType", Ignore = true)]
    public FuelCarrierType FuelCarrierType { get; set; } = FuelCarrierType.Fuel;

    /// <summary>
    /// The maximum amount of its fuel source/energy carrier a truck can hold
    /// Upon initialization, the vehicle starts with a full tank
    /// </summary>
    [PropertyDescription(Name = "maxFuelCarrierAmount", Ignore = true)]
    public double MaxFuelCarrierAmount { get; set; }
    
    /// <summary>
    /// The average amount of fuel a truck uses during 100km
    /// Used for linear interpolation
    /// </summary>
    [PropertyDescription(Name = "fuelConsumptionPer100km", Ignore = true)]
    public double FuelConsumptionPer100Km  { get; set; }

    /// <summary>
    /// Drag coefficient of the vehicle (unitless).
    /// TODO: example value, consult drag coefficient table
    /// </summary>
    [PropertyDescription(Name = "dragCoefficient", Ignore = true)]
    public double DragCoefficient { get; set; } = 0.6;

    /// <summary>
    /// Rolling resistance coefficient (unitless).
    /// TODO: not constant, see https://doi.org/10.1109/TVT.2022.3220157 
    /// </summary>
    [PropertyDescription(Name = "rollingResistance", Ignore = true)]
    public double RollingResistance { get; set; } = 0.006;

    /// <summary>
    /// Tank-To-Wheel efficiency of the engine/drivetrain (0-1).
    /// https://doi.org/10.1016/j.enconman.2022.115412
    /// </summary>
    [PropertyDescription(Name = "tank2wheel", Ignore = true)]
    public double Tank2WheelEfficiency { get; set; } = 0.35;

    /// <summary>
    /// The time it takes to refuel/recharge the vehicle in minutes.
    /// Note: During a refuel pause, after this time elapses, the tank is reset to its maximum.
    /// In the future, refueling could be implemented as a rate, like <c>refuelEnergyAmountPerMinute</c>, and set target refuel tank levels.
    /// </summary>
    [PropertyDescription(Name = "refuelTimeInMinutes", Ignore = true)]
    public double RefuelTimeInMinutes { get; set; } = 5;

    
    
    /// <summary>
    /// Amount of Power that a truck has in KW
    /// </summary>
    [PropertyDescription(Name = "power", Ignore = true)]
    public double Power { get; set; }

    
    /// <summary>
    ///     Gets or sets the position of this entity with (lon/x, lat/y) coordinate.
    /// </summary>
    public virtual Position Position { get; set; }

    /// <summary>
    ///     Gets or sets the current edge where the entity is currently standing on.
    /// </summary>
    /// <remarks>
    ///     When this property is <c>null</c> the entity needs first to
    ///     be inserted on an  <see cref="ISpatialGraphEnvironment" />.
    /// </remarks>
    public ISpatialEdge CurrentEdge { get; set; }

    /// <summary>
    ///     Gets or sets the current position in <c>meter (m)</c> on the <see cref="CurrentEdge" />
    /// </summary>
    /// <remarks>
    ///     When <see cref="CurrentEdge" /> is null this property is not relevant.
    /// </remarks>
    public double PositionOnCurrentEdge { get; set; }

    /// <summary>
    ///     Gets or sets the lane where this entity is standing on the <see cref="CurrentEdge" />.
    /// </summary>
    /// <remarks>
    ///     The lane is an index between <c>[0,CurrentEdge.LaneCount)</c>.
    /// </remarks>
    public int LaneOnCurrentEdge { get; set; }

    /// <summary>
    ///     Gets the modal type restricting the entity how and where it can move on.
    /// </summary>
    public SpatialModalityType ModalityType { get; protected set; }

    /// <summary>
    ///     Gets the flag, indicating that moving operations of this
    ///     entity are checked for collision with other ones.
    /// </summary>
    public bool IsCollidingEntity { get; set; } = true;
}