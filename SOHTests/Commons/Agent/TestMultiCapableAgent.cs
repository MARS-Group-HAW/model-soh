using Mars.Interfaces.Environments;
using SOHMultimodalModel.Model;
using SOHMultimodalModel.Routing;
using SOHTests.Commons.Layer;
using Xunit;

namespace SOHTests.Commons.Agent;

/// <summary>
///     Test agent that uses walking, driving and cycling to move.
/// </summary>
public class TestMultiCapableAgent : MultiCapableAgent<TestMultimodalLayer>
{
    public Position GoalPosition { get; set; }
    public ModalChoice ModalChoice { get; set; }

    public bool HasUsedCar { get; private set; }
    public bool HasUsedBicycle { get; private set; }
    public bool HasUsedFerry { get; protected set; }
    public bool HasUsedTrain { get; protected set; }

    public override void Init(TestMultimodalLayer layer)
    {
        Gender = GenderType.Male;
        Mass = 80;

        EnvironmentLayer = layer.SpatialGraphMediatorLayer;

        BicycleRentalLayer = layer.BicycleRentalLayer;
        CarParkingLayer = layer.CarParkingLayer;
        CarRentalLayer = layer.CarRentalLayer;
        FerryStationLayer = layer.FerryStationLayer;
        TrainStationLayer = layer.TrainStationLayer;

        EnableCapability(ModalChoice);

        base.Init(layer);

        Assert.NotNull(StartPosition);
        if (GoalPosition != null)
        {
            MultimodalRoute = layer.Search(this, StartPosition, GoalPosition, ModalChoice);
            if (!StartPosition.Equals(GoalPosition)) Assert.NotEmpty(MultimodalRoute);
        }
    }

    public override void Tick()
    {
        Move();
    }

    protected override bool EnterModalType(ModalChoice modalChoice, Route route)
    {
        if (!base.EnterModalType(modalChoice, route)) return false;

        if (modalChoice == ModalChoice.CarDriving)
        {
            HasUsedCar = true;
            Assert.Null(Car.CarParkingSpace);
        }
        else if (modalChoice is ModalChoice.CyclingRentalBike or ModalChoice.CyclingOwnBike)
        {
            HasUsedBicycle = true;
        }
        else if (modalChoice == ModalChoice.Ferry)
        {
            HasUsedFerry = true;
        }
        else if (modalChoice == ModalChoice.Train)
        {
            HasUsedTrain = true;
        }

        return true;
    }

    protected new void ReRouteToGoal()
    {
        base.ReRouteToGoal();

        Assert.All(MultimodalRouteCommons.GiveDistanceOfSwitchPoints(MultimodalRoute),
            d => Assert.InRange(d, 0, 10));
    }

    public void SetPreferredSpeed(double preferredSpeed)
    {
        PreferredSpeed = preferredSpeed;
    }
}