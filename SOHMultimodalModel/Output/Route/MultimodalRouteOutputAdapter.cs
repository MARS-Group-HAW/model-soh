using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Mars.Common;
using Mars.Interfaces.Environments;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace SOHMultimodalModel.Output.Route;

public static class MultimodalRouteOutputAdapter
{
    private static readonly ReaderWriterLockSlim Lock = new();

    public static string Suffix { get; set; }


    /// <summary>
    ///     Converts the multimodal route into a set of individual features and serialize it into valid geojson.
    ///     Then writes this geojson into a file.
    /// </summary>
    /// <param name="multimodalRoute">The multi modal route containing one or multiple routes.</param>
    /// <param name="separateFilesPerRoute">Can be marked if for every route a single file should be created.</param>
    /// <returns></returns>
    public static void PrintLineString(MultimodalRoute multimodalRoute, bool separateFilesPerRoute = false)
    {
        if (!multimodalRoute.Any() || !multimodalRoute.Last().Route.Any()) return;
        var collection = new FeatureCollection();

        var index = 0;
        foreach (var multimodalStop in multimodalRoute.Stops)
        {
            var route = multimodalStop.Route;
            var currentModalType = multimodalStop.ModalChoice;
            var routeCollection = new FeatureCollection();

            foreach (var stop in route.Stops)
            {
                var featureAttributes = new Dictionary<string, object>(stop.Edge.Attributes)
                {
                    { "modality", currentModalType.ToString() }
                };
                featureAttributes.Remove("geometry");
                var edgeGeometry = stop.Edge.Geometry;
                var geometry = new LineString(edgeGeometry.Select(position => position.ToCoordinate()).ToArray());
                var feature = new Feature(geometry, new AttributesTable(featureAttributes));
                collection.Add(feature);
                routeCollection.Add(feature);
            }

            if (separateFilesPerRoute)
            {
                var routeGeojson = new GeoJsonWriter().Write(routeCollection);
                Lock.EnterWriteLock();
                File.WriteAllText("trips_" + Suffix + index++ + "route.geojson", routeGeojson);
                Lock.ExitWriteLock();
            }
        }

        var fileName = $"trips_route{Suffix ?? ""}_{DateTime.Now:yy-MMM-ddTHH-mm-ss}.geojson";
        var geojson = new GeoJsonWriter().Write(collection);
        Lock.EnterWriteLock();
        File.WriteAllText(fileName, geojson);
        Lock.ExitWriteLock();
    }
}