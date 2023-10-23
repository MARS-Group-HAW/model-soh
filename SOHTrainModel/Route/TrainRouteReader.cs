using System.Collections.Generic;
using System.Data;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using SOHTrainModel.Station;

namespace SOHTrainModel.Route;

/// <summary>
///     Provides the possibility to read a train route from a corresponding csv.
/// </summary>
public static class TrainRouteReader
{
    /// <summary>
    ///     Reads the input csv and builds a TrainSchedule by line
    /// </summary>
    /// <param name="file">Holds schedule and route information.</param>
    /// <param name="trainStationLayer">Provides access to the train stations that are referenced in the csv.</param>
    /// <returns>A dictionary with line id to <see cref="TrainRoute" />.</returns>
    public static Dictionary<string, TrainRoute> Read(string file, TrainStationLayer trainStationLayer)
    {
        var routes = new Dictionary<string, TrainRoute>();

        var dataTable = CsvReader.MapData(file);

        if (dataTable.Rows.Count < 2) return routes;

        TrainStation startStation = null;
        foreach (DataRow row in dataTable.Rows)
        {
            if (row.ItemArray.Length <= 2) continue;

            var line = row[0].Value<string>();
            if (!routes.ContainsKey(line))
            {
                routes.Add(line, new TrainRoute());
                startStation = null;
            }

            var route = routes[line];
            var stationId = row[1].Value<string>();
            var station = trainStationLayer.Find(stationId);
            if (station == null) continue;

            var minutes = row[2].Value<int>();
            station.Lines.Add(line.Value<string>());

            if (startStation != null) route.Entries.Add(new TrainRouteEntry(startStation, station, minutes));

            startStation = station;
        }

        return routes;
    }
}