using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Car.Parking;

namespace SOHModel.BigEvent;

public class BarclaysParkingLayer : CarParkingLayer
{
    public static List<Car.Model.Car> ParkedCars { get; private set; } = new List<Car.Model.Car>();

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        var initialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        
        if (StreetLayer == null)
            throw new ArgumentException($"{nameof(CarParkingLayer)} requires {nameof(StreetLayer)}");

        if (initialized && OccupancyProbability > 0)
            UpdateOccupancy(OccupancyProbability);

        foreach (CarParkingSpace parkingSpace in Features.OfType<CarParkingSpace>())
        {
            var parkingSpaceName = parkingSpace.VectorStructured.Data["name"];
            if (parkingSpaceName != null && parkingSpaceName.ToString()!.Contains("Parkplatz"))
            {
                for (int i = 0; i < parkingSpace.Capacity; i++)
                {
                    var car = CreateOwnCarNear(parkingSpace.Position);
                    ParkedCars.Add(car);
                }
                Console.WriteLine("Parking Spot \"" + parkingSpaceName + "\" initialized with " + 
                                  parkingSpace.ParkingVehicles.Count + "/" + parkingSpace.Capacity + " cars.");
            }
        }
            
        return initialized;
    }
}