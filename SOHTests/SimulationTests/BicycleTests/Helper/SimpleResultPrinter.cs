using System;
using System.Data;
using System.IO;
using System.Linq;
using Mars.Common.IO.Mapped.Collections;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using SOHDomain.Output;
using SOHMultimodalModel.Output.Trips;

namespace SOHTests.SimulationTests.BicycleTests.Helper
{
    public static class SimpleResultPrinter
    {
        public static void PrintResults(DataTable table, string fileName, int steps, int idNumber)
        {
            var longitude = table.Select("Tick >= 0");
            var id = longitude[idNumber]["ID"].ToString();

            var sortedRows = new TripPosition[longitude.Length];
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            foreach (var t in longitude)
                if (t["ID"].ToString() == id)
                {
                    var tick = Convert.ToInt32(t["Tick"]);

                    var time = Convert.ToDateTime(t["DateTime"]);
                    var diff = time.ToUniversalTime() - origin;
                    var timeInSeconds = Convert.ToInt32(diff.TotalSeconds);
                    sortedRows[tick] = new TripPosition(Convert.ToDouble(t["X"]),
                        Convert.ToDouble(t["Y"])) {UnixTimestamp = timeInSeconds};
                }

            var trip = new List<TripPosition>();

            for (var i = 0; i <= sortedRows.Length - steps; i = i + steps)
                if (sortedRows[i] != null)
                    trip.Add(sortedRows[i]);

            var writer = new GeoJsonWriter();
            var collection = new FeatureCollection();

            var jsonConverters =
                writer.SerializerSettings.Converters.Where(converter => converter is CoordinateConverter);
            foreach (var jsonConverter in jsonConverters) writer.SerializerSettings.Converters.Remove(jsonConverter);

            writer.SerializerSettings.Converters.Add(new TripPositionCoordinateConverter());
            if (trip.Count >= 2)
            {
                var lineString = new LineString(trip.ToArray());
                collection.Add(new Feature(lineString, new AttributesTable()));
            }

            File.WriteAllText(fileName + ".geojson", writer.Write(collection));
        }

        public static void PrintResults(DataTable table, string vehicleId, string fileName, int steps)
        {
            var longitude = table.Select("Tick >= 0 AND " +
                                         "StableId = '" + vehicleId + "'");
            var sortedRows = new TripPosition[longitude.Length];
            var origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            for (var i = 0; i < longitude.Length; i++)
            {
                var time = Convert.ToDateTime(longitude[i]["DateTime"]);
                var diff = time.ToUniversalTime() - origin;
                var timeInSeconds = Convert.ToInt32(diff.TotalSeconds);
                var tick = Convert.ToInt32(longitude[i]["Tick"]);
                sortedRows[tick] = new TripPosition(Convert.ToDouble(longitude[i]["0_Position"]),
                    Convert.ToDouble(longitude[i]["1_Position"])) {UnixTimestamp = timeInSeconds};
            }

            var trip = new List<TripPosition>();

            for (var i = 0; i <= sortedRows.Length - steps; i = i + steps)
                //                sortedRows[i].;
                trip.Add(sortedRows[i]);

            var writer = new GeoJsonWriter();
            var collection = new FeatureCollection();

            var jsonConverters =
                writer.SerializerSettings.Converters.Where(converter => converter is CoordinateConverter);
            foreach (var jsonConverter in jsonConverters) writer.SerializerSettings.Converters.Remove(jsonConverter);

            writer.SerializerSettings.Converters.Add(new TripPositionCoordinateConverter());
            if (trip.Count >= 2)
            {
                var lineString = new LineString(trip.ToArray());
                collection.Add(new Feature(lineString, new AttributesTable()));
            }

            File.WriteAllText(fileName + ".geojson", writer.Write(collection));
        }
    }
}