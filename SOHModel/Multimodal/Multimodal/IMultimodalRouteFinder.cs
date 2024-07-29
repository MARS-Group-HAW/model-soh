using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;

namespace SOHModel.Multimodal.Multimodal;

/// <summary>
///     Single access point for finding a <code>MultimodalRoute</code> for given capabilities.
/// </summary>
public interface IMultimodalRouteFinder
{
    /// <summary>
    ///     Provides the possibility to search for a <c>MultimodalRoute</c> for a given capability only.
    /// </summary>
    /// <param name="agent">For whom the search is performed. May hold modal specific information.</param>
    /// <param name="start">Start point of search.</param>
    /// <param name="goal">End point of search.</param>
    /// <param name="modalChoice">Define which modal type is used.</param>
    /// <returns>The multimodal route for given capability.</returns>
    MultimodalRoute? Search(IModalCapabilitiesAgent agent, Position start, Position goal, ModalChoice modalChoice);

    /// <summary>
    ///     Provides the possibility to search for a <code>MultimodalRoute</code> for given capabilities.
    ///     If multiple capabilities are given, then the one with shortest expected travel time is returned.
    /// </summary>
    /// <param name="agent">For whom the search is performed. May hold modal specific information.</param>
    /// <param name="start">Start point of search.</param>
    /// <param name="goal">End point of search.</param>
    /// <param name="capabilities">Define which modal type is used.</param>
    /// <returns>The fastest multimodal route from start to goal and given capabilities.</returns>
    MultimodalRoute? Search(IModalCapabilitiesAgent agent, Position start, Position goal,
        IEnumerable<ModalChoice> capabilities);
}