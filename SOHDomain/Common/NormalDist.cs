using System;
using System.Runtime.CompilerServices;
using Mars.Common.Core.Random;

namespace SOHDomain.Common;

public static class NormalDist
{
    /// <summary>
    ///     Normal distribution calculation (Box-Muller transform) for given parameters.
    /// </summary>
    /// <param name="mean"></param>
    /// <param name="standardDeviation"></param>
    /// <returns>A normal distribution result for given params.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double NormalDistRandom(double mean, double standardDeviation)
    {
        var u1 = 1.0 - RandomHelper.Random.NextDouble(); //uniform(0,1] random doubles
        var u2 = 1.0 - RandomHelper.Random.NextDouble();
        // var u2 = RandomHelper.Random.NextDouble();
        var randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2); //random normal(0,1)
        return mean + standardDeviation * randStdNormal; //random normal(mean,stdDev^2)            
    }
}