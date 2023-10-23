using System;

namespace SOHMultimodalModel.Planning;

/// <summary>
///     This enum describes all supported dayplan kinds.
/// </summary>
public enum TripReason
{
    HomeTime, //yippi! :)
    Eat,
    Work,
    FreeTime,
    Errands
}

/// <summary>
///     A <code>Trip</code> is a journey from a position A to a position B.
/// </summary>
public class Trip
{
    public Trip(TripReason tripReason, DateTime startTime)
    {
        TripReason = tripReason;
        StartTime = startTime;
    }

    public TripReason? TripReason { get; }
    public DateTime StartTime { get; set; }
}