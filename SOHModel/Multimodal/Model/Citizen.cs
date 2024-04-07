using Mars.Common.Core.Random;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Domain.Common;
using SOHModel.Multimodal.Commons;
using SOHModel.Multimodal.Layers;
using SOHModel.Multimodal.Multimodal;
using SOHModel.Multimodal.Planning;

namespace SOHModel.Multimodal.Model;

/// <summary>
///     The <code>Citizen</code> is proceeding his/her dayplan by moving to different POIs within a day.
/// </summary>
public class Citizen : MultiCapableAgent<IMultimodalLayer>
{
    [PropertyDescription] public MediatorLayer MediatorLayer { get; set; }

    public override void Init(IMultimodalLayer layer)
    {
        StartPosition ??= MediatorLayer.VectorBuildingsLayer.RandomPosition();

        base.Init(layer);

        Home = new PointOfInterest(TripReason.HomeTime, StartPosition);

        if (Worker)
        {
            var workPosition = MediatorLayer.VectorLandUseLayer.RandomPosition();
            Work = new PointOfInterest(TripReason.Work, workPosition);
        }

        Tour = new Tour(layer.Context, Worker, PartTimeWorker);
    }

    private const double FreeTimeAtHomeProbability = 0.5;
    private const double EatAtHomeProbability = 0.8;
    private const double EatAtHomeDistance = 0.5;

    private bool _partTimeWorker;

    /// <summary>
    ///     Gets or sets the flag, indicating that his citizen is a full worker
    /// </summary>
    [PropertyDescription(Ignore = true, Name = "worker")]
    public bool Worker { get; set; }

    /// <summary>
    ///     Gets or sets a flag indicating that this citizen is a part-time worker and thus a <see cref="Worker" /> as well.
    /// </summary>
    [PropertyDescription(Ignore = true, Name = "partTimeWorker")]
    public bool PartTimeWorker
    {
        get => _partTimeWorker;
        set
        {
            if (value)
                Worker = _partTimeWorker = true;
            else
                _partTimeWorker = false;
        }
    }

    /// <summary>
    ///     Describes how much percent of the population has a car to their personal disposal
    /// </summary>
    [PropertyDescription(Ignore = true)]
    public double CapabilityDrivingWithProbability
    {
        set => CapabilityDrivingOwnCar = RandomHelper.Random.NextDouble() < value;
    }

    /// <summary>
    ///     Gets the associated day plan for this agent.
    /// </summary>
    public Tour Tour { get; set; }

    /// <summary>
    ///     Gets or sets the central <c>Home</c> POI of this agent.
    /// </summary>
    [PropertyDescription(Ignore = true)]
    public PointOfInterest Home { get; private set; }

    /// <summary>
    ///     Gets or sets the main <c>Work</c> POI of this agent.
    /// </summary>
    [PropertyDescription(Ignore = true)]
    public PointOfInterest Work { get; private set; }

    public double Height { get; set; }
    public double Width { get; set; }
    
    public override void Tick()
    {
        if (Tour.MoveNext())
        {
            var goalPosition = FindPositionForTrip(Tour.Current);
            MultimodalRoute = MultimodalLayer.Search(this, Position, goalPosition, Capabilities);
        }

        base.Move();
    }

    public void ChangeWork(Position position)
    {
        Work = new PointOfInterest(TripReason.Work, position);
    }

    public void ChangeHome(Position position)
    {
        Home = new PointOfInterest(TripReason.HomeTime, position);
    }

    private Position FindPositionForTrip(Trip trip)
    {
        return trip.TripReason switch
        {
            TripReason.Work => Work.Position,
            TripReason.HomeTime => Home.Position,
            TripReason.Eat => RandomHelper.Random.NextDouble() < EatAtHomeProbability &&
                              Position.DistanceInKmTo(Home.Position) < EatAtHomeDistance
                ? Home.Position
                : MediatorLayer.FindNextNearestLocationForAnyTarget(Position, OsmGroups.Eat),
            TripReason.FreeTime => RandomHelper.Random.NextDouble() < FreeTimeAtHomeProbability
                ? Home.Position
                : MediatorLayer.FindNextNearestLocationForAnyTarget(Position, OsmGroups.FreeTime),
            TripReason.Errands => MediatorLayer.FindNextNearestLocationForAnyTarget(Position, OsmGroups.Errand),
            _ => throw new ArgumentOutOfRangeException()
        };
    }
}