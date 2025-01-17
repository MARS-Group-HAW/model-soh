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