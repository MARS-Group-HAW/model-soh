using Mars.Common.Core.Collections;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SensorThings.Client;
using SensorThings.Core;
using SensorThings.OData;
using Location = SensorThings.Core.Location;

namespace SOHBicycleRealTime;

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

        var harburg = envelopeInput ?? new Envelope(9.9354367, 10.0275171, 53.4219923, 53.4700337);
        var from = fromInput ?? new DateTime(2024, 11, 04, 15, 35, 0);
        var to = toInput ?? new DateTime(2024, 11, 04, 16, 05, 0);

        Console.WriteLine(
            $"Retrieving initial data from sensor-network for time range [{from.ToUniversalTime()}...{to.ToUniversalTime()} ...");
        Console.WriteLine($"for the area [{harburg}]");
        //var features = GetObservations(harburg, from, to, "StadtRad", out var observations);
        var features = GetObservations(harburg, from, to, 
            "HH_STA_traffic_lights", out var observations);
        
        var geojson = new GeoJsonWriter().Write(features);
        File.WriteAllText("traffic_signals_all_april_2024.geojson", geojson);
        Console.WriteLine($"Observation count: {observations.Count}");

        Console.WriteLine("... done");


        // var topics = datastreams.Select(observation => $"v1.1/Datastreams({observation.Id})/Observations")
        //     .ToArray();
        //
        // foreach (var topic in topics) Console.WriteLine(topic);
        // var qos = Enumerable.Range(0, datastreams.Count).Select(i => MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE).ToArray();
        //
        // var client = new MqttClient(MqttHost);
        // var code = client.Connect(Guid.NewGuid().ToString());
        //
        // //ushort msgId = client.Subscribe(new string[] { "v1.1/Observations" }, new byte[] { MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE});
        // var msgId = client.Subscribe(topics, qos);
        // client.MqttMsgPublishReceived += ClientMqttMsgPublishReceived;
        return features;
    }

    // private static void ClientMqttMsgPublishReceived(object sender, MqttMsgPublishEventArgs e)
    // {
    //     var str = Encoding.Default.GetString(e.Message);
    //     // var observation = JsonConvert.DeserializeObject<Observation>(str);
    //     // var stream = observation.GetDatastream(Client).Result.Result;
    //
    //
    //     Console.WriteLine(str);
    //     Console.WriteLine();
    //     // Console.WriteLine(stream.Description);
    //     // Console.WriteLine(
    //     //     $"{stream.GetObservedProperty(Client).Result.Result.Name} -> {observation.Id} published at {observation.PhenomenonTime.Start}: {observation.Result}");
    //     // Console.WriteLine();
    //
    //
    //     // Add Catalog Append (Message.PhenomeononTime, Message)
    //     // 
    //     // Remove Catalog Append (Catalog.LastAddTemporalRef, Message)
    // }

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
            geometryWkt,
            allElementsOfThePast));

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
            
            // QueryFilter = new QueryFilter($"substringof(trim('{dataStreamTopic}'), serviceName)"),
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

                // var interest = observation.FeatureOfInterest;
                // var interestFeature = Reader.Read<Feature>(interest.Feature.ToString());
                // interestFeature.Data = new AttributesTable
                // {
                //     {"type", "featureOfInterest"},
                //     {"name", interest.Name}, 
                //     {"interestDescription", interest.Description},
                //     {"interestId", interest.Id}
                // };
                // collection.Add(interestFeature);
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