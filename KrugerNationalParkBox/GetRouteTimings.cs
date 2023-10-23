using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Import;
using Mars.Interfaces.Model.Options;
using System.Text.Json;
using KrugerNationalPark.Misc;

namespace KrugerNationalParkStarter
{
    public static class GetRouteTimings
    {
        public static void Timings()
        {
            // Build Spatial graph env.
            ISpatialGraphEnvironment spatialGraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Source>
                {
                    new()
                    {
                        File = "resources/knp_graph.graphml",
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true,
                            Modalities = new HashSet<SpatialModalityType> {SpatialModalityType.CarDriving}
                        }
                    }
                },
                NodeIndex = true
            });
            
            // load POIs
            var pois = new List<Position>();
            var originNames = new List<(string, string)>();

            using (var reader = new StreamReader(@"./camp_waypoints.csv"))
            {
                // skip header of input file
                reader.ReadLine();
                // read first relevant line
                var line = reader.ReadLine();
                if (line != null)
                {
                    // dynamically identify the separator used in input file
                    var commaIndex = line.IndexOf(',');
                    var semicolonIndex = line.IndexOf(';');
                    var separator = semicolonIndex == -1 ? line[commaIndex] : line[Math.Min(commaIndex, semicolonIndex)];
                    while (line != null)
                    {
                        var lineValues = line.Split(separator);

                        var originName = lineValues[0];
                        var originCampType = lineValues[1];
                        originNames.Add((originName, originCampType));
                        var originPos = new Position(Convert.ToDouble(lineValues[2], CultureInfo.InvariantCulture),
                            Convert.ToDouble(lineValues[3], CultureInfo.InvariantCulture));
                        pois.Add(originPos);
                        line = reader.ReadLine();
                    }
                }
            }

            var routeInfoList = new List<OriginPOCO>();
            for (var i = 0; i < pois.Count; i++)
            {
                var originPos = pois[i];
                var originName = originNames[i].Item1;
                var originCampType = originNames[i].Item2;
                var originNode = spatialGraphEnvironment.NearestNode(originPos);

                var timings = new List<DestinationPOCO>();

                for (var j = 0; j < pois.Count; j++)
                {
                    var destinationPos = pois[j];
                    var destinationNode = spatialGraphEnvironment.NearestNode(destinationPos);

                    if (originPos.Equals(destinationPos)) continue;

                    var destinationName = originNames[j].Item1;
                    var destinationCampType = originNames[j].Item2;

                    var route = spatialGraphEnvironment.FindRoute(originNode, destinationNode);
                    var edgeStops = route.Stops;

                    var tripTime = 0.0; // in seconds
                    var tripLength = route.RouteLength;

                    for (var k = 0; k < route.Count; k++)
                    {
                        var edge = edgeStops[k].Edge;
                        tripTime += edge.Length / edge.MaxSpeed;
                    }

                    var routeInfoPoco = new DestinationPOCO
                    {
                        Destination = destinationPos,
                        DestinationName = destinationName,
                        DestinationCampType = destinationCampType,
                        Duration = tripTime,
                        Length = tripLength
                    };

                    timings.Add(routeInfoPoco);
                }

                var originPoco = new OriginPOCO
                {
                    Origin = originPos,
                    OriginName = originName,
                    OriginCampType = originCampType,
                    RouteInfoList = timings
                };

                routeInfoList.Add(originPoco);
            }

            File.WriteAllText("./route_info_list.json", JsonSerializer.Serialize(routeInfoList));
        }
    }
}