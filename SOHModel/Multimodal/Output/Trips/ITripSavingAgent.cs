using Mars.Core.Data.Wrapper.Memory;

namespace SOHModel.Multimodal.Output.Trips;

/// <summary>
///     Has a <see cref="Mars.Core.Data.Wrapper.Memory.TripsCollection" /> that stores all trips.
/// </summary>
public interface ITripSavingAgent
{
    /// <summary>
    ///     Uniquely identifies this agent.
    /// </summary>
    int StableId { get; }

    /// <summary>
    ///     Contains all trip information.
    /// </summary>
    TripsCollection TripsCollection { get; }
}