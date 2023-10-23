using NetTopologySuite.IO;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;

namespace SOHMultimodalModel.Output.Trips;

/// <summary>
///     A <see cref="GeometryConverter" /> that can handle <see cref="TripsLine" />s.
/// </summary>
public class TripsLineConverter : GeometryConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        if (value is TripsLine tripsLine)
        {
            writer.WriteStartObject();
            writer.WritePropertyName("type");
            writer.WriteValue(nameof(GeoJsonObjectType.LineString));
            writer.WritePropertyName("coordinates");
            serializer.Serialize(writer, tripsLine.Coordinates);
            writer.WriteEndObject();
        }
        else
        {
            base.WriteJson(writer, value, serializer);
        }
    }
}