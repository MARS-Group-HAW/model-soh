using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;

namespace SOHModel.SemiTruck.Model;

public class SemiTruckSchedulerLayer : SchedulerLayer
{
    private readonly SemiTruckLayer _semiTruckLayer;

    /// <summary>
    ///     Initializes the scheduler with a reference to the SemiTruckLayer.
    /// </summary>
    /// <param name="semiTruckLayer">The layer where SemiTruck drivers are managed.</param>
    public SemiTruckSchedulerLayer(SemiTruckLayer semiTruckLayer)
    {
        _semiTruckLayer = semiTruckLayer;
    }

    /// <summary>
    ///     Initializes the scheduler with a reference to the SemiTruckLayer and a DataTable for configuration.
    /// </summary>
    /// <param name="semiTruckLayer">The layer where SemiTruck drivers are managed.</param>
    /// <param name="table">DataTable containing scheduling configurations.</param>
    public SemiTruckSchedulerLayer(SemiTruckLayer semiTruckLayer, DataTable table) : base(table)
    {
        _semiTruckLayer = semiTruckLayer;
    }

    /// <summary>
    ///     Schedules and registers a new SemiTruckDriver based on the provided configuration data.
    /// </summary>
    /// <param name="dataRow">The configuration data for scheduling a SemiTruck driver.</param>
    protected override void Schedule(SchedulerEntry dataRow)
    {
        const string typeKey = "truckType";

        // Retrieve truck type from the configuration or set a default type if not specified
        var truckType = dataRow.Data.TryGetValue(typeKey, out var type) ? type.Value<string>() : "StandardTruck";
        var maxPayload = dataRow.Data.TryGetValue("maxPayload", out var payload) ? payload.Value<int>() : 30000;

        // Retrieve start and destination coordinates for dynamic routing
        if (!dataRow.Data.TryGetValue("startLat", out var startLatValue) || 
            !dataRow.Data.TryGetValue("startLon", out var startLonValue) ||
            !dataRow.Data.TryGetValue("destLat", out var destLatValue) || 
            !dataRow.Data.TryGetValue("destLon", out var destLonValue))
        {
            throw new ArgumentException("Missing start or destination coordinates for dynamic routing in input data.");
        }

        double startLat = startLatValue.Value<double>();
        double startLon = startLonValue.Value<double>();
        double destLat = destLatValue.Value<double>();
        double destLon = destLonValue.Value<double>();

        // Create the driver with dynamic route initialization
        var driver = new SemiTruckDriver(
            layer: _semiTruckLayer, 
            register: RegisterAgent, 
            unregister: UnregisterAgent, 
            startLat: startLat, 
            startLon: startLon, 
            destLat: destLat, 
            destLon: destLon, 
            truckType: truckType
        );


        // Register the driver in the SemiTruckLayer
        _semiTruckLayer.Driver.Add(driver.ID, driver);
        RegisterAgent(_semiTruckLayer, driver);
    }
}
