using Mars.Components.Layers;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Bicycle.Model;
using SOHModel.Domain.Graph;

namespace SOHModel.Bicycle.Rental;

/// <summary>
///     The <code>BicycleRentalLayer</code> capsules the access to all <code>BicycleRentalStation</code>s.
///     It requires a vector file that contains all available stations.
/// </summary>
public class BicycleRentalLayer : VectorLayer<BicycleRentalStation>, IBicycleRentalLayer
{
    public string KeyCount { get; set; } = "Anzahl";

    [PropertyDescription(Name = "synchronizations")]
    public List<DateTime>? SynchronizationTimePoints { get; set; }

    [PropertyDescription(Name = "synchronizeAlwaysSince")]
    public DateTime? SynchronizeAlwaysSince { get; set; }

    /// <summary>
    ///     Provides access to the <see cref="ISpatialGraphEnvironment" /> on which the <see cref="Bicycle" />s move.
    /// </summary>
    [PropertyDescription]
    public ISpatialGraphLayer SpatialGraphMediatorLayer { get; set; }

    public bool IsInitialized { get; set; }

    public override bool InitLayer(
        LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        IsInitialized = true;
        return IsInitialized;
    }

    public ModalChoice ModalChoice => ModalChoice.CyclingRentalBike;

    public BicycleRentalStation? Nearest(Position position, bool notEmpty)
    {
        return Nearest(position.PositionArray, Predicate);

        bool Predicate(BicycleRentalStation rentalStation)
        {
            return !notEmpty || !rentalStation.Empty;
        }
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
        foreach (var vectorFeature in Features)
        {
            if (vectorFeature is BicycleRentalStation rentalStation)
            {
                rentalStation.Synchronize();
            }
        }
    }
}