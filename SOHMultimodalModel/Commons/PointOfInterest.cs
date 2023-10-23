using Mars.Interfaces.Environments;
using SOHMultimodalModel.Planning;

namespace SOHMultimodalModel.Commons;

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