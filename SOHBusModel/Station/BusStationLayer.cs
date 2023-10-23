using System;
using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace SOHBusModel.Station;

/// <summary>
///     The <see cref="BusStationLayer" /> capsules the access to all <code>BusStation</code>s.
/// </summary>
public class BusStationLayer : VectorLayer<BusStation>, IBusStationLayer
{
    private Position _anyPosition;

    private Position AnyPosition =>
        _anyPosition ??= Nearest(Position.CreateGeoPosition(0, 0)).Position;

    public BusStation Nearest(Position position, Func<BusStation, bool> predicate = null)
    {
        return base.Nearest(position != null ? position.PositionArray : _anyPosition.PositionArray, predicate);
    }

    public ModalChoice ModalChoice => ModalChoice.Bus;

    public BusStation Find(string stationId)
    {
        return Nearest(AnyPosition, station => station.Id == stationId);
    }
}