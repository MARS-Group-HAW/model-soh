using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Mars.Interfaces;

namespace SOHMultimodalModel.Planning;

/// <summary>
///     A <see cref="Tour" /> is an enumeration of <see cref="Trip" />s that starts and ends at home.
/// </summary>
public class Tour : IEnumerator<Trip>
{
    private ISimulationContext _context;
    private int _index = -1;

    /// <summary>
    ///     Creates a new <see cref="Tour" /> for and working, part-time working or unemployed agent.
    /// </summary>
    /// <param name="context">The simulation clock with current tick, step and realtime.</param>
    /// <param name="worker">The flag indicating that this plan is for a working agent.</param>
    /// <param name="partTimeWorker">The flag indicating that this is for an agent, only working in noon.</param>
    public Tour(ISimulationContext context, bool worker, bool partTimeWorker)
    {
        Trips = DayPlanGenerator
            .CreateDayPlanForAgent(context.CurrentTimePoint.GetValueOrDefault(), worker, partTimeWorker).ToList();
        _context = context;
    }

    public Tour()
    {
        // Action are associated with fixed time-points in example 8 o'clock is starting the shift for everyone.
        // Participated actions and their order can be distinguished. 
    }

    /// <summary>
    ///     Gets the ordered sequence of trips.
    /// </summary>
    public List<Trip> Trips { get; }

    public TripReason[] ActionOrder { get; set; }

    /// <summary>
    ///     Resets the day plan iterator.
    /// </summary>
    public void Reset()
    {
        _index = -1;
    }

    /// <summary>
    ///     Gets the current object of the enumerator
    /// </summary>
    object IEnumerator.Current => Current;

    /// <summary>
    ///     Gets the current <see cref="Trip" /> for the current simulation time.
    /// </summary>
    public Trip Current => _index < 0 ? null : Trips[_index];

    /// <summary>
    ///     Moves to the next <see cref="Trip" /> when the time is coming and actions are available.
    /// </summary>
    /// <returns>Returns true, when the cursor moved to the next trip.</returns>
    public bool MoveNext()
    {
        var nextAction = _index < Trips.Count - 1 ? Trips[_index + 1] : null;

        if (_context.CurrentTimePoint >= nextAction?.StartTime)
        {
            _index++;
            return true;
        }

        return false;
    }


    public void Dispose()
    {
        _index = 0;
        _context = null;
    }
}