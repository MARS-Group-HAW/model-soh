using Mars.Interfaces.Environments;
using SOHModel.Bicycle.Rental;

namespace SOHModel.Multimodal.Model;

/// <summary>
///     This <see cref="Traveler{TLayer}" /> entity uses the <c>cycling</c> and <c>walking</c> modality to reach their
///     goal.
/// </summary>
public class CycleTraveler : Traveler<CycleTravelerLayer>
{
    private CycleTravelerLayer _cycleTravelerLayer;

    /// <summary>
    ///     Indicates if this agent possesses a bike on his/her own.
    /// </summary>
    public bool HasBike { get; set; }


    public override void Init(CycleTravelerLayer layer)
    {
        base.Init(layer);

        _cycleTravelerLayer = layer;
        EnvironmentLayer = layer.SpatialGraphMediatorLayer;
        BicycleRentalLayer = layer.BicycleRentalLayer;
        Gender = (GenderType)new Random().Next(2);
        OvertakingActivated = false;
        EnableCapability(ModalChoice.CyclingRentalBike);
        EnableCapability(ModalChoice.CyclingOwnBike);

        if (HasBike) Bicycle = _cycleTravelerLayer.EntityManager.Create<RentalBicycle>("type", "city");
    }

    protected override MultimodalRoute FindMultimodalRoute()
    {
        if (_cycleTravelerLayer.GatewayLayer != null)
        {
            var (start, goal) = _cycleTravelerLayer.GatewayLayer.Validate(Position, GoalPosition);
            return SearchMultimodalRoute(start, goal);
        }

        return SearchMultimodalRoute(Position, GoalPosition);
    }

    private MultimodalRoute SearchMultimodalRoute(Position start, Position goal)
    {
        if (HasBike)
        {
            var street = EnvironmentLayer.Environment;
            var route = street.FindShortestRoute(street.NearestNode(start), street.NearestNode(goal),
                edge => edge.Modalities.Contains(SpatialModalityType.CarDriving));
            return new MultimodalRoute(route, ModalChoice.CyclingOwnBike);
        }

        return MultimodalLayer.Search(this, start, goal, ModalChoice.CyclingRentalBike);
    }

    protected override bool EnterModalType(ModalChoice modalChoice, Route route)
    {
        var success = base.EnterModalType(modalChoice, route);
        if (success && modalChoice == ModalChoice.CyclingRentalBike) _cycleTravelerLayer.RentalCount++;
        return success;
    }
}