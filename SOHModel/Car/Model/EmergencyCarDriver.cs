using Mars.Components.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Car.Steering;
using SOHModel.Domain.Steering.Capables;
using SOHModel.Domain.Steering.Common;

namespace SOHModel.Car.Model
{
    public sealed class EmergencyCarDriver : AbstractAgent, ICarSteeringCapable, IPassengerCapable
    {
        private readonly CarDriver _carDriver;

        public EmergencyCarDriver(CarDriver carDriver)
        {
            _carDriver = carDriver;
            SirenActive = false;
        }

        [PropertyDescription(Name = "sirenActive")]
        public bool SirenActive { get; set; }

        public void ActivateSiren()
        {
            SirenActive = true;
        }

        public void DeactivateSiren()
        {
            SirenActive = false;
        }

        public override void Tick()
        {
            if (SirenActive)
            {
                // Emergency-specific logic
            }

            _carDriver.Tick();
        }

        // Delegate properties to _carDriver
        public Position Position
        {
            get => _carDriver.Position ?? Position.CreateGeoPosition(0, 0); // Fallback-Position
            set => _carDriver.Position = value;
        }


        public double Velocity
        {
            get => _carDriver.Velocity;
            set => _carDriver.Velocity = value;
        }

        public bool GoalReached => _carDriver.GoalReached;

        public Route Route => _carDriver.Route;

        public string TrafficCode
        {
            get => _carDriver.TrafficCode;
            set => _carDriver.TrafficCode = value;
        }

        // Implement ICarSteeringCapable
        public Model.Car Car => _carDriver.Car;

        public bool CurrentlyCarDriving => _carDriver.CurrentlyCarDriving;

        // Implement ISteeringCapable
        public bool BrakingActivated
        {
            get => _carDriver.BrakingActivated;
            set => _carDriver.BrakingActivated = value;
        }

        public bool OvertakingActivated => _carDriver.OvertakingActivated;

        // Implement IPassengerCapable
        public void Notify(PassengerMessage message)
        {
            _carDriver.Notify(message);
        }
    }
}
