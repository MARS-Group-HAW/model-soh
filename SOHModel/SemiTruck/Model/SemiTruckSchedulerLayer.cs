using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;
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

                // TODO Why is ID and INIT necessary?
                var driver = new SemiTruckDriver
                {
                    ID = Guid.NewGuid(),
                    StartLat = startLat,
                    StartLon = startLon,
                    DestLat = destLat,
                    DestLon = destLon,
                    Drivemode = driveMode,
                    TruckType = semiTruckType
                };
                driver.Init(SemiTruckLayer);

                // Ensure the driver was initialized correctly
                if (driver.Position == null)
                {
                    Console.WriteLine("[ERROR] Driver Position was not initialized properly during Init. Skipping this driver.");
                    return; // Skip this driver
                }

                // Add the driver to the layer and register it
                SemiTruckLayer.Driver.Add(driver.ID, driver);
                RegisterAgent(SemiTruckLayer, driver);

                // Validate that the driver was successfully added
                if (!SemiTruckLayer.Driver.ContainsKey(driver.ID))
                {
                    Console.WriteLine("[ERROR] Driver registration failed. Skipping this driver.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Exception occurred while scheduling driver: {ex.Message}");
            }
        }
    }
}
