using Mars.Components.Layers;
using Mars.Interfaces.Model;
using SOHModel.Bicycle.Parking;
using SOHModel.Bicycle.Rental;
using SOHModel.Bus.Model;
using SOHModel.Bus.Route;
using SOHModel.Bus.Station;
using SOHModel.Car.Model;
using SOHModel.Car.Parking;
using SOHModel.Car.Rental;
using SOHModel.Domain.Graph;
using SOHModel.Ferry.Model;
using SOHModel.Ferry.Route;
using SOHModel.Ferry.Station;
using SOHModel.Multimodal.Layers;
using SOHModel.Multimodal.Model;
using SOHModel.Multimodal.Routing;
using SOHModel.Train.Model;
using SOHModel.Train.Route;
using SOHModel.Train.Station;

namespace SOHModel;

public static class Startup
{
    public static ModelDescription CreateModelDescription()
    {
        var description = new ModelDescription();

        description.AddLayer<SpatialGraphMediatorLayer>([typeof(ISpatialGraphLayer)]);
        description.AddLayer<GatewayLayer>();

        description.AddLayer<BicycleParkingLayer>([typeof(IBicycleParkingLayer)]);
        description.AddLayer<BicycleRentalLayer>([typeof(IBicycleRentalLayer)]);
        description.AddLayer<CycleTravelerLayer>();
        description.AddLayer<CycleTravelerSchedulerLayer>();

        description.AddLayer<CarParkingLayer>([typeof(ICarParkingLayer)]);
        description.AddLayer<CarRentalLayer>([typeof(ICarRentalLayer)]);

        description.AddLayer<BusLayer>();
        description.AddLayer<BusSchedulerLayer>();
        description.AddLayer<BusStationLayer>();
        description.AddLayer<BusRouteLayer>([typeof(IBusRouteLayer)]);

        description.AddLayer<TrainLayer>();
        description.AddLayer<TrainSchedulerLayer>();
        description.AddLayer<TrainStationLayer>();
        description.AddLayer<TrainRouteLayer>([typeof(ITrainRouteLayer)]);

        description.AddLayer<PassengerTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>(
            "PassengerTravelerSchedulerLayer");

        description.AddLayer<FerryLayer>();
        description.AddLayer<FerryRouteLayer>();
        description.AddLayer<FerrySchedulerLayer>();
        description.AddLayer<FerryStationLayer>([typeof(IFerryStationLayer)]);

        description.AddLayer<DockWorkerLayer>();
        description.AddLayer<DockWorkerSchedulerLayer>();

        description.AddLayer<CitizenLayer>();
        description.AddLayer<HumanTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<HumanTraveler, HumanTravelerLayer>>(
            "HumanTravelerSchedulerLayer");

        description.AddLayer<MediatorLayer>();
        description.AddLayer<VectorBuildingsLayer>();
        description.AddLayer<VectorLanduseLayer>();
        description.AddLayer<VectorPoiLayer>();

        description.AddAgent<Citizen, CitizenLayer>();
        description.AddAgent<HumanTraveler, HumanTravelerLayer>();
        description.AddAgent<CycleTraveler, CycleTravelerLayer>();
        description.AddAgent<FerryDriver, FerryLayer>();
        description.AddAgent<DockWorker, DockWorkerLayer>();
        description.AddAgent<BusDriver, BusLayer>();
        description.AddAgent<PassengerTraveler, PassengerTravelerLayer>();
        description.AddAgent<TrainDriver, TrainLayer>();
        description.AddEntity<Bus.Model.Bus>();

        description.AddEntity<Bicycle.Model.Bicycle>();
        description.AddEntity<Car.Model.Car>();
        description.AddEntity<Train.Model.Train>();
        description.AddEntity<Ferry.Model.Ferry>();
        description.AddEntity<RentalBicycle>();
        description.AddEntity<RentalCar>();

        return description;
    }
}