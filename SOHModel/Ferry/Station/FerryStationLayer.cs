using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace SOHModel.Ferry.Station;

/// <summary>
///     The <see cref="FerryStationLayer" /> capsules the access to all <code>FerryStation</code>s.
/// </summary>
public class FerryStationLayer : VectorLayer<FerryStation>, IFerryStationLayer
{
    private Position _anyPosition;

    private Position AnyPosition => _anyPosition ??= Nearest(Position.CreatePosition(0, 0)).Position;


    public FerryStation Nearest(Position position, Func<FerryStation, bool> predicate = null)
    {
        return Nearest(position != null ? position.PositionArray : AnyPosition.PositionArray, predicate);
    }

    public ModalChoice ModalChoice => ModalChoice.Ferry;

    public FerryStation Find(string stationId)
    {
        return Nearest(AnyPosition.PositionArray, station => station.Id == stationId);
    }
}