using System.Runtime.CompilerServices;
using Mars.Common.Core.Random;
using Mars.Numerics.Statistics;
using SOHDomain.Common;
using SOHMultimodalModel.Model;

namespace SOHMultimodalModel.Commons;

public static class PedestrianAverageSpeedGenerator
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateWalkingSpeed(GenderType gender)
    {
        return gender == GenderType.Male
            ? CalculateSpeed(HumanVelocityConstants.MeanValueWalkMale,
                HumanVelocityConstants.DeviationWalkMale)
            : CalculateSpeed(HumanVelocityConstants.MeanValueWalkFemale,
                HumanVelocityConstants.DeviationWalkFemale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double CalculateRunningSpeed(GenderType gender)
    {
        return gender == GenderType.Male
            ? CalculateSpeed(HumanVelocityConstants.MeanValueRunMale,
                HumanVelocityConstants.DeviationRunMale)
            : CalculateSpeed(HumanVelocityConstants.MeanValueRunFemale,
                HumanVelocityConstants.DeviationRunFemale);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double CalculateSpeed(double meanValue, double deviation)
    {
        return new FastGaussianDistributionD(meanValue, deviation / 3d).Next(RandomHelper.Random);
    }
}