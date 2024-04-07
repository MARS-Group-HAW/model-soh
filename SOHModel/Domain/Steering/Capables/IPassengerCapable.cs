using Mars.Interfaces.Environments;
using SOHModel.Domain.Steering.Common;

namespace SOHModel.Domain.Steering.Capables;

/// <summary>
///     The passenger can use vehicles to be transported. THe vehicle notifies the passenger about certain events and
///     whereabouts.
/// </summary>
public interface IPassengerCapable : IPositionable
{
    /// <summary>
    ///     Informs the passenger about the most recent event.
    /// </summary>
    /// <param name="passengerMessage">Holds information about the current status of the vehicle.</param>
    void Notify(PassengerMessage passengerMessage);
}