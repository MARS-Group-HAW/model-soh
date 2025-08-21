using Mars.Components.Layers;
using Mars.Interfaces.Environments;


namespace SOHModel.Tram.Station;

public class TramStationLayer: VectorLayer<TramStation>, ITramStationLayer
{
    private Position? _anyPosition;

    private Position AnyPosition =>
        _anyPosition ??= Nearest(Position.CreateGeoPosition(0, 0)).Position;

    public TramStation? Nearest(Position? position, Func<TramStation, bool>? predicate = null)
    {
        return base.Nearest(position != null ? position.PositionArray : _anyPosition.PositionArray, predicate);
    }

    public ModalChoice ModalChoice => ModalChoice.Train;

    public TramStation Find(string stationId)
    {
        return Nearest(AnyPosition, station => station.Id == stationId);
    }
}