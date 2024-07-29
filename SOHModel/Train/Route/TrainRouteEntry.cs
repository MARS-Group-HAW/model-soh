using SOHModel.Train.Station;

namespace SOHModel.Train.Route;

public class TrainRouteEntry : IEquatable<TrainRouteEntry>
{
    public TrainRouteEntry(TrainStation from, TrainStation to, in int minutes)
    {
        From = from;
        To = to;
        Minutes = minutes;
    }

    public TrainStation From { get; }
    public TrainStation To { get; }

    public int Minutes { get; }

    public bool Equals(TrainRouteEntry? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(From, other.From) && Equals(To, other.To) && Minutes == other.Minutes;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != GetType()) return false;
        return Equals((TrainRouteEntry)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = From.GetHashCode();
            hashCode = (hashCode * 397) ^ Minutes.GetHashCode();
            hashCode = (hashCode * 397) ^ To.GetHashCode();
            return hashCode;
        }
    }
}