using System;
using System.Collections.Generic;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHBicycleModel.Model;
using SOHDomain.Graph;

namespace SOHBicycleModel.Rental;

/// <summary>
///     The <code>BicycleRentalLayer</code> capsules the access to all <code>BicycleRentalStation</code>s.
///     It requires a vector file that contains all available stations.
/// </summary>
public class BicycleRentalLayer : VectorLayer<BicycleRentalStation>, IBicycleRentalLayer
{
    public string KeyCount { get; set; } = "Anzahl";

    [PropertyDescription(Name = "synchronizations")]
    public List<DateTime> SynchronizationTimePoints { get; set; }

    [PropertyDescription(Name = "scenario")]
    public char Scenario { get; set; }

    [PropertyDescription(Name = "synchronizeAlwaysSince")]
    public DateTime? SynchronizeAlwaysSince { get; set; }

    public new IEntityManager EntityManager { get; private set; }

    /// <summary>
    ///     Provides access to the <see cref="ISpatialGraphEnvironment" /> on which the <see cref="Bicycle" />s move.
    /// </summary>
    [PropertyDescription]
    public SpatialGraphMediatorLayer SpatialGraphMediatorLayer { get; set; }

    public bool IsInitialized { get; set; }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        EntityManager = layerInitData?.Container?.Resolve<IEntityManager>();

        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        IsInitialized = true;
        return IsInitialized;
    }

    public ModalChoice ModalChoice => ModalChoice.CyclingRentalBike;

    public BicycleRentalStation Nearest(Position position, bool notEmpty)
    {
        bool Predicate(BicycleRentalStation rentalStation)
        {
            return !notEmpty || !rentalStation.Empty;
        }

        return Nearest(position.PositionArray, Predicate);
    }

    public override void SetCurrentTick(long currentStep)
    {
        base.SetCurrentTick(currentStep);

        if (SynchronizationTimePoints != null)
            if (SynchronizationTimePoints.Contains(Context.CurrentTimePoint.GetValueOrDefault()))
                PropagateSynchronization();

        if (SynchronizeAlwaysSince != null && SynchronizeAlwaysSince <= Context.CurrentTimePoint)
            PropagateSynchronization();
    }

    private void PropagateSynchronization()
    {
        Console.WriteLine($"{Context.CurrentTimePoint}: Apply synchronization for stations ... ");
        foreach (var vectorFeature in Features)
            if (vectorFeature is BicycleRentalStation rentalStation)
                rentalStation.Synchronize();
    }
}