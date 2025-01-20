using System.Data;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using SOHModel.Ferry.Station;

namespace SOHModel.Ferry.Route;

/// <summary>
///     Provides the possibility to read a ferry route from a corresponding csv.
/// </summary>
public static class FerryRouteReader
{
    /// <summary>
    ///     Reads the input csv and builds a FerrySchedule by each line.
    /// </summary>
    /// <param name="file">Holds schedule and route information.</param>
    /// <param name="ferryStationLayer">Provides access to the ferry stations that are referenced in the csv.</param>
    /// <returns>A dictionary with line id to <see cref="FerryRoute" />.</returns>
    public static Dictionary<int, FerryRoute> Read(string file, FerryStationLayer ferryStationLayer)
    {
        var routes = new Dictionary<int, FerryRoute>();
        if (string.IsNullOrEmpty(file)) return routes;

        var dataTable = CsvReader.MapData(file);
        if (dataTable.Rows.Count < 2) return routes;

        ReadLines(ferryStationLayer, dataTable, routes);

        return routes;
    }

    private static void ReadLines(FerryStationLayer ferryStationLayer,
        DataTable dataTable, Dictionary<int, FerryRoute> routes)
    {
        FerryStation? startStation = null;
        foreach (DataRow row in dataTable.Rows)
        {
            if (row.ItemArray.Length <= 2) continue;

            int line = row[0].Value<int>();
            if (!routes.TryGetValue(line, out var route))
            {
                route = new FerryRoute();
                routes.Add(line, route);
                startStation = null;
            }

            string? stationId = row[1].Value<string>();
            var station = ferryStationLayer.Find(stationId);
            if (station == null) continue;

            int minutes = row[2].Value<int>();
            station.Lines.Add(line.Value<string>());

            if (startStation != null) route.Entries.Add(new FerryRouteEntry(startStation, station, minutes));

            startStation = station;
        }
    }
}