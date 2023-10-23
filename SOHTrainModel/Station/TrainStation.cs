using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.Core;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using ServiceStack;
using SOHDomain.Steering.Capables;
using SOHTrainModel.Model;

namespace SOHTrainModel.Station;

/// <summary>
///     The <code>TrainStationLayer</code> is located somewhere and can hold <code>Train</code>s up to its capacity
///     extent.
/// </summary>
public class TrainStation : IVectorFeature
{
    private ConcurrentDictionary<Train, byte> _trains;

    /// <summary>
    ///     The centroid of this <see cref="TrainStation" />.
    /// </summary>
    public Position Position { get; set; }

    /// <summary>
    ///     Identifies the train station.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    ///     Describes the train station.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Provides all lines that visit this station.
    /// </summary>
    public ISet<string> Lines { get; private set; }

    /// <summary>
    ///     Provides all available trains at this station.
    /// </summary>
    public ConcurrentDictionary<Train, byte> Trains => _trains ??= new ConcurrentDictionary<Train, byte>();

    /// <summary>
    ///     Gets or sets the concrete feature data.
    /// </summary>
    public VectorStructuredData VectorStructured { get; set; }

    public void Init(ILayer layer, VectorStructuredData data)
    {
        Update(data);
    }

    /// <summary>
    ///     Initializes the <see cref="TrainStation" /> with the feature information.
    /// </summary>
    public void Update(VectorStructuredData data)
    {
        VectorStructured = data;
        var centroid = VectorStructured.Geometry.Centroid;
        Position = Position.CreatePosition(centroid.X, centroid.Y);
        Id = VectorStructured.Data["id"].Value<string>();
        Name = VectorStructured.Data["short_name"].Value<string>();

        Lines = data.Data.ContainsKey("lines")
            ? data.Data["lines"].Value<string>().Split(',').ToSet()
            : new HashSet<string>();
    }

    /// <summary>
    ///     Enter the station with a train and therefore provide the possibility to be entered by
    ///     <see cref="IPassengerCapable" />s.
    /// </summary>
    /// <param name="train">The train that is parked in this spot.</param>
    /// <returns>True if a parking spot could be found, false otherwise (Beware: the train is not parked here).</returns>
    public bool Enter(Train train)
    {
        train.TrainStation = this;
        return Trains.TryAdd(train, byte.MinValue);
    }

    /// <summary>
    ///     Leave the station with given train.
    /// </summary>
    /// <param name="train">The train that leaves this spot.</param>
    /// <returns>True if train is not on this station any more.</returns>
    public bool Leave(Train train)
    {
        var success = !Trains.ContainsKey(train) || Trains.TryRemove(train, out _);
        if (success) train.TrainStation = null;

        return success;
    }

    /// <summary>
    ///     Finds the next train that is currently at this station and drives to given goal.
    /// </summary>
    /// <param name="goal">That a train to use is reaching.</param>
    /// <returns>The next train that drives to that goal, null if none found</returns>
    public Train Find(Position goal)
    {
        foreach (var train in Trains.Keys)
            if (train.Driver is TrainDriver trainDriver)
                if (trainDriver.RemainingStations
                    .Any(entry => Distance.Haversine(entry.To.Position.PositionArray, goal.PositionArray) < 30))
                    return train;

        return null;
    }
}