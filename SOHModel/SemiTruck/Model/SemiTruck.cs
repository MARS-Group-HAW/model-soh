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
        /// Default constructor initializes the SemiTruck with the CarDriving modality.
        /// </summary>
        public SemiTruck()
        {
            ModalityType = SpatialModalityType.CarDriving;
        }

        /// <summary>
        /// Unladen mass in kg (tractor plus trailer). If greater than zero, <see cref="RoadUser.Mass"/> is
        /// derived as <c>TareMass + Payload</c>; otherwise the legacy <c>mass</c> column is used unchanged.
        /// </summary>
        [PropertyDescription(Name = "tareMass", Ignore = true)]
        public double TareMass { get; set; }

        /// <summary>
        /// Default cargo mass in kg, used together with <see cref="TareMass"/>.
        /// </summary>
        [PropertyDescription(Name = "payload", Ignore = true)]
        public double Payload { get; set; }

        /// <summary>
        /// Fraction (0-1) of capacity at which a stop ends. <c>0</c> selects the default (80% battery, 100% otherwise).
        /// </summary>
        [PropertyDescription(Name = "chargeTargetSoc", Ignore = true)]
        public double ChargeTargetSoc { get; set; }

        /// <summary>
        /// Recomputes <see cref="RoadUser.Mass"/> from tare mass and payload (optionally overriding the payload).
        /// </summary>
        public void ApplyLoad(double? payloadOverrideKg = null)
        {
            if (payloadOverrideKg is >= 0) Payload = payloadOverrideKg.Value;
            if (TareMass > 0) Mass = TareMass + Payload;
        }

        /// <summary>
        /// The fuel consumption strategy used by the truck.
        /// </summary>
        public IFuelConsumptionStrategy FuelConsumptionStrategy { get; private set; }

        /// <summary>
        /// The type of fuel strategy to use.
        /// </summary>
        [PropertyDescription]
        public FuelStrategyType FuelStrategy { get; set; } = FuelStrategyType.Linear;

        /// <summary>
        /// The strategy used to plan breaks and rests.
        /// </summary>
        public IRestStrategy RestPlanningStrategy { get; private set; } = new LegalMinimumRestStrategy();

        /// <summary>
        /// Selects the rest planning strategy (CSV column <c>restStrategy</c>).
        /// </summary>
        [PropertyDescription(Name = "restStrategy", Ignore = true)]
        public RestStrategyType RestStrategy { get; set; } = RestStrategyType.LegalMinimum;

        /// <summary>
        /// Minutes of legal driving time left at which the driver starts looking for a stop; <c>0</c> uses the default (30).
        /// </summary>
        [PropertyDescription(Name = "restPlanningMarginMinutes", Ignore = true)]
        public double RestPlanningMarginMinutes { get; set; }

        /// <summary>
        /// Initializes the rest planning strategy based on <see cref="RestStrategy"/>.
        /// </summary>
        public void InitializeRestStrategy()
        {
            RestPlanningStrategy = RestStrategy switch
            {
                RestStrategyType.LegalMinimum => new LegalMinimumRestStrategy(),
                RestStrategyType.ChargeWhileResting => new ChargeWhileRestingStrategy(),
                _ => throw new InvalidOperationException($"Unknown rest strategy: {RestStrategy}")
            };
        }

        /// <summary>
        /// Initializes the fuel consumption strategy based on the FuelStrategy type.
        /// </summary>
        public void InitializeFuelStrategy()
        {
            FuelConsumptionStrategy = FuelStrategy switch
            {
                FuelStrategyType.Linear => new LinearFuelConsumptionStrategy(),
                FuelStrategyType.RoadLoad => new RoadLoadFuelConsumptionStrategy(),
                _ => throw new InvalidOperationException($"Unknown strategy: {FuelStrategy}")
            };
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
