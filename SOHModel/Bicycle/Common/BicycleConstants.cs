using SOHModel.Domain.Common;

namespace SOHModel.Bicycle.Common;

public static class BicycleConstants
{
    public const double G = 9.81;

    // it is assumed that for normal turning people desire to have to lean just a little bit
    // Schrittgeschwindigkeit? potenziell macht man das ja mit den Füßen auf dem Boden
//        public static readonly double UTurnSpeed = CalcMaxTurnSpeed(30);
    public static readonly double UTurnSpeed = 1.389;

    // 33% von 50P generell (18/3)
//        public static readonly double SharpTurnSpeed = CalcMaxTurnSpeed(20);
    public static readonly double SharpTurnSpeed = 1.667;

//        66% von 50P generell (18/3 * 2)
//        public static readonly double RegularTurnSpeed = CalcMaxTurnSpeed(10);
    public static readonly double RegularTurnSpeed = 3.33;

    // Normalgeschw. // 100%
//        public static readonly double WideTurnSpeed = CalcMaxTurnSpeed(5);
    public static readonly double WideTurnSpeed = 5;

    public static readonly double IntersectionSpeed = 0;

//        public static readonly double IntersectionSpeed = CalcMaxTurnSpeed(20);
//        public static readonly double AverageDeceleration = 5;
    // TODO it is assumed and observed that showing a bicycle is as slow as a normal human walking
    public static readonly double ShowingSpeed =
        (HumanVelocityConstants.MeanValueWalkFemale + HumanVelocityConstants.MeanValueWalkMale) / 2;


    public static readonly double MaxDeceleration = 7;
//        public static readonly double MaxPossibleTurnSpeed = CalcMaxTurnSpeed(30);

    public static readonly double MaxDecelFactor = -5;
    public static readonly double Efficiency = 0.95;
    public static readonly double MaxAccelerationFactor = 3;

    private static readonly double DecelRearWheelDryRoad = 2.56;
    private static readonly double DecelRearWheelWetRoad = 1.92;
    private static readonly double DecelFrontWheelDryRoad = 5.79;
    private static readonly double DecelFrontWheelWetRoad = 4.01;
    private static readonly double MeanDecelRearWheel = (DecelRearWheelDryRoad + DecelRearWheelWetRoad) / 2;
    private static readonly double MeanDecelFrontWheel = (DecelFrontWheelDryRoad + DecelFrontWheelWetRoad) / 2;

    public static readonly double MeanDecel = (MeanDecelFrontWheel + MeanDecelRearWheel) / 2;
    // public static readonly double MeanDecel = (DecelFrontWheelDryRoad + DecelRearWheelDryRoad) / 2;
//            bremsverzögerung trocken 	2,56
//        bremsverzögerung nass 	1,92
//        Vorderrad	
//            bremsverzögerung trocken 	5,79
//        bremsverzögerung nass 	4,01
//        4,175


    // https://www.bikeforums.net/road-cycling/574851-lean-angle-road-bike.html
    // https://en.wikipedia.org/wiki/Bicycle_and_motorcycle_dynamics#Turning
//        public static double CalcMaxTurnSpeed(double desiredAngle)
//        {
//            // theta = arctan(v^2/gr)
//            // https://www.wolframalpha.com/widgets/view.jsp?id=bcb6517b0d0f23538294d298e97c00c5
//            // -> v = (Sqrt(g) * Sqrt(tan(theta))) / sqrt(r)
//            // because 45 are only possible for dry and clean roads and new tires, the angle will be reduced
//            // for simplicity the radius is ignored
//            // wurzel 9.81 = 3.132
//            // sqrt(tan(30)) = 0.76
//            // sqrt(9.81) = 3.13 -> 2.3788
//            // sqrt(1) = 1 -> 2.3788
//            // sqrt(10) = 3.16 -> 0.75
//            // sqrt(100) = 10 -> 0.23788
//            double angle      = ConvertDegreeToRadian(desiredAngle);
//            double firstPart  = Math.Sqrt(G);
//            double tan        = Math.Tan(angle);
//            double secondPart = Math.Sqrt(tan);
//            return firstPart * secondPart;
////            return 3.132 * Math.Sqrt(Math.Tan(desiredAngle));
//        }

//        private static double ConvertDegreeToRadian(double degree)
//        {
//            return Math.PI * degree / 180.0;
//        }
}