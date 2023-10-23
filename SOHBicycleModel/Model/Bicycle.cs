using System;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Common;
using SOHBicycleModel.Parking;
using SOHBicycleModel.Steering;
using SOHDomain.Model;
using SOHDomain.Steering.Capables;

namespace SOHBicycleModel.Model;

public class Bicycle : Vehicle<IBicycleSteeringCapable, IPassengerCapable, BicycleSteeringHandle,
    BicyclePassengerHandle>
{
    private BicycleType _type;

    public Bicycle()
    {
        ModalityType = SpatialModalityType.Cycling;
        PassengerCapacity = 1;
    }

    /// <summary>
    ///     Gets or sets the underlying managed <see cref="ISpatialGraphEnvironment" />.
    /// </summary>
    public ISpatialGraphEnvironment Environment { get; set; }

    /// <summary>
    ///     Provides access to the parking lot if the bicycle is stored there.
    /// </summary>
    public BicycleParkingLot BicycleParkingLot { get; set; }

    /// <summary>
    ///     Gets or sets the type of this bicycle.
    /// </summary>
    [PropertyDescription(Name = "type")]
    public BicycleType Type
    {
        get => _type;
        set
        {
            _type = value;
            Weight = HandleBicycleType.GetBicycleWeight(value);
        }
    }

    /// <summary>
    ///     Gets or sets the weight for this bicycle in <c>(kg)</c>.
    /// </summary>
    [PropertyDescription(Name = "weight")]
    public double Weight { get; set; }

    /// <summary>
    ///     Gets or sets teh weight load of package for this bicycle in <c>(kg)</c>.
    /// </summary>
    [PropertyDescription(Name = "weightLoad")]
    public double WeightLoad { get; set; }

    /// <summary>
    ///     TODO: Remove driver mass dependency
    /// </summary>
    [PropertyDescription(Name = "driverMass")]
    public double DriverMass { get; set; }

    /// <summary>
    ///     Gets or sets the whole mass of this object in <c>(kg)</c>.
    /// </summary>
    [PropertyDescription(Name = "mass")]
    public override double Mass
    {
        get => Weight + DriverMass + WeightLoad;
        set => throw new ArgumentException(
            "Bicycle mass needs to be set by 'weightLoad', 'weight', 'driverMass'");
    }

    protected override BicyclePassengerHandle CreatePassengerHandle()
    {
        return new BicyclePassengerHandle(this);
    }

    protected override BicycleSteeringHandle CreateSteeringHandle(IBicycleSteeringCapable driver)
    {
        return new BicycleSteeringHandle(Environment, this, driver);
    }
}