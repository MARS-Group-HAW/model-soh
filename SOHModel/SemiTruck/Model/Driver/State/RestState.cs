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
        private RestRequirement _requirement;

        /// <summary>Requests a stop at a rest area; planning starts with the next <c>CheckAndPlan</c>.</summary>
        public void Request(RestRequirement requirement) => _requirement = requirement;

        public bool HasRequest => _requirement != null;

        protected override RestStateType GetStopType() => RestStateType.Rest;

        protected override TimeSpan GetPauseDuration(SemiTruckLayer layer, SemiTruck truck) =>
            _requirement?.Duration ?? SemiTruckDriverConstants.BreakDuration;

        protected override string[] GetSearchTags() => new[] { "rest_area", "services" };

        protected override IEnumerable<dynamic> GetFacilityList(SemiTruckLayer layer) => layer.AllRestAreas;

        protected override bool AllowRoadsideStop => true;

        protected override double GetSearchRadius() => SemiTruckDriverConstants.RestAreaSearchRadius;

        protected override bool ShouldStop(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruck truck, FuelConsumptionTracker fuelTracker) => _requirement != null;

        protected override void OnPauseCompleted(SemiTruckLayer layer, SemiTruck truck,
            FuelConsumptionTracker fuelTracker, SemiTruckDriver driver)
        {
            _requirement = null;
        }

        protected override bool IsPauseCompleted(SemiTruckLayer layer, SemiTruck truck)
        {
            return layer._simulationTime >= _stopStartTime + GetPauseDuration(layer, truck);
        }

        /// <summary>
        /// Wrapper method to maintain compatibility with SemiTruckDriver.
        /// </summary>
        public bool HandlePause(SemiTruckSteeringHandle steeringHandle, SemiTruckLayer layer,
            SemiTruckDriver semiTruckDriver)
        {
            return HandlePause(steeringHandle, layer, semiTruckDriver.SemiTruck, null, semiTruckDriver);
        }
    }
}
