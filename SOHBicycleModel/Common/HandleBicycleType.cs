using Mars.Common.Core.Random;

namespace SOHBicycleModel.Common;

public class HandleBicycleType
{
    //TODO in entity init csv verschieben und im bicylce speichern
    public static double GetBicycleWeight(BicycleType type)
    {
        //    Citybike	17	20	18,5
        //    Trekking	16	19	17,5
        //    Mountain	13,5	15	14,25
        //    Rennrad	11,5
//            if (type == BicycleType.Electro)
//            {
//                // https://easyebiking.com/how-much-does-an-e-bike-weigh/
//                return new Random().NextDouble(18.0, 22.0);
//            }
//            else 
        if (type == BicycleType.Racing)
            return RandomHelper.Random.NextDouble(10.0, 13.0);
        if (type == BicycleType.City)
            return RandomHelper.Random.NextDouble(17.0, 20.0);
        if (type == BicycleType.Trekking)
            return RandomHelper.Random.NextDouble(16.0, 18.0);
        if (type == BicycleType.Mountain)
            return RandomHelper.Random.NextDouble(13.0, 15.0);
        if (type == BicycleType.Cross)
            // https://www.profirad.de/fahrrad/crossbike/
            // https://www.autobild.de/vergleich/crossbike-test/
            return RandomHelper.Random.NextDouble(11.0, 16.0);
        if (type == BicycleType.Fitness)
            // https://www.testberichte.de/testsieger/level3_fahrrad_fitnessraeder_845.html
            // 
            return RandomHelper.Random.NextDouble(8.0, 13.0);
        if (type == BicycleType.LoadWheel)
            // 23,4
            // 21,5
            // 22,8
            // 22,8
            // 19,7
            // 18,5
            // 40
            // 16
            // 35
            // 38,4
            // 20
            // 65
            // 37
            // 60
            return RandomHelper.Random.NextDouble(16.0, 65.0);
        return 0;
    }

    public static double GetBicycleMaxLoadweight(BicycleType type)
    {
//            if (type == BicycleType.Electro)
//            {
//                return new Random().NextDouble(18.0, 22.0);
//            }
//            else 
        if (type == BicycleType.Racing)
            return RandomHelper.Random.NextDouble(10.0, 13.0);
        if (type == BicycleType.City)
            return RandomHelper.Random.NextDouble(17.0, 20.0);
        if (type == BicycleType.Trekking)
            return RandomHelper.Random.NextDouble(16.0, 18.0);
        if (type == BicycleType.Mountain)
            return RandomHelper.Random.NextDouble(13.0, 15.0);
        if (type == BicycleType.Cross)
            return RandomHelper.Random.NextDouble(11.0, 16.0);
        if (type == BicycleType.Fitness)
            return RandomHelper.Random.NextDouble(8.0, 13.0);
        if (type == BicycleType.LoadWheel)
            // standard hinten = 25
            // 10 + hinten = 35
            // 20 + hinten + fahrer < 120 = 43
            // 50 + hinten + fahrer < 165 = 90
            // vorne + hinten + fahrer < 100 = 23
            // vorne + hinten + fahrer < 125 = 48
            // 100 + 25 + 100 = 125
            // 80 + 0 + 100 = 80
            // 100 + 25 + 100 = 125
            // 100 + 25 + 100 = 125
            // 80 + 0 + 100 = 80
            // 100 + 0 + fahrer = 100
            // 80 + 50 + fahrer = 130
            // vorne + hinten + fahrer < 200 = 123
            // 100 + 0 + 100 = 100
            // 100 + 50 + fahrer < 180 = 150
            // 80 + 50 + fahrer = 130
            // 80 + 50 + fahrer = 130
            // 100 + fahrer = 100
            return RandomHelper.Random.NextDouble(16.0, 65.0);
        return 0;
    }
}