using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Bus.Model;
using SOHModel.Bus.Station;
using SOHModel.Bus.Steering;
using SOHModel.Ferry.Model;
using SOHModel.Ferry.Station;
using SOHModel.Ferry.Steering;
using SOHModel.Multimodal.Multimodal;
using SOHModel.Train.Model;
using SOHModel.Train.Station;
using SOHModel.Train.Steering;
using SOHModel.Tram.Model;
using SOHModel.Tram.Station;
using SOHModel.Tram.Steering;

namespace SOHTests.Commons.Agent;

/// <summary>
/// Pedestrian that can use PT as passenger (Ferry/Train/Tram/Bus).
/// Tram rides on TrainDriving, so we expose both Tram & Train passenger interfaces.
/// </summary>
public class TestPassengerPedestrian : TestMultiCapableAgent,
    ITrainPassenger, IFerryPassenger, IBusPassenger
{
    // --- Layers injected by the test/Init(...) ---
    [PropertyDescription] public FerryStationLayer FerryStationLayer { get; set; }
    [PropertyDescription] public TrainStationLayer TrainStationLayer { get; set; }
    [PropertyDescription] public BusStationLayer BusStationLayer { get; set; }

    // Handy switch for tests: enable Train modality (covers Tram as well)
    [PropertyDescription(Name = "CapabilityTrain")]
    public bool CapabilityTrain
    {
        set
        {
            if (value) EnableCapability(ModalChoice.Train);
        }
    }

    public Ferry UsedFerry { get; private set; }
    public Train UsedTram { get; private set; }
    public Bus UsedBus { get; private set; }

    protected override bool EnterModalType(ModalChoice modalChoice, Route route)
    {
        switch (modalChoice)
        {
            case ModalChoice.Ferry:
            {
                var station = FerryStationLayer?.Nearest(Position);
                var ferry = station?.Find(route.Goal);
                var ok = TryEnterVehicleAsPassenger(ferry, this);
                if (ok)
                {
                    UsedFerry = ferry;
                    HasUsedFerry = true;
                }

                return ok;
            }

            case ModalChoice.Train:
            {
                // Try TRAM first (TrainDriving), then classic RAIL if available
                var tStation = TrainStationLayer?.Nearest(Position);
                var tram = tStation?.Find(route.Goal);
                if (TryEnterVehicleAsPassenger(tram, this))
                {
                    UsedTram = tram;
                    HasUsedTrain = true; // keep legacy flag semantics
                    return true;
                }

                var rStation = TrainStationLayer?.Nearest(Position);
                var train = rStation?.Find(route.Goal);
                if (TryEnterVehicleAsPassenger(train, this))
                {
                    HasUsedTrain = true;
                    return true;
                }

                return false;
            }

            case ModalChoice.Bus:
            {
                var station = BusStationLayer?.Nearest(Position);
                var bus = station?.Find(route.Goal);
                var ok = TryEnterVehicleAsPassenger(bus, this);
                if (ok)
                {
                    UsedBus = bus;
                    HasUsedBus = true;
                }

                return ok;
            }

            default:
                return base.EnterModalType(modalChoice, route);
        }
    }
}