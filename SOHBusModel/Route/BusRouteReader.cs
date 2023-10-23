using System.Collections.Generic;
using System.Data;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using SOHBusModel.Station;

namespace SOHBusModel.Route;

/// <summary>
///     Provides the possibility to read a bus route from a corresponding csv.
/// </summary>
public static class BusRouteReader
{
    /// <summary>
    ///     Reads the input csv and builds a bus schedule by line
    /// </summary>
    /// <param name="file">Holds schedule and route information.</param>
    /// <param name="busStationLayer">Provides access to the bus stations that are referenced in the csv.</param>
    /// <returns>A dictionary with line id to <see cref="BusRoute" />.</returns>
    public static Dictionary<string, BusRoute> Read(string file, BusStationLayer busStationLayer)
    {
        var routes = new Dictionary<string, BusRoute>();

        var dataTable = CsvReader.MapData(file);

        if (dataTable.Rows.Count < 2) return routes;

        BusStation startStation = null;
        foreach (DataRow row in dataTable.Rows)
        {
            if (row.ItemArray.Length <= 2) continue;

            var line = row[0].Value<string>();
            if (!routes.ContainsKey(line))
            {
                routes.Add(line, new BusRoute());
                startStation = null;
            }

            var route = routes[line];
            var stationId = row[1].Value<string>();
            var station = busStationLayer.Find(stationId);
            if (station == null) continue;

            var minutes = row[2].Value<int>();
            station.Lines.Add(line.Value<string>());

            if (startStation != null) route.Entries.Add(new BusRouteEntry(startStation, station, minutes));

            startStation = station;
        }

        return routes;
    }
}