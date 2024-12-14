using System.Data;
using Mars.Common.Core;
using Mars.Common.Core.Collections;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Agents;
using SOHModel.SemiTruck.Model;

namespace SOHModel.SemiTruck.Scheduling
{
    /// <summary>
    /// Layer for scheduling SemiTruck drivers into the simulation.
    /// </summary>
    public class SemiTruckSchedulerLayer : SchedulerLayer
    {
        public SemiTruckLayer SemiTruckLayer { get; set; }

        /// <summary>
        /// Schedules a SemiTruckDriver based on the given scheduler entry.
        /// </summary>
        /// <param name="dataRow">Scheduler entry containing configuration data.</param>
        protected override void Schedule(SchedulerEntry dataRow)
        {
            // Validate required fields
            var requiredFields = new[] { "sourceX", "sourceY", "destinationX", "destinationY" };
            foreach (var field in requiredFields)
            {
                if (!dataRow.Data.ContainsKey(field) || string.IsNullOrEmpty(dataRow.Data[field].Value<string>()))
                {
                    Console.WriteLine($"[ERROR] Missing or empty required field: {field}. Skipping this entry.");
                    return; // Skip this entry
                }
            }

            try
            {
                // Extract parameters from the scheduler entry
                const string typeKey = "TruckType";
                var semiTruckType = dataRow.Data.TryGetValue(typeKey, out var type) ? type.Value<string>() : "StandardTruck";

                var startLat = dataRow.Data["sourceY"].Value<double>();
                var startLon = dataRow.Data["sourceX"].Value<double>();
                var destLat = dataRow.Data["destinationY"].Value<double>();
                var destLon = dataRow.Data["destinationX"].Value<double>();
                var driveMode = dataRow.Data.TryGetValue("DriveMode", out var driveModeValue) ? driveModeValue.Value<int>() : 1;
                
                var agentManager = SemiTruckLayer.Container.Resolve<IAgentManager>();
                var agents = agentManager.Spawn<SemiTruckDriver, SemiTruckLayer>(
                    dependencies: new List<IModelObject> { SemiTruckLayer, SemiTruckLayer.Environment },
                    assignment: agent =>
                    {
                        agent.StartLat = startLat;
                        agent.StartLon = startLon;
                        agent.DestLat = destLat;
                        agent.DestLon = destLon;
                        agent.DriveMode = driveMode;
                        agent.TruckType = semiTruckType;
                    });

                

                // Adds driver to the layer and register it
                SemiTruckLayer.Driver.AddRange(agents.ToDictionary(agent => agent.ID, agent => (IAgent)agent));

            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception occurred while scheduling driver: {ex.Message}");
            }
        }
    }
}
