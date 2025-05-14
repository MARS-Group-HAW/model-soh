using System.Data;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using SOHModel.Train.Station;

namespace SOHModel.Train.Route;

/// <summary>
///     Provides the possibility to read a train route from a corresponding csv.
/// </summary>
public static class TrainRouteReader
{
    /// <summary>
    ///     Reads the input csv and builds a TrainSchedule by line.
    /// </summary>
    /// <param name="file">Holds schedule and route information.</param>
    /// <param name="trainStationLayer">Provides access to the train stations that are referenced in the csv.</param>
    /// <returns>A dictionary with line id to <see cref="TrainRoute" />.</returns>
    public static Dictionary<string, TrainRoute> Read(string file, TrainStationLayer trainStationLayer)
    {
        var routes = new Dictionary<string, TrainRoute>();

        if (string.IsNullOrEmpty(file)) return routes;
        var dataTable = CsvReader.MapData(file);

        if (dataTable.Rows.Count < 2) return routes;

        ReadLines(trainStationLayer, dataTable, routes);

        return routes;
    }

    private static void ReadLines(
        TrainStationLayer trainStationLayer,
        DataTable dataTable,
        Dictionary<string, TrainRoute> routes)
    {
        TrainStation? startStation = null;
        foreach (DataRow row in dataTable.Rows)
        {
            if (row.ItemArray.Length <= 2) continue;

            string? line = row[0].Value<string>();
            if (!routes.TryGetValue(line, out var route))
            {
                route = new TrainRoute();
                routes.Add(line, route);
                startStation = null;
            }

            string? stationId = row[1].Value<string>();
            var station = trainStationLayer.Find(stationId);
            if (station == null) continue;

            int minutes = row[2].Value<int>();
            station.Lines.Add(line.Value<string>());

            if (startStation != null) route.Entries.Add(new TrainRouteEntry(startStation, station, minutes));

            startStation = station;
        }
    }
}