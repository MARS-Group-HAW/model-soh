using System.Data;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using SOHModel.Bus.Station;

namespace SOHModel.Bus.Route;

/// <summary>
///     Provides the possibility to read a bus route from a corresponding csv.
/// </summary>
public static class BusRouteReader
{
    /// <summary>
    ///     Reads the input csv and builds a bus schedule by line.
    /// </summary>
    /// <param name="file">Holds schedule and route information.</param>
    /// <param name="busStationLayer">Provides access to the bus stations that are referenced in the csv.</param>
    /// <returns>A dictionary with line id to <see cref="BusRoute" />.</returns>
    public static Dictionary<string, BusRoute> Read(string file, BusStationLayer busStationLayer)
    {
        var routes = new Dictionary<string, BusRoute>();

        if (string.IsNullOrEmpty(file)) return routes;

        var dataTable = CsvReader.MapData(file);

        if (dataTable.Rows.Count < 2) return routes;

        ReadLines(busStationLayer, dataTable, routes);

        return routes;
    }

    private static void ReadLines(BusStationLayer busStationLayer,
        DataTable dataTable,
        Dictionary<string, BusRoute> routes)
    {
        BusStation? startStation = null;
        foreach (DataRow row in dataTable.Rows)
        {
            if (row.ItemArray.Length <= 2) continue;

            string? line = row[0].Value<string>();
            if (!routes.TryGetValue(line, out var route))
            {
                route = new BusRoute();
                routes.Add(line, route);
                startStation = null;
            }

            string? stationId = row[1].Value<string>();
            var station = busStationLayer.Find(stationId);
            if (station == null) continue;

            int minutes = row[2].Value<int>();
            station.Lines.Add(line.Value<string>());

            if (startStation != null) route.Entries.Add(new BusRouteEntry(startStation, station, minutes));

            startStation = station;
        }
    }
}