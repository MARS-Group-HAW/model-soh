using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Car.Steering;
using SOHModel.Domain.Graph;
using SOHModel.Domain.Model;
using SOHModel.Domain.Steering.Capables;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model
{
    /// <summary>
    /// Represents a SemiTruck in the simulation. It extends the generic Vehicle class,
    /// implementing specific logic for SemiTruck behavior, steering, and passenger handling.
    /// </summary>
    public class SemiTruck : Vehicle<ISemiTruckSteeringCapable, IPassengerCapable, SemiTruckSteeringHandle, SemiTruckPassengerHandle>
    {
        // Backing field for the spatial graph environment
        private ISpatialGraphEnvironment _environment;

        // Private field to store the steering handle
        private SemiTruckSteeringHandle _steeringHandle;

        /// <summary>
        /// The current steering handle for the SemiTruck.
        /// </summary>
        public SemiTruckSteeringHandle SteeringHandle { get; private set; }

        /// <summary>
        /// Default constructor initializes the SemiTruck with the CarDriving modality.
        /// </summary>
        public SemiTruck()
        {
            ModalityType = SpatialModalityType.CarDriving;
        }
        

        /// <summary>
        /// The street layer associated with the SemiTruck, annotated for property injection.
        /// </summary>
        [PropertyDescription]
        public StreetLayer StreetLayer { get; set; }

        /// <summary>
        /// The spatial graph environment in which the SemiTruck operates.
        /// Defaults to the environment from the StreetLayer if not explicitly set.
        /// </summary>
        public ISpatialGraphEnvironment Environment
        {
            get => _environment ?? StreetLayer.Environment;
            set => _environment = value;
        }

        /// <summary>
        /// Creates and returns a passenger handle for the SemiTruck.
        /// </summary>
        /// <returns>A new instance of <see cref="SemiTruckPassengerHandle"/>.</returns>
        protected override SemiTruckPassengerHandle CreatePassengerHandle()
        {
            return new SemiTruckPassengerHandle(this);
        }

        /// <summary>
        /// Creates and returns a steering handle for the SemiTruck, using the associated driver.
        /// </summary>
        /// <param name="driver">The driver controlling the SemiTruck.</param>
        /// <returns>A new instance of <see cref="SemiTruckSteeringHandle"/>.</returns>
        protected override SemiTruckSteeringHandle CreateSteeringHandle(ISemiTruckSteeringCapable driver)
        {
            // Return a new steering handle for the SemiTruck
            return new SemiTruckSteeringHandle(Environment, this);
        }
    }
}
