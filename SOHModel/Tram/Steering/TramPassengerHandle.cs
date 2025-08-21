using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Handles;


namespace SOHModel.Tram.Steering;
/// <summary>
///     This handle provides the position of the tram and the possibility to leave the tram.
/// </summary>
public class TramPassengerHandle : IPassengerHandle
{
    private readonly Model.Tram _tram;

    public TramPassengerHandle(Model.Tram tram)
    {
        _tram = tram;
    }

    public Position Position => _tram.Position;

    /// <summary>
    ///     Leave the tram if it is located in a tram station.
    /// </summary>
    /// <param name="passengerCapable">Who wants to leave the tram.</param>
    /// <returns>True if the tram could be left. False otherwise.</returns>
    public bool LeaveVehicle(IPassengerCapable passengerCapable)
    {
        if (_tram.TramStation == null) return false;

        _tram.LeaveVehicle(passengerCapable);
        return true;
    }

}