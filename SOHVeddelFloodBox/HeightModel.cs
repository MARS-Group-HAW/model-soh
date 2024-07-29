using System.Collections.Generic;
using System.IO;
using Mars.Interfaces.Environments;
using ServiceStack;

namespace SOHVeddelFloodBox;

public class HeightModel
{
    private readonly List<HeightData> _listHeightData = new();

    public void Read()
    {
        using var reader = new StreamReader("resources/veddel_height.csv");

        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (line == null) continue;

            var values = line.Split(',');

            _listHeightData.Add(new HeightData(values[2].ToDouble(),
                values[3].ToDouble(), values[4].ToDouble()));
        }
    }

    public double DetectBestHeight(Position position)
    {
        double distanceAlt = 1000;
        var partNearest = new HeightData(0, 0, 0);
        foreach (var part in _listHeightData)
        {
            var distance = position.DistanceInMTo(part.Longitude, part.Latitude);
            if (distance < distanceAlt)
            {
                distanceAlt = distance;
                partNearest = part;
            }
        }

        return partNearest.Height;
    }

    public struct HeightData
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Height { get; set; }

        public HeightData(double latitude, double longitude, double height)
        {
            Latitude = latitude;
            Longitude = longitude;
            Height = height;
        }
    }
}