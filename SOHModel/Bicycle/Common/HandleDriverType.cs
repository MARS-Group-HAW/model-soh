using Mars.Common.Core.Random;
using Mars.Interfaces.Model.Exceptions;

namespace SOHModel.Bicycle.Common;

public class HandleDriverType
{
    private static readonly double MinDriverRand = 0;
    private static readonly double MaxDriverRand = 1;
    private static readonly double Headwaytime = 1.5;
    private static readonly double FollowingVariation = 2;

    private static readonly double EnteringFollowingThreshold = -20; // CC3

    // private static readonly double NegativeFollowingThreshold = -0.25; // CC4
    // private static readonly double PositiveFollowingThreshold = 0.25; // CC5
    // private static readonly double OscillationSpeedDependency = 1; // CC6
    private static readonly double OscillationAcceleration = 0.20; // m / s^2, CC7

    public static double DetermineDriverRand(DriverType driverType)
    {
        var driverRandRange = MaxDriverRand - MinDriverRand;
        var part = driverRandRange / Enum.GetNames(typeof(DriverType)).Length;
        switch (driverType)
        {
            case DriverType.Aggressive:
                return RandomHelper.Random.NextDouble(2 * part, MaxDriverRand);
            case DriverType.Normal:
                return RandomHelper.Random.NextDouble(part, 2 * part);
            case DriverType.Defensive:
                return RandomHelper.Random.NextDouble(MinDriverRand, part);
            default:
                throw new MissingTypeException("Invalid driver type");
        }
    }

    public static double DetermineHeadwayTime(DriverType driverType)
    {
        switch (driverType)
        {
            case DriverType.Aggressive:
                return RandomHelper.Random.NextDouble(Headwaytime / 2, Headwaytime);
            case DriverType.Normal:
                return Headwaytime;
            case DriverType.Defensive:
                return RandomHelper.Random.NextDouble(Headwaytime, Headwaytime * 1.5);
            default:
                throw new MissingTypeException("Invalid driver type");
        }
    }

    public static double DetermineFollowingVariation(DriverType driverType)
    {
        switch (driverType)
        {
            case DriverType.Aggressive:
                return RandomHelper.Random.NextDouble(FollowingVariation / 2, FollowingVariation);
            case DriverType.Normal:
                return FollowingVariation;
            case DriverType.Defensive:
                return RandomHelper.Random.NextDouble(FollowingVariation, FollowingVariation * 1.5);
            default:
                throw new MissingTypeException("Invalid driver type");
        }
    }

    // TODO CC3
    public static double DetermineEnterFolowingThreshold(DriverType driverType)
    {
        switch (driverType)
        {
            case DriverType.Aggressive:
                return RandomHelper.Random.NextDouble(EnteringFollowingThreshold, EnteringFollowingThreshold / 2);
            case DriverType.Normal:
                return EnteringFollowingThreshold;
            case DriverType.Defensive:
                return RandomHelper.Random.NextDouble(EnteringFollowingThreshold * 1.5, EnteringFollowingThreshold);
            default:
                throw new MissingTypeException("Invalid driver type");
        }
    }

    // TODO CC4 + CC5 
//        public static double DetermineFollowingThreshold(DriverType driverType)
//        {
////            return DeterminePositiveFollowingThreshold(driverType);
//        }

//        // TODO CC4
//        public static double DetermineNegativeFollowingThreshold(DriverType driverType)
//        {
//            switch (driverType)
//            {
//                case DriverType.Aggressive:
//                    return new Random().NextDouble(NegativeFollowingThreshold, NegativeFollowingThreshold / 2);
//                case DriverType.Normal:
//                    return NegativeFollowingThreshold;
//                case DriverType.Defensive:
//                    return new Random().NextDouble(NegativeFollowingThreshold * 1.5, NegativeFollowingThreshold);
//                default:
//                    throw new MissingTypeException("Invalid driver type");
//            }
//        }
//        
//        // TODO CC5
//        public static double DeterminePositiveFollowingThreshold(DriverType driverType)
//        {
//            switch (driverType)
//            {
//                case DriverType.Aggressive:
//                    return new Random().NextDouble(PositiveFollowingThreshold / 2, PositiveFollowingThreshold);
//                case DriverType.Normal:
//                    return PositiveFollowingThreshold;
//                case DriverType.Defensive:
//                    return new Random().NextDouble(PositiveFollowingThreshold, PositiveFollowingThreshold * 1.5);
//                default:
//                    throw new MissingTypeException("Invalid driver type");
//            }
//        }

//        // TODO CC6
//        public static double DetermineOscillationSpeedDependency(DriverType driverType)
//        {
//            switch (driverType)
//            {
//                case DriverType.Aggressive:
//                    return new Random().NextDouble(FollowingVariation / 2, FollowingVariation);
//                case DriverType.Normal:
//                    return OscillationSpeedDependency;
//                case DriverType.Defensive:
//                    return new Random().NextDouble(FollowingVariation, FollowingVariation * 1.5);
//                default:
//                    throw new MissingTypeException("Invalid driver type");
//            }
//        }

    // TODO CC7
    public static double DetermineOscillationAcceleration(DriverType driverType)
    {
        switch (driverType)
        {
            case DriverType.Aggressive:
                return RandomHelper.Random.NextDouble(OscillationAcceleration, OscillationAcceleration * 1.5);
            case DriverType.Normal:
                return OscillationAcceleration;
            case DriverType.Defensive:
                return RandomHelper.Random.NextDouble(OscillationAcceleration / 2, OscillationAcceleration);
            default:
                throw new MissingTypeException("Invalid driver type");
        }
    }

    public static bool DecideIfOvertaking(DriverType driverType)
    {
//            var    part       = 100 / Enum.GetNames(typeof(DriverType)).Length;
        var likeliness = driverType switch
        {
            DriverType.Aggressive => RandomHelper.Random.NextDouble(40, 100),
            DriverType.Normal => RandomHelper.Random.NextDouble(0, 100),
            DriverType.Defensive => RandomHelper.Random.NextDouble(0, 60),
            _ => throw new MissingTypeException("Invalid driver type")
        };

        if (likeliness >= 50) return true;

        return false;
    }
}