using Mars.Common;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using NetTopologySuite.Geometries;
using SOHModel.Multimodal.Multimodal;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHModel.Multimodal.Model;

/// <summary>
///     A traveler agent tries to move from start to goal. He/she may use different modalities for that purpose.
///     The agent removes himself/herself from the simulation after fulfilling this task.
/// </summary>
public class Traveler<TLayer> : MultiCapableAgent<TLayer>
    where TLayer : IMultimodalLayer
{
    [PropertyDescription(Name = "source")] public Geometry SourceGeometry { get; set; }

    [PropertyDescription(Name = "destination")]
    public Geometry TargetGeometry { get; set; }

    internal int TravelScheduleId { get; set; }

    /// <summary>
    ///     The position this agent tries to reach.
    /// </summary>
    public Position GoalPosition { get; set; }

    public override void Init(TLayer layer)
    {
        if (SourceGeometry != null) StartPosition = SourceGeometry.RandomPositionFromGeometry();
        if (TargetGeometry != null) GoalPosition = TargetGeometry.RandomPositionFromGeometry();

        base.Init(layer);
    }

    /// <summary>
    ///     Defines which modal choices are used by the <code>Traveler</code> to move.
    /// </summary>
    protected new virtual IEnumerable<ModalChoice> ModalChoices()
    {
        return TravelerConstants.DefaultChoices;
    }


    public override void Tick()
    {
        MultimodalRoute ??= FindMultimodalRoute();

        base.Move();

        if (GoalReached) MultimodalLayer.UnregisterAgent(MultimodalLayer, this);
    }

    /// <summary>
    ///     Searches for a <see cref="MultimodalRoute" /> from current position to the goal.
    /// </summary>
    /// <returns>The found multimodal route.</returns>
    protected virtual MultimodalRoute FindMultimodalRoute()
    {
        return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoices());
    }
}