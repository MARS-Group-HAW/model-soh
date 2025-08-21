using System.Data;
using Mars.Common.Core;
using Mars.Common.IO.Csv;
using SOHModel.Tram.Station;
namespace SOHModel.Tram.Route;

public class TramRouteReader
{
    /// <summary>
    ///     Reads the input csv and builds a TramSchedule by line.
    /// </summary>
    /// <param name="file">Holds schedule and route information.</param>
    /// <param name="tramStationLayer">Provides access to the tram stations that are referenced in the csv.</param>
    /// <returns>A dictionary with line id to <see cref="TramRoute" />.</returns>
    public static Dictionary<string, TramRoute> Read(string file, TramStationLayer tramStationLayer)
    {
        var routes = new Dictionary<string, TramRoute>();

        if (string.IsNullOrEmpty(file)) return routes;
        var dataTable = CsvReader.MapData(file);

        if (dataTable.Rows.Count < 2) return routes;

        ReadLines(tramStationLayer, dataTable, routes);

        return routes;
    }

    private static void ReadLines(
        TramStationLayer tramStationLayer,
        DataTable dataTable,
        Dictionary<string, TramRoute> routes)
    {
        TramStation? startStation = null;
        foreach (DataRow row in dataTable.Rows)
        {
            if (row.ItemArray.Length <= 2) continue;

            string? line = row[0].Value<string>();
            if (!routes.TryGetValue(line, out var route))
            {
                route = new TramRoute();
                routes.Add(line, route);
                startStation = null;
            }

            string? stationId = row[1].Value<string>();
            var station = tramStationLayer.Find(stationId);
            if (station == null) continue;

            int minutes = row[2].Value<int>();
            station.Lines.Add(line.Value<string>());

            if (startStation != null) route.Entries.Add(new TramRouteEntry(startStation, station, minutes));

            startStation = station;
        }
    }
}