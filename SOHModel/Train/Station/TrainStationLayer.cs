using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace SOHModel.Train.Station;

/// <summary>
///     The <see cref="TrainStationLayer" /> capsules the access to all <code>TrainStation</code>s.
/// </summary>
public class TrainStationLayer : VectorLayer<TrainStation>, ITrainStationLayer
{
    private Position? _anyPosition;

    private Position AnyPosition =>
        _anyPosition ??= Nearest(Position.CreateGeoPosition(0, 0)).Position;

    public TrainStation? Nearest(Position? position, Func<TrainStation, bool>? predicate = null)
    {
        return base.Nearest(position != null ? position.PositionArray : _anyPosition.PositionArray, predicate);
    }

    public ModalChoice ModalChoice => ModalChoice.Train;

    public TrainStation Find(string stationId)
    {
        return Nearest(AnyPosition, station => station.Id == stationId);
    }
}