using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHVeddelFloodBox;

public class Household
{
    private readonly HashSet<VeddelTraveler> _familyAtMeetingPoint = new();

    private Position _meetingPoint;

    public Household(int numFamilyMembers, Geometry address)
    {
        NumFamilyMembers = numFamilyMembers;
        Address = address;
        InMembersHouse = 0;
        FamilyMembers = new List<VeddelTraveler>();
    }

    public int NumFamilyMembers { set; get; }
    public Geometry Address { set; get; }
    public List<VeddelTraveler> FamilyMembers { set; get; }
    public int InMembersHouse { get; set; }

    public HouseholdType Type { get; set; }

    public int DelayTime { get; set; }

    public Position MeetingPoint
    {
        get => _meetingPoint;
        set
        {
            lock (Lock)
            {
                if (value == null)
                {
                    _meetingPoint = null;
                    return;
                }

                // same point already set, do noting
                if (value.Equals(_meetingPoint))
                    return;

                _meetingPoint = value;
                foreach (var familyMember in FamilyMembers) familyMember.OnMeetingPointDecided(value);

                _familyAtMeetingPoint.Clear();
            }
        }
    }

    private object Lock { get; } = new();

    public void AddFamilyMember(VeddelTraveler familyVeddelTraveler)
    {
        FamilyMembers.Add(familyVeddelTraveler);
        if (familyVeddelTraveler.Household != this)
            familyVeddelTraveler.Household = this;
        InMembersHouse += 1;
    }

    public void IncrementNumFamilyMembers(int numberToAdd)
    {
        NumFamilyMembers += numberToAdd;
    }

    public void VoteFamilyMeetingPoint()
    {
        lock (Lock)
        {
            if (FamilyMembers.Any(traveler => traveler.Finished)) return;

            if (MeetingPoint != null)
                return;
            Position meetingPoint = null;
            var distance = double.MaxValue;
            foreach (var traveler in FamilyMembers)
                if (traveler.GoalPosition.Equals(traveler.EvacuationPoint))
                {
                    if (traveler.MultimodalRoute != null)
                    {
                        var length = traveler.MultimodalRoute.CurrentRoute.RemainingRouteDistanceToGoal;
                        if (length != 0 && length < distance)
                        {
                            distance = length;
                            meetingPoint = traveler.Position;
                        }
                    }
                }
                else
                {
                    // This else branch should never be executed due to the lock
                    var tmp = traveler.MultimodalLayer.Search(traveler, traveler.Position, traveler.GoalPosition.Copy(),
                        ModalChoice.Walking);

                    // Sometimes a route can't be found. This is ignored for now and should be examined in the future
                    if (tmp.Goal == null) continue;

                    if (tmp.RouteLength != 0 && tmp.RouteLength < distance)
                    {
                        distance = tmp.RouteLength;
                        meetingPoint = traveler.Position;
                    }
                }

            // set the meeting point and notify all travelers
            MeetingPoint = meetingPoint;
        }
    }

    public void AddtravelerToMeetingPoint(VeddelTraveler traveler)
    {
        lock (Lock)
        {
            _familyAtMeetingPoint.Add(traveler);
            if (_familyAtMeetingPoint.Count == FamilyMembers.Count)
            {
                foreach (var familyMemeber in FamilyMembers) familyMemeber.OnFamilyGathered();
                _meetingPoint = null;
            }
        }
    }
}