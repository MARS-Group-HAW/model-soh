using System;
using System.Collections.Generic;
using Mars.Interfaces.Environments;
using SOHModel.Database;
using SOHModel.Domain.Steering.Common;
using SOHModel.SemiTruck.Model.Driver.Utils;
using SOHModel.SemiTruck.Steering;

namespace SOHModel.SemiTruck.Model.Driver.State
{

    /// <summary>
    /// Manages rest area related state and logic for a SemiTruckDriver.
    /// </summary>
    public class RestState : StopState
    {
        private DateTime _lastBreakTime;
        private bool _pauseCompleted;
        private readonly TimeSpan _maxDrivingTimeWithoutBreak = SemiTruckDriverConstants.MaxDrivingTimeLimit;

        /// <summary>
        /// Initializes the rest state with the current simulation time.
        /// </summary>
        public void Initialize(DateTime simulationTime)
        {
            _lastBreakTime = simulationTime;
        }

        protected override RestStateType GetStopType() => RestStateType.Rest;

        protected override TimeSpan GetPauseDuration(SemiTruckLayer layer, SemiTruck truck) => SemiTruckDriverConstants.DefaultRestDuration;

        protected override string[] GetSearchTags() => new[] { "rest_area", "services" };

        protected override IEnumerable<dynamic> GetFacilityList(SemiTruckLayer layer) => layer.AllRestAreas;

        protected override double GetSearchRadius() => SemiTruckDriverConstants.RestAreaSearchRadius;

        protected override bool ShouldStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker)
        {
            return ShouldRest(layer._simulationTime, steeringHandle.Route.RemainingRouteDistanceToGoal);
        }

        protected override void OnPauseCompleted(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            _pauseCompleted = false;
        }

        protected override void OnArrival(SemiTruckLayer layer)
        {
            _lastBreakTime = layer._simulationTime;
        }

        protected override bool IsPauseCompleted() => _pauseCompleted;

        protected override void MarkPauseCompleted(bool completed)
        {
            _pauseCompleted = completed;
        }

        /// <summary>
        /// Wrapper method to maintain compatibility with SemiTruckDriver.
        /// </summary>
        public bool HandlePause(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruckDriver semiTruckDriver)
        {
            return HandlePause(steeringHandle, layer, semiTruckDriver.SemiTruck, null, semiTruckDriver);
        }

        /// <summary>
        /// Checks whether the truck needs to take a mandatory rest break.
        /// </summary>
        public bool ShouldRest(DateTime simulationTime, double remainingDistance)
        {
            return (simulationTime - _lastBreakTime) > _maxDrivingTimeWithoutBreak &&
                   remainingDistance > SemiTruckDriverConstants.RestAreaSearchRadius;
        }

        /// <summary>
        /// Sets the target rest node.
        /// Wrapper for compatibility with existing code.
        /// </summary>
        public void SetRestNode(ISpatialNode node)
        {
            SetTargetNode(node);
        }
    }
}
