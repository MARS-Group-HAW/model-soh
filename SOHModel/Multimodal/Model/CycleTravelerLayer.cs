using SOHModel.Multimodal.Multimodal;
using SOHModel.Multimodal.Routing;

namespace SOHModel.Multimodal.Model;

/// <summary>
///     This layer implements the <see cref="IMultimodalLayer" /> to provide walking and bicycle driving mutli-modal
///     routing capabilities.
/// </summary>
public class CycleTravelerLayer : AbstractMultimodalLayer
{
    public int RentalCount { get; set; }

    /// <summary>
    ///     Provides the possibility to enter or leave the graph via gateway points.
    /// </summary>
    public GatewayLayer GatewayLayer { get; set; }
}