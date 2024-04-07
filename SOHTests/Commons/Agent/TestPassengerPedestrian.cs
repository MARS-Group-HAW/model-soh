using Mars.Interfaces.Environments;
using SOHModel.Ferry.Model;
using SOHModel.Train.Model;

namespace SOHTests.Commons.Agent;

/// <summary>
///     Pedestrian that can use other transportation vehicles as passenger
/// </summary>
public class TestPassengerPedestrian : TestMultiCapableAgent
{
    public Ferry UsedFerry { get; private set; }
    public Train UsedTrain { get; private set; }

    protected override bool EnterModalType(ModalChoice modalChoice, Route route)
    {
        if (modalChoice == ModalChoice.Ferry)
        {
            var ferryStation = FerryStationLayer.Nearest(Position);
            var ferry = ferryStation.Find(route.Goal);
            var result = TryEnterVehicleAsPassenger(ferry, this);
            if (result)
            {
                UsedFerry = ferry;
                HasUsedFerry = true;
            }

            return result;
        }

        if (modalChoice == ModalChoice.Train)
        {
            var station = TrainStationLayer.Nearest(Position);
            var train = station.Find(route.Goal);
            var result = TryEnterVehicleAsPassenger(train, this);
            if (result)
            {
                UsedTrain = train;
                HasUsedTrain = true;
            }

            return result;
        }

        return base.EnterModalType(modalChoice, route);
    }
}