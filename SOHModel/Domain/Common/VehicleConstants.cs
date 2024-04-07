using Mars.Interfaces.Environments;
using SOHModel.Domain.Model;

namespace SOHModel.Domain.Common;

/// <summary>
///     Define mechanical quantities concerning a standard <code>Vehicle</code>.
/// </summary>
public static class VehicleConstants
{
    public const double SharpTurnSpeed = 2.7;
    public const double RegularTurnSpeed = 4.1;
    public const double WideTurnSpeed = 5.5;
    public const double UTurnSpeed = 1.38;
    public const double IntersectionSpeed = 2.7;

    public static double TurningSpeedFor(this RoadUser roadUser, DirectionType direction)
    {
        switch (direction)
        {
            case DirectionType.Up:
                return roadUser.Velocity;
            case DirectionType.Down:
                return UTurnSpeed;
            case DirectionType.DownLeft:
            case DirectionType.DownRight:
                return SharpTurnSpeed;
            case DirectionType.Left:
            case DirectionType.Right:
                return RegularTurnSpeed;
            case DirectionType.UpLeft:
            case DirectionType.UpRight:
                return WideTurnSpeed;
        }

        return 0.0;
    }
}