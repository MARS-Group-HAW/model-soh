using System;
using System.Collections.Generic;
using Mars.Core.Data.Wrapper.Memory;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;
using Newtonsoft.Json;

namespace SOHMultimodalModel.Output.Trips;

public class TripPositionCoordinateConverter : CoordinateConverter
{
    /// <summary>
    ///     Predicate function to check if an instance of <paramref name="objectType" /> can be converted using this converter.
    /// </summary>
    /// <param name="objectType">The type of the object to convert</param>
    /// <returns>
    ///     <value>true</value>
    ///     if the conversion is possible, otherwise
    ///     <value>false</value>
    /// </returns>
    public override bool CanConvert(Type objectType)
    {
        if (!(objectType == typeof(Coordinate)) && !(objectType == typeof(Coordinate[])) &&
            !(objectType == typeof(List<Coordinate[]>)) && !(objectType == typeof(List<List<Coordinate[]>>)) &&
            !typeof(IEnumerable<Coordinate>).IsAssignableFrom(objectType) &&
            !typeof(IEnumerable<IEnumerable<Coordinate>>).IsAssignableFrom(objectType))
            return typeof(IEnumerable<IEnumerable<IEnumerable<Coordinate>>>).IsAssignableFrom(objectType);
        return true;
    }

    /// <summary>
    ///     Writes a coordinate, a coordinate sequence or an enumeration of coordinates to JSON
    /// </summary>
    /// <param name="writer">The writer</param>
    /// <param name="value">The coordinate</param>
    /// <param name="serializer">The serializer</param>
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        switch (value)
        {
            case null:
                writer.WriteToken(JsonToken.Null);
                break;
            case List<List<Coordinate[]>> coordinates1:
                WriteJsonCoordinatesEnumerable2(writer, coordinates1, serializer);
                break;
            case List<Coordinate[]> coordinateArrayList:
                WriteJsonCoordinatesEnumerable(writer, coordinateArrayList, serializer);
                break;
            case IEnumerable<Coordinate> coordinates2:
                WriteJsonCoordinates(writer, coordinates2, serializer);
                break;
            default:
            {
                if (!(value is Coordinate coordinate))
                    return;
                WriteJsonCoordinate(writer, coordinate, serializer);
                break;
            }
        }
    }

    /// <summary>Writes a single coordinate to JSON</summary>
    /// <param name="writer">The writer</param>
    /// <param name="coordinate">The coordinate</param>
    /// <param name="serializer">The serializer</param>
    protected static void WriteJsonCoordinate(JsonWriter writer, Coordinate coordinate, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(coordinate.X);
        writer.WriteValue(coordinate.Y);
        writer.WriteValue(0);
        if (coordinate is TripPosition tripPosition)
            writer.WriteValue(tripPosition.UnixTimestamp);
        writer.WriteEndArray();
    }

    private static void WriteJsonCoordinates(JsonWriter writer, IEnumerable<Coordinate> coordinates,
        JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var coordinate in coordinates)
            WriteJsonCoordinate(writer, coordinate, serializer);
        writer.WriteEndArray();
    }

    private void WriteJsonCoordinatesEnumerable(JsonWriter writer, IEnumerable<Coordinate[]> coordinates,
        JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var coordinate in coordinates)
            WriteJsonCoordinates(writer, coordinate, serializer);
        writer.WriteEndArray();
    }

    private void WriteJsonCoordinatesEnumerable2(JsonWriter writer, IEnumerable<List<Coordinate[]>> coordinates,
        JsonSerializer serializer)
    {
        writer.WriteStartArray();
        foreach (var coordinate in coordinates)
            WriteJsonCoordinatesEnumerable(writer, coordinate, serializer);
        writer.WriteEndArray();
    }
}