using System;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHVeddelFloodBox;

public class VeddelTraveler : HumanTraveler
{
    private bool _alreadyAtGoal;
    private Position _evacuationPoint;
    private bool _findRouteFailed;
    private Household _household;
    private bool _isRegisteredAtFamilyMeetingPoint;
    private HumanTravelerLayer _layer;

    public Household Household
    {
        get => _household;
        set
        {
            if (value.FamilyMembers.Contains(this) == false)
                value.AddFamilyMember(this);
            _household = value;
        }
    }

    public double FamilyDistanceThreshold { get; set; } = 20;
    public double MinTargetDistance { get; set; } = 2;

    public Position EvacuationPoint
    {
        get => _evacuationPoint;
        set
        {
            GoalPosition = value;
            _evacuationPoint = value;
        }
    }

    public OutputBuilder OutputBuilder { get; set; }
    public bool Finished { get; set; }

    public override void Init(HumanTravelerLayer layer)
    {
        _layer = layer;
        base.Init(layer);
    }

    public void OnMeetingPointDecided(Position meetingPoint)
    {
        GoalPosition = meetingPoint;
        _isRegisteredAtFamilyMeetingPoint = false;
        FindRoute();
    }

    public void OnFamilyGathered()
    {
        GoalPosition = EvacuationPoint.Copy();
        FindRoute();
    }

    public override void Tick()
    {
        // Agent has been flooded and is now being unregistered.

        if (_layer.SpatialGraphMediatorLayer.Environment.NearestNode(Position).Attributes
            .TryGetValue("underwater", out _))
        {
            Console.WriteLine("tot");
            MultimodalLayer.UnregisterAgent(MultimodalLayer, this);
            if (OutputBuilder != null)
                if (Household.Type == HouseholdType.WAITING) // ignore agents that are waiting at home
                {
                    var diff = SimulationTime.Subtract(Context.StartTimePoint.GetValueOrDefault());
                    OutputBuilder.AddTravelerDead((int)Math.Round(diff.TotalSeconds));
                }

            return;
        }

        if (Household.Type == HouseholdType.WAITING) return;

        if (Household.Type == HouseholdType.DELAY)
        {
            var diff = SimulationTime.Subtract(Context.StartTimePoint.GetValueOrDefault());
            if (diff.TotalSeconds >= _household.DelayTime)
                Move();
        }

        if (Household.Type == HouseholdType.WALKING) Move();
    }

    public override void Move()
    {
        if (Finished)
            return;
        if (MultimodalRoute == null && _alreadyAtGoal == false)
            FindRoute();

        // sometimes FindRoute doesn't find a suitable route
        // therefore try again.
        if (_findRouteFailed)
            FindRoute();

        // check if a family member got separated and decide on a meeting point
        if (Household.MeetingPoint == null)
            CheckFamilyMemberSeparated();

        if ((GoalReached || _alreadyAtGoal)
            && !_isRegisteredAtFamilyMeetingPoint
            && GoalPosition.Equals(Household.MeetingPoint))
        {
            Household.AddtravelerToMeetingPoint(this);
            _isRegisteredAtFamilyMeetingPoint = true;
        }

        try
        {
            if (MultimodalRoute != null && !GoalReached && !_alreadyAtGoal)
                base.Move();
        }
        catch (Exception)
        {
            //Console.WriteLine(e);
        }

        if ((GoalReached || _alreadyAtGoal) && GoalPosition.DistanceInMTo(EvacuationPoint) < MinTargetDistance)
        {
            Finished = true;
            if (OutputBuilder != null)
            {
                var diff = SimulationTime.Subtract(Context.StartTimePoint.GetValueOrDefault());
                OutputBuilder.AddTravelerFinished((int)Math.Round(diff.TotalSeconds));
            }

            MultimodalLayer.UnregisterAgent(MultimodalLayer, this);
        }
    }


    private void FindRoute()
    {
        var startNode = EnvironmentLayer.Environment.NearestNode(Position, null, SpatialModalityType.Walking);
        var goalNode = EnvironmentLayer.Environment.NearestNode(GoalPosition, null, SpatialModalityType.Walking);

        var tmp = MultimodalLayer.Search(this, Position, GoalPosition, ModalChoice.Walking);
        if (startNode.Position.Equals(goalNode.Position))
        {
            _alreadyAtGoal = true;
            _findRouteFailed = false;
            MultimodalRoute = tmp;
            return;
        }

        _alreadyAtGoal = false;

        if (tmp.Goal != null)
        {
            MultimodalRoute = tmp;
            _findRouteFailed = false;
        }
        else
        {
            _findRouteFailed = true;
            Console.WriteLine("Error: Couldn't find route");
        }
    }

    private void CheckFamilyMemberSeparated()
    {
        // don't do anything if a meetingPoint already exists
        if (Household.MeetingPoint != null)
            return;

        // check if a Family member git separated
        foreach (var target in Household.FamilyMembers)
        {
            var distance = Position.DistanceInMTo(target.Position);
            if (distance >= FamilyDistanceThreshold) Household.VoteFamilyMeetingPoint();
        }
    }
}