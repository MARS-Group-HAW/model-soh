using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Mars.Common.Core;
using Mars.Components.Layers.Temporal;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHDomain.Graph;

namespace SOHBicycleModel.Rental;

/// <summary>
///     The <code>BicycleRentalStation</code> is located somewhere and can hold <code>IRentalBicycle</code>s
///     for rental.
/// </summary>
public class BicycleRentalStation : IVectorFeature, IQueryFieldProvider
{
    private const string SyncKey = "SyncDifferenz";
    public const int StandardAmount = 10;
    private static readonly (string, string) BicycleType = ("type", "city");

    private ConcurrentDictionary<IRentalBicycle, byte> _parkingBicycles;
    private string KeyCount => Layer?.KeyCount;

    /// <summary>
    ///     The centroid of this rental station.
    /// </summary>
    public Position Position { get; private set; }

    /// <summary>
    ///     The name of this (location of) rental station.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    ///     Holds created and/or at this station located bicycles.
    /// </summary>
    private ConcurrentDictionary<IRentalBicycle, byte> ParkingBicycles =>
        _parkingBicycles ??= new ConcurrentDictionary<IRentalBicycle, byte>();

    /// <summary>
    ///     Indicates that there are no available bicycles at this rental station.
    /// </summary>
    public bool Empty => Count == 0;

    /// <summary>
    ///     Provides the amount of currently available bicycles at this station.
    /// </summary>
    public int Count => ParkingBicycles.Count;

    /// <summary>
    ///     Only stores the most recent update count for temporal updates.
    /// </summary>
    private int? LastUpdateCount { get; set; }

    private BicycleRentalLayer Layer { get; set; }

    public object GetValue(string field)
    {
        return field == "DataStreamId" ? VectorStructured.Data["streamId"] : null;
    }

    public VectorStructuredData VectorStructured { get; set; }

    public void Init(ILayer layer, VectorStructuredData data)
    {
        Layer = (BicycleRentalLayer)layer;
        Init(data);
    }

    /// <summary>
    ///     Initializes the <code>BicycleRentalStation</code> with the feature information.
    /// </summary>
    public void Update(VectorStructuredData data)
    {
        if (!Layer.IsInitialized)
            Init(data);
        else
            Update(data.Data);
    }

    private void Update(IDictionary<string, object> values)
    {
        LastUpdateCount = values[KeyCount].Value<int>();

        if (Layer.SynchronizeAlwaysSince.HasValue &&
            Layer.SynchronizeAlwaysSince >= Layer.Context.CurrentTimePoint) Synchronize();
    }

    private void Init(VectorStructuredData data)
    {
        var centroid = data.Geometry.Centroid;

        Position = Position.CreatePosition(centroid.X, centroid.Y);
        Name = data.Data["name"].Value<string>();
        var initialAmount = data.Data.ContainsKey(KeyCount)
            ? data.Data[KeyCount].Value<int>()
            : StandardAmount;
        VectorStructured = data;
        VectorStructured.Data.Add(SyncKey, 0);
        VectorStructured.Data.Add("Rents", 0);
        VectorStructured.Data.Add("Returns", 0);
        LastUpdateCount = initialAmount;
        ParkingBicycles.Clear();
        InitBicycles(initialAmount);
    }

    private void InitBicycles(int amount)
    {
        var entityManager = Layer.EntityManager;
        var environment = Layer.SpatialGraphMediatorLayer.Environment;
        if (environment == null)
            throw new ApplicationException($"{nameof(BicycleRentalLayer)} requires an {nameof(ISpatialGraphLayer)}");

        for (var i = 0; i < amount; i++)
            if (entityManager != null)
            {
                var bicycle = entityManager.Create<RentalBicycle>(BicycleType.Item1, BicycleType.Item2);
                bicycle.Environment = environment;
                bicycle.BicycleRentalStation = this;
                ParkingBicycles.TryAdd(bicycle, 0);
            }
            else
            {
                //TODO wird aufgerufen bevor BicycleLayer
                var bicycle = new RentalBicycle
                {
                    Height = 0, Length = 2, Width = 1, MaxAcceleration = 0, MaxDeceleration = 0, MaxSpeed = 50,
                    BicycleRentalStation = this,
                    Environment = environment
                };
                ParkingBicycles.TryAdd(bicycle, 0);
            }

        VectorStructured.Data[KeyCount] = Count;
    }

    /// <summary>
    ///     Enter the parking spot with a bicycle and "consume" its required space.
    /// </summary>
    /// <param name="bicycle">The bicycle that is parked in this spot.</param>
    /// <returns>True if a parking spot could be found, false otherwise (Beware: the bicycle is not parked here).</returns>
    public bool Enter(IRentalBicycle bicycle)
    {
        if (ParkingBicycles.ContainsKey(bicycle)) return false;

        VectorStructured.Data["Returns"] = VectorStructured.Data["Returns"].Value<int>() + 1;
        bicycle.BicycleRentalStation = this;
        var entered = ParkingBicycles.TryAdd(bicycle, byte.MinValue);
        VectorStructured.Data[KeyCount] = Count;
        return entered;
    }

    /// <summary>
    ///     Leave the parking spot with given bicycle.
    /// </summary>
    /// <param name="bicycle">The bicycle that leaves this spot.</param>
    /// <returns>True if bicycle is not on this parking space any more.</returns>
    public bool Leave(IRentalBicycle bicycle)
    {
        bicycle.BicycleRentalStation = null;
        var vehicleLeft = !ParkingBicycles.ContainsKey(bicycle) || ParkingBicycles.TryRemove(bicycle, out _);

        VectorStructured.Data["Rents"] = VectorStructured.Data["Rents"].Value<int>() + 1;
        VectorStructured.Data[KeyCount] = Count;

        return vehicleLeft;
    }

    /// <summary>
    ///     Rents any available <see cref="IRentalBicycle" /> in this rental station.
    /// </summary>
    /// <returns>The rented bicycle, null if none is available.</returns>
    public IRentalBicycle RentAny()
    {
        while (ParkingBicycles.Any())
        {
            var rentalBicycle = ParkingBicycles.FirstOrDefault().Key;
            if (rentalBicycle != null && ParkingBicycles.TryRemove(rentalBicycle, out _))
                return rentalBicycle;
        }

        return null;
    }

    /// <summary>
    ///     Synchronizes all stored updates values with the current status.
    /// </summary>
    /// <exception cref="NotImplementedException"></exception>
    public void Synchronize()
    {
        if (LastUpdateCount == null) return;

        //hard update strategy
        var updatedAmount = LastUpdateCount.Value;
        var update = updatedAmount - Count;

        if (updatedAmount > Count)
        {
            InitBicycles(updatedAmount - Count);
        }
        else
        {
            var deleteAmount = Count - updatedAmount;
            for (var i = 0; i < deleteAmount; i++)
            {
                var bicycle = ParkingBicycles.FirstOrDefault().Key;
                bicycle.BicycleRentalStation = null;
                ParkingBicycles.TryRemove(bicycle, out _);
            }
        }

        VectorStructured.Data[KeyCount] = ParkingBicycles.Count;
        VectorStructured.Data[SyncKey] = update;
    }
}