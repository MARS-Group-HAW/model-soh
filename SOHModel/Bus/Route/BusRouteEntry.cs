using SOHModel.Bus.Station;

namespace SOHModel.Bus.Route;

public class BusRouteEntry(BusStation from, BusStation to, in int minutes) : IEquatable<BusRouteEntry>
{
    public BusStation From { get; } = from;
    public BusStation To { get; } = to;

    public int Minutes { get; } = minutes;

    public bool Equals(BusRouteEntry? other)
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
        return Equals((BusRouteEntry)obj);
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