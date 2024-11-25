using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mars.Common.Core.Collections;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SensorThings.Client;
using SensorThings.Core;
using SensorThings.OData;
using Location = SensorThings.Core.Location;
using Mars.Common;

namespace SOHC_ITSBox
{
    public static class SensorThingsImporter
    {
        private const string Host = "https://tld.iot.hamburg.de/v1.1";
        private static SensorThingsClient _client;
        private static readonly GeoJsonReader Reader = new();

        public static FeatureCollection LoadData(
            DateTime? fromInput = null, DateTime? toInput = null, Envelope envelopeInput = null)
        {
            _client = new SensorThingsClient(Host);

            Console.WriteLine("Retrieving initial data from sensor-network for initial phase ...");

            var c_itsArea = new Envelope(9.96865453546937, 9.97134403, 53.541856109114335, 53.56802054142237);

            // Using very high precision (15) for maximum detail
            var geoHashes = GetGeoHashBboxes(c_itsArea, 5);  //  fine-grained precision

            var from = fromInput ?? new DateTime(2024, 11, 18, 11, 0, 0);
            var to = toInput ?? new DateTime(2024, 11, 18, 11, 15, 0);

            Console.WriteLine($"Retrieving initial data from sensor-network for time range [{from.ToUniversalTime()}...{to.ToUniversalTime()} ...");
            Console.WriteLine($"for the area [{c_itsArea}]");

            var features = new FeatureCollection();

            foreach (var geoHash in geoHashes)
            {
                Console.WriteLine($"Processing GeoHash: {geoHash}");
                var geoCoordinate = GeoHash.DecodeBbox(geoHash);
                
                var minLon = geoCoordinate.MinX;
                var maxLon = geoCoordinate.MaxX;
                var minLat = geoCoordinate.MinY;
                var maxLat = geoCoordinate.MaxY;

                var geoEnvelope = new Envelope(minLon, maxLon, minLat, maxLat);
                var featureCollection = GetObservations(geoEnvelope, from, to, "HH_STA_traffic_lights", out var observations);
                features.AddRange(featureCollection);
            }

            //Console.WriteLine($"Number of features: {features.Count}");
            if (true ) //features.Count > 0)
            {
                var geojson = new GeoJsonWriter().Write(features);
                File.WriteAllText("traffic_signals.geojson", geojson);
                Console.WriteLine("GeoJSON file has been written.");
            }
            else
            {
                Console.WriteLine("No features to write to the file.");
            }


            return features;
        }
        private static List<string> GetGeoHashBboxes(Envelope envelope, int precision)
        {
            var geoHashes = new List<string>();
            var minLat = envelope.MinY;
            var maxLat = envelope.MaxY;
            var minLon = envelope.MinX;
            var maxLon = envelope.MaxX;

            var latStep = (maxLat - minLat) / 10;
            var lonStep = (maxLon - minLon) / 10;

            for (var lat = minLat; lat < maxLat; lat += latStep)
            {
                for (var lon = minLon; lon < maxLon; lon += lonStep)
                {
                    var geoHash = GeoHash.Encode(lat, lon, precision);
                    geoHashes.Add(geoHash);
                }
            }

            return geoHashes;
        }

        private static FeatureCollection GetObservations(Envelope window, DateTime from, DateTime to,
            string dataStreamTopic, out List<Datastream> observations)
        {
            if (window == null)
            {
                observations = new List<Datastream>();
                return null;
            }

            var geometryWkt = new WKTWriter().Write(ToGeometry(window));

            var temporalQuery =
                $"phenomenonTime gt {from:yyyy-MM-ddTHH:mm:ssZ} and phenomenonTime le {to:yyyy-MM-ddTHH:mm:ssZ}";

            var interestQuery = new OdataQuery
            {
                QueryFilter = new QueryFilter(temporalQuery),
                QueryExpand = new QueryExpand(new[]
                {
                    new Expand(new[] { "FeatureOfInterest" })
                })
            };

            var allElementsOfThePast = new OdataQuery
            {
                QueryFilter = new QueryFilter($"phenomenonTime lt {from:yyyy-MM-ddTHH:mm:ssZ}"),
                QueryOrderBy = new QueryOrderBy(new Dictionary<string, OrderType>
                {
                    { "phenomenonTime", OrderType.Descending }
                }),
                QueryExpand = new QueryExpand(new[]
                {
                    new Expand(new[] { "FeatureOfInterest" })
                })
            };

            var result =
                GetObservationsFeatureCollection(dataStreamTopic, out observations, geometryWkt, interestQuery);

            var collection = GroupByFirst(result, GetObservationsFeatureCollection(dataStreamTopic, out observations,
                geometryWkt, allElementsOfThePast));

            return collection;
        }

        private static FeatureCollection GroupByFirst(FeatureCollection result,
            FeatureCollection getObservationsFeatureCollection)
        {
            foreach (var grouping in getObservationsFeatureCollection.GroupBy(feature => feature.Attributes["thingId"]))
            {
                var lastValidFeature = grouping.FirstOrDefault();
                if (lastValidFeature != null) result.Add(lastValidFeature);
            }

            return result;
        }

        private static FeatureCollection GetObservationsFeatureCollection(string dataStreamTopic,
            out List<Datastream> observationTopics,
            string geometryWkt, params OdataQuery[] observationTemporalQuery)
        {
            var observationExpand = observationTemporalQuery
                .WhereNotNull()
                .Select(query => new Expand(new[] { "Observations" }, query))
                .Append(new Expand(new[] { "ObservedProperty" })).ToArray();

            var propertyQuery = new OdataQuery
            {
                QueryExpand = new QueryExpand(observationExpand)
            };
            var dataStreamQuery = new OdataQuery
            {
                QueryExpand = new QueryExpand(new[]
                {
                    new Expand(new[] { "Datastreams" }, propertyQuery)
                })
            };

            var filterString = $"geo.intersects(location, geography'{geometryWkt}')";
            var filterQuery = new OdataQuery
            {
                QueryFilter = new QueryFilter(filterString),
                QueryExpand = new QueryExpand(new[]
                {
                    new Expand(new[] { "Things" }, dataStreamQuery)
                })
            };

            var locationResponse = _client.GetLocationCollection(filterQuery).Result;
            
            var collection = new FeatureCollection();
            if (!locationResponse.Success)
            {
                Console.WriteLine("no success. GAME OVER!");
                observationTopics = new List<Datastream>();
                return collection;
            }
            
            observationTopics = new List<Datastream>();

            foreach (var location in locationResponse.Result.Items)
            {
                foreach (var thing in location.Things)
                foreach (var resultItem in thing.Datastreams)
                {
                    observationTopics.Add(resultItem);
                    var observations = resultItem.Observations;
                    foreach (var observation in observations.Where(observation => observation.Result != null))
                    {
                        var feature = CreateFeature(location, resultItem, thing, observation);
                        collection.Add(feature);
                    }
                }

                Console.WriteLine(location.Name);
                Console.WriteLine("##############################");
            }

            return collection;
        }

        private static IFeature CreateFeature(Location location, Datastream resultItem, Thing thing,
            Observation observation)
        {
            var feature = Reader.Read<Feature>(location.Feature.ToString());
            feature.Attributes = new AttributesTable();
            feature.Attributes.Add("type", "observation");
            feature.Attributes.Add("locationId", location.Id);
            feature.Attributes.Add("name", location.Name);
            feature.Attributes.Add("dataDescription", resultItem.Description);
            feature.Attributes.Add("streamId", resultItem.Id);
            feature.Attributes.Add("propertyDefinition", resultItem.ObservedProperty.Definition);
            feature.Attributes.Add("thingDescription", thing.Description);
            feature.Attributes.Add("thingId", thing.Id);
            feature.Attributes.Add("time", observation.PhenomenonTime.Start);
            feature.Attributes.Add(resultItem.ObservedProperty.Name, observation.Result);
            return feature;
        }

        private static Geometry ToGeometry(Envelope env)
        {
            if (env == null || !(env.MinX <= env.MaxX) || !(env.MinY <= env.MaxY)) return null;

            var bounds = new[]
            {
                new Coordinate(env.MinX, env.MinY),
                new Coordinate(env.MaxX, env.MinY),
                new Coordinate(env.MaxX, env.MaxY),
                new Coordinate(env.MinX, env.MaxY),
                new Coordinate(env.MinX, env.MinY)
            };

            var ring = new LinearRing(bounds);
            return new Polygon(ring);
        }
    }
}
