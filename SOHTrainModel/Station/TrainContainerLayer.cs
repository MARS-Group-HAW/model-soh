using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace SOHTrainModel.Station;

public class TrainContainerLayer : VectorLayer
{
    /// <summary>
    ///     Gets the modal type for which this layer is responsible.
    ///     E.g. the default pedestrian modality or car modality.
    /// </summary>
    public ModalChoice ModalChoice => ModalChoice.Walking;
}