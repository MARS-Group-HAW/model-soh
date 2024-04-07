namespace SOHModel.Domain.Steering.Acceleration;

public class IntelligentDriverAccelerator : IVehicleAccelerator
{
    private const double SafeTimeHeadway = 1.6; //seconds
    private const double MaxAcceleration = 0.73; //meter per square second
    private const double ComfortableDeceleration = 1.67; //meter per square second
    private const double GapInCongestion = 2.0; //meter
    private const double GapInConvoy = 0.0; //meter
    private const int AccelerationExponent = 4;


    public double CalculateSpeedChange(double currentSpeed, double maxSpeed, double distanceToVehicleAhead,
        double speedVehicleAhead)
    {
        if (distanceToVehicleAhead <= 0) return -currentSpeed;

        var speedDiff = Math.Round(Math.Abs(speedVehicleAhead - currentSpeed), 3);
        var desiredGap = Math.Round(GapInCongestion + GapInConvoy * Math.Sqrt(currentSpeed / maxSpeed) +
                                    SafeTimeHeadway * currentSpeed +
                                    currentSpeed * speedDiff /
                                    (2 * Math.Sqrt(MaxAcceleration * ComfortableDeceleration)), 3);

        var speedChange = Math.Round(MaxAcceleration *
                                     (1 - Math.Pow(currentSpeed / maxSpeed, AccelerationExponent) -
                                      Math.Pow(desiredGap / distanceToVehicleAhead, 2)), 3);
        return speedChange;
    }
}