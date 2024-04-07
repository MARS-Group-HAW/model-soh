using System.Collections.Concurrent;
using Mars.Common.Core;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Ferry.Model;

namespace SOHModel.Ferry.Station;

/// <summary>
///     The <code>FerryStationLayer</code> is located somewhere and can hold <code>IParkingCar</code>s up to its capacity
///     extent.
/// </summary>
public class FerryStation : IVectorFeature
{
    private ConcurrentDictionary<Model.Ferry, byte>? _ferries;

    /// <summary>
    ///     The centroid of this <see cref="FerryStation" />.
    /// </summary>
    public Position Position { get; set; }

    /// <summary>
    ///     Identifies the ferry station.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    ///     Describes the ferry station.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Provides all lines that visit this station.
    /// </summary>
    public ISet<string> Lines { get; private set; }

    /// <summary>
    ///     Provides all available ferries at this station.
    /// </summary>
    public ConcurrentDictionary<Model.Ferry, byte> Ferries => 
        _ferries ??= new ConcurrentDictionary<Model.Ferry, byte>();

    /// <summary>
    ///     Gets or sets the concrete feature data.
    /// </summary>
    public VectorStructuredData VectorStructured { get; set; } = default!;

    public void Init(ILayer layer, VectorStructuredData data)
    {
        Update(data);
    }

    /// <summary>
    ///     Initializes the <see cref="FerryStation" /> with the feature information.
    /// </summary>
    public void Update(VectorStructuredData data)
    {
        VectorStructured = data;
        var centroid = VectorStructured.Geometry.Centroid;
        Position = Position.CreatePosition(centroid.X, centroid.Y);
        Id = VectorStructured.Data["id"].Value<string>();
        Name = VectorStructured.Data["short_name"].Value<string>();

        // Lines = feature.Data["lines"].Value<string>().Split(',').ToHashSet();
        Lines = new HashSet<string>();
    }

    /// <summary>
    ///     Enter the station with a ferry and therefore provide the possibility to be entered by
    ///     <see cref="IPassengerCapable" />s.
    /// </summary>
    /// <param name="ferry">The ferry that is parked in this spot.</param>
    /// <returns>True if a parking spot could be found, false otherwise (Beware: the ferry is not parked here).</returns>
    public bool Enter(Model.Ferry ferry)
    {
        ferry.FerryStation = this;
        return Ferries.TryAdd(ferry, byte.MinValue);
    }

    /// <summary>
    ///     Leave the station with given ferry.
    /// </summary>
    /// <param name="ferry">The ferry that leaves this spot.</param>
    /// <returns>True if ferry is not on this station any more.</returns>
    public bool Leave(Model.Ferry ferry)
    {
        var success = !Ferries.ContainsKey(ferry) || Ferries.TryRemove(ferry, out _);
        if (success) ferry.FerryStation = null;

        return success;
    }

    /// <summary>
    ///     Finds the next ferry that is currently at this station and drives to given goal.
    /// </summary>
    /// <param name="goal">That a ferry to use is reaching.</param>
    /// <returns>The next ferry that drives to that goal, null if none found</returns>
    public Model.Ferry Find(Position goal)
    {
        foreach (var ferry in Ferries.Keys)
            if (ferry.Driver is FerryDriver ferryDriver)
                if (ferryDriver.RemainingStations
                    .Any(entry => Distance.Haversine(entry.To.Position.PositionArray, goal.PositionArray) < 30))
                    return ferry;

        return null;
    }
}