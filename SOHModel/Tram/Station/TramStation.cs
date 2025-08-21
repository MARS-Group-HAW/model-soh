using System.Collections.Concurrent;
using Mars.Common.Core;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using ServiceStack;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Tram.Model;

namespace SOHModel.Tram.Station;

public class TramStation: IVectorFeature
{
    private ConcurrentDictionary<Model.Tram, byte> _trams;

    /// <summary>
    ///     The centroid of this <see cref="TramStation" />.
    /// </summary>
    public Position Position { get; set; }

    /// <summary>
    ///     Identifies the tram station.
    /// </summary>
    public string Id { get; private set; }

    /// <summary>
    ///     Describes the tram station.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Provides all lines that visit this station.
    /// </summary>
    public ISet<string> Lines { get; private set; }

    /// <summary>
    ///     Provides all available trams at this station.
    /// </summary>
    public ConcurrentDictionary<Model.Tram, byte> Trams => _trams ??= new ConcurrentDictionary<Model.Tram, byte>();

    /// <summary>
    ///     Gets or sets the concrete feature data.
    /// </summary>
    public VectorStructuredData VectorStructured { get; set; }

    public void Init(ILayer layer, VectorStructuredData data)
    {
        Update(data);
    }

    /// <summary>
    ///     Initializes the <see cref="TramStation" /> with the feature information.
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
    ///     Enter the station with a tram and therefore provide the possibility to be entered by
    ///     <see cref="IPassengerCapable" />s.
    /// </summary>
    /// <param name="tram">The tram that is parked in this spot.</param>
    /// <returns>True if a parking spot could be found, false otherwise (Beware: the tram is not parked here).</returns>
    public bool Enter(Model.Tram tram)
    {
        tram.TramStation = this;
        return Trams.TryAdd(tram, byte.MinValue);
    }

    /// <summary>
    ///     Leave the station with given tram.
    /// </summary>
    /// <param name="tram">The tram that leaves this spot.</param>
    /// <returns>True if tram is not on this station any more.</returns>
    public bool Leave(Model.Tram tram)
    {
        var success = !Trams.ContainsKey(tram) || Trams.TryRemove(tram, out _);
        if (success) tram.TramStation = null;

        return success;
    }

    /// <summary>
    ///     Finds the next tram that is currently at this station and drives to given goal.
    /// </summary>
    /// <param name="goal">That a tram to use is reaching.</param>
    /// <returns>The next tram that drives to that goal, null if none found</returns>
    public Model.Tram Find(Position goal)
    {
        foreach (var tram in Trams.Keys)
            if (tram.Driver is TramDriver tramDriver)
                if (tramDriver.RemainingStations
                    .Any(entry => Distance.Haversine(entry.To.Position.PositionArray, goal.PositionArray) < 30))
                    return tram;

        return null;
    }
}