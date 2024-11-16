using Mars.Common.Core.Random;
using System.Collections.Generic;
using Mars.Common;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Bicycle.Parking;
using SOHModel.Car.Parking;
using SOHModel.Multimodal.Model;

namespace SOHModel.BigEvent;

/// <summary>
///     This <see cref="Traveler{BigEventLayer}" /> entity uses a variety of modalities to reach its goal.
/// </summary>
public class Visitor : Traveler<BigEventLayer>
{
    [PropertyDescription] public bool LivesNearby { get; set; } // Indicates if the visitor lives nearby

    [PropertyDescription] public double UsesPublicTransport { get; set; } // Probability of using public transport
    private readonly ISet<ModalChoice> _modalChoices = new HashSet<ModalChoice> { ModalChoice.Walking, ModalChoice.Train };

    public ModalChoice ChosenMode { get; private set; } // Store the chosen mode of transport

    public override void Init(BigEventLayer layer)
    {
        if (SourceGeometry != null) StartPosition = layer.GetExitPosition(); // Choose random exit position from the arena
        if (TargetGeometry != null) GoalPosition = TargetGeometry.RandomPositionFromGeometry(); // Choose random goal position

        base.Init(layer);

        // Choose a single modality
        ChosenMode = Evaluate(this, layer);
    }

    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        // This method is no longer needed, as the choice is now stored in ChosenMode
        return new List<ModalChoice> { ChosenMode };
        return _modalChoices;
    }


    protected override MultimodalRoute FindMultimodalRoute()
    {
        try
        {
            return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoices());
        }
        catch (Exception ex) {
            if (ex.Message.Contains("no reachable train station found", System.StringComparison.CurrentCultureIgnoreCase) || ex.Message.Contains("no train route available", System.StringComparison.CurrentCultureIgnoreCase)) {
                return MultimodalLayer.Search(this, StartPosition, GoalPosition, [ModalChoice.Walking]);
            }
            else throw;
        }
    }

    public ModalChoice Evaluate(Visitor attributes, BigEventLayer layer)
    {
        // Default to Walking if the visitor does not live nearby
        ModalChoice chosenMode = ModalChoice.Walking;

        // If the visitor lives nearby, they will walk
        if (!attributes.LivesNearby)
        {
            // Choose public transport based on probability
            if (RandomHelper.Random.NextDouble() < attributes.UsesPublicTransport)
            {
                chosenMode = ModalChoice.Bus; // Choose bus
                GoalPosition = layer.GetBusStopPosition(); // Get a random bus stop as the goal position
            }
        }

        return chosenMode; // Return the single chosen mode of transport
    }
}


