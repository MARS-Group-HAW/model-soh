using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHDomain.Steering.Common;
using SOHFerryModel.Route;
using SOHFerryModel.Steering;

namespace SOHFerryModel.Model;

public class FerryDriver : AbstractAgent, IFerrySteeringCapable
{
    private static int _stableId;

    private long _startTickForCurrentStation;

    private FerryDriver(UnregisterAgent unregister, FerryLayer layer)
    {
        ID = Guid.NewGuid();
        _unregister = unregister;
        Layer = layer;
    }

    public FerryDriver(FerryLayer layer, UnregisterAgent unregister) : this(unregister, layer)
    {
        InitializeFerry();
    }

    public FerryDriver(FerryLayer layer, UnregisterAgent unregister, string ferryType) : this(unregister, layer)
    {
        InitializeFerry(ferryType);
    }

    public bool Boarding => AmountOfTicksAtCurrentStation <= MinimumBoardingTimeInSeconds || !DepartureTickArrived;
    public bool DepartureTickArrived => _departureTick <= Layer.Context.CurrentTick;

    private long AmountOfTicksAtCurrentStation => Layer.Context.CurrentTick - _startTickForCurrentStation;

    public FerryRoute.FerryRouteEnumerator FerryRouteEnumerator =>
        (FerryRoute.FerryRouteEnumerator)(_ferryRouteEnumerator ??= FerryRoute.GetEnumerator());

    public IEnumerable<FerryRouteEntry> RemainingStations => FerryRoute.Skip(FerryRouteEnumerator.CurrentIndex);

    public FerryRouteEntry CurrentFerryRouteEntry => FerryRouteEnumerator.Current;

    public int StationStops => FerryRoute.Entries.IndexOf(FerryRouteEnumerator.Current);

    [PropertyDescription(Name = "line")] public int Line { get; set; }

    [PropertyDescription(Name = "waitingInSeconds")]
    public int MinimumBoardingTimeInSeconds { get; set; }

    [PropertyDescription] public FerryLayer Layer { get; }

    private Mars.Interfaces.Environments.Route Route
    {
        get => _steeringHandle.Route;
        set => _steeringHandle.Route = value;
    }

    public bool GoalReached => _steeringHandle?.GoalReached ?? false;
    public Ferry Ferry { get; set; }
    public int StableId { get; } = _stableId++;

    public FerryRoute FerryRoute { get; set; }

    public Position Position
    {
        get => Ferry.Position;
        set => Ferry.Position = value;
    }

    public void Notify(PassengerMessage passengerMessage)
    {
        //do nothing, I am the driver
    }

    public bool OvertakingActivated => false;
    public bool BrakingActivated { get; set; }

    private void InitializeFerry(string type = "Typ2000")
    {
        Ferry = Layer.EntityManager.Create<Ferry>("type", type);
        Ferry.Layer = Layer;
        Ferry.TryEnterDriver(this, out _steeringHandle);
    }

    public override void Tick()
    {
        if (FerryRoute == null)
        {
            FindFerryRouteAndStartCommuting();
            _departureTick = Layer.Context.CurrentTick;
        }

        if (!Boarding)
        {
            Ferry.FerryStation?.Leave(Ferry);

            _steeringHandle.Move();

            if (GoalReached)
            {
                Environment.Remove(Ferry);
                FerryRouteEnumerator.Current?.To.Enter(Ferry);

                var currentMinutes = FerryRouteEnumerator.Current?.Minutes ?? 0;
                _departureTick += currentMinutes * 60;

                var notAtTerminalStation = FindNextRoute();
                if (notAtTerminalStation)
                {
                    Ferry.NotifyPassengers(PassengerMessage.GoalReached);
                }
                else
                {
                    Ferry.NotifyPassengers(PassengerMessage.TerminalStation);
                    _unregister(Layer, this);
                }
            }
        }
    }


    private void FindFerryRouteAndStartCommuting()
    {
        if (Layer.FerryRouteLayer.FerryRoutes.TryGetValue(Line, out var schedule))
            FerryRoute = schedule;
        else
            throw new ArgumentException($"No train route provided by {nameof(FerryRouteLayer)}");

        if (FerryRoute.Count() < 2)
            throw new ArgumentException("Ferry route requires at least two stops");

        FindNextRoute();

        if (!FerryRouteEnumerator.Current?.From.Enter(Ferry) ?? true)
            throw new ArgumentException("Ferry could not dock the first station");
    }

    private bool FindNextRoute()
    {
        if (!FerryRouteEnumerator.MoveNext()) return false;

        var source =
            Environment.NearestNode(FerryRouteEnumerator.Current?.From.Position, SpatialModalityType.ShipDriving);
        var target =
            Environment.NearestNode(FerryRouteEnumerator.Current?.To.Position, SpatialModalityType.ShipDriving);

        Route = Environment.FindShortestRoute(source, target,
            edge => edge.Modalities.Contains(SpatialModalityType.ShipDriving));

        if (Route == null)
            throw new ApplicationException(
                $"{nameof(FerryDriver)} cannot find route from '{source.Position}' " +
                $"to '{target.Position}' but: {Environment.Nodes.Count}");

        _startTickForCurrentStation = Layer.Context.CurrentTick;
        Environment.Insert(Ferry, Route.First().Edge.From);
        return true;
    }


    #region fields

    private FerrySteeringHandle _steeringHandle;
    private readonly UnregisterAgent _unregister;
    private long _departureTick;
    private ISpatialGraphEnvironment Environment => Layer.GraphEnvironment;

    private IEnumerator<FerryRouteEntry> _ferryRouteEnumerator;

    #endregion
}