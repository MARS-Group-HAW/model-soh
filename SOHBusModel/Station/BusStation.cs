using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.Core;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using SOHBusModel.Model;
using SOHDomain.Steering.Capables;

namespace SOHBusModel.Station;

/// <summary>
///     The <code>BusStation</code> is located somewhere and can hold <code>Bus</code>s up to its capacity
///     extent.
/// </summary>
public class BusStation : IVectorFeature
{
    private ConcurrentDictionary<Bus, byte> _buses;

    /// <summary>
    ///     The centroid of this <see cref="BusStation" />.
    /// </summary>
    public Position Position { get; set; }

    /// <summary>
    ///     Identifies the bus station.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    ///     Describes the trabusin station.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Provides all lines that visit this station.
    /// </summary>
    public ISet<string> Lines { get; private set; }

    /// <summary>
    ///     Provides all available buses at this station.
    /// </summary>
    public ConcurrentDictionary<Bus, byte> Buses => _buses ??= new ConcurrentDictionary<Bus, byte>();

    /// <summary>
    ///     Gets or sets the concrete feature data.
    /// </summary>
    public VectorStructuredData VectorStructured { get; set; }

    public void Init(ILayer layer, VectorStructuredData data)
    {
        Update(data);
    }

    /// <summary>
    ///     Initializes the <see cref="BusStation" /> with the feature information.
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
    ///     Enter the station with a bus and therefore provide the possibility to be entered by
    ///     <see cref="IPassengerCapable" />s.
    /// </summary>
    /// <param name="bus">The bus that is parked in this spot.</param>
    /// <returns>True if a parking spot could be found, false otherwise (Beware: the bus is not parked here).</returns>
    public bool Enter(Bus bus)
    {
        bus.BusStation = this;
        return Buses.TryAdd(bus, byte.MinValue);
    }

    /// <summary>
    ///     Leave the station with given bus.
    /// </summary>
    /// <param name="bus">The bus that leaves this spot.</param>
    /// <returns>True if bus is not on this station any more.</returns>
    public bool Leave(Bus bus)
    {
        var success = !Buses.ContainsKey(bus) || Buses.TryRemove(bus, out _);
        if (success) bus.BusStation = null;

        return success;
    }

    /// <summary>
    ///     Finds the next bus that is currently at this station and drives to given goal.
    /// </summary>
    /// <param name="goal">That a bus to use is reaching.</param>
    /// <returns>The next bus that drives to that goal, null if none found</returns>
    public Bus Find(Position goal)
    {
        foreach (var train in Buses.Keys)
            if (train.Driver is BusDriver trainDriver)
                if (trainDriver.RemainingStations
                    .Any(entry => Distance.Haversine(entry.To.Position.PositionArray, goal.PositionArray) < 30))
                    return train;

        return null;
    }
}