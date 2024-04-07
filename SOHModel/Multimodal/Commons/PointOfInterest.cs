using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Planning;

namespace SOHModel.Multimodal.Commons;

public class PointOfInterest
{
    public PointOfInterest(TripReason tripReason, Position position)
    {
        TripReason = tripReason;
        Position = position;
    }

    public TripReason TripReason { get; }
    public Position Position { get; }
}