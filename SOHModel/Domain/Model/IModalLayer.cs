using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHModel.Domain.Model;

/// <summary>
///     The modal layers provide access to resources for the respective modal type.
/// </summary>
public interface IModalLayer : ILayer
{
    /// <summary>
    ///     Gets the modal type for which this layer is responsible.
    ///     E.g. the default pedestrian modality or car modality.
    /// </summary>
    ModalChoice ModalChoice { get; }
}