using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetTopologySuite.Features;
using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;

namespace SOHMultimodalModel.Output.Trips;

/// <summary>
///     Provides the possibility to save the trips of given agents in a trips.json-file
/// </summary>
public static class TripsOutputAdapter
{
    private const string FileName = "{0}{1}.geojson";

    /// <summary>
    ///     Give the created files a different name.
    /// </summary>
    public static string FileNamePrefix { get; set; } = "trips";

    /// <summary>
    ///     Give the created file a different Suffix (e.g. trips_cars.geojson instead of trips.geojson)
    /// </summary>
    public static string FileNameSuffix { get; set; } = null;

    /// <summary>
    ///     The given agents have stored their trip as a list of trip positions.
    ///     These are converted and stored in a geojson-file.
    /// </summary>
    /// <param name="agents">Hold all trip information.</param>
    public static void PrintTripResult(params ITripSavingAgent[] agents)
    {
        PrintTripResult(agents.ToList());
    }

    /// <summary>
    ///     The given agents have stored their trip as a list of trip positions.
    ///     These are converted and stored in a geojson-file.
    /// </summary>
    /// <param name="agents">Hold all trip information.</param>
    public static void PrintTripResult(IEnumerable<ITripSavingAgent> agents)
    {
        var writer = new GeoJsonWriter();
        var collection = new FeatureCollection();

        var jsonConverters =
            writer.SerializerSettings.Converters
                .Where(converter => converter is CoordinateConverter || converter is GeometryConverter);
        foreach (var jsonConverter in jsonConverters) writer.SerializerSettings.Converters.Remove(jsonConverter);
        writer.SerializerSettings.Converters.Add(new TripPositionCoordinateConverter());
        writer.SerializerSettings.Converters.Add(new TripsLineConverter());

        foreach (var agent in agents)
        {
            if (agent.TripsCollection == null) continue;
            foreach (var (modalType, trip) in agent.TripsCollection.Result)
                if (trip != null && trip.Count >= 2)
                {
                    var path = new TripsLine(trip.ToArray());
                    var f = new Feature(path, new AttributesTable(
                        new Dictionary<string, object>
                        {
                            { "creation_id", agent.StableId.ToString() },
                            { "agent_type", agent.GetType().Name },
                            { "modal_type", modalType?.ToString() }
                        }));
                    collection.Add(f);
                }
        }

        var fileName = string.Format(FileName, FileNamePrefix ?? "", FileNameSuffix ?? "");
        File.WriteAllText(fileName, writer.Write(collection));
    }
}