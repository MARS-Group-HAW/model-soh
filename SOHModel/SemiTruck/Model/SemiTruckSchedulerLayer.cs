using System.Data;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Environments;

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
        
        Console.WriteLine("Processing new SchedulerEntry:");
        foreach (var entry in dataRow.Data)
        {
            Console.WriteLine($"  {entry.Key}: {entry.Value}");
        }

        const string typeKey = "truckType";

        // Retrieve truck type from the configuration or set a default type if not specified
        var truckType = dataRow.Data.TryGetValue(typeKey, out var type) ? type.Value<string>() : "StandardTruck";
        var maxPayload = dataRow.Data.TryGetValue("maxPayload", out var payload) ? payload.Value<int>() : 30000;
        
        Console.WriteLine($"TruckType: {truckType}, MaxPayload: {maxPayload}");

        // Retrieve start and destination coordinates for dynamic routing
        if (!dataRow.Data.TryGetValue("source", out var sourceValue) || 
            !dataRow.Data.TryGetValue("destination", out var destinationValue))
        {
            throw new ArgumentException("Missing source or destination coordinates for dynamic routing in input data.");
        }

        // Parse WKT format for source and destination
        var sourceCoords = dataRow.SourceGeometry.Coordinates;
        var destCoords = dataRow.TargetGeometry.Coordinates;

        double startLat = sourceCoords[0].X;
        double startLon = sourceCoords[0].Y;
        double destLat = destCoords[0].X;
        double destLon = destCoords[0].Y;
        
        Console.WriteLine($"Start Coordinates: ({startLat}, {startLon}), Destination: ({destLat}, {destLon})");

        // var position = Position.CreateGeoPosition(startLon, startLat);
        // // Retrieve the nearest edge from the environment based on start coordinates
        //
        // var nearestNode = _semiTruckLayer.GraphEnvironment.NearestNode(position);
        //
        // if (nearestNode == null)
        // {
        //     throw new InvalidOperationException($"No suitable node found near coordinates: ({startLat}, {startLon})");
        // }
        //
        // // Get an outgoing edge from the node
        // var outgoingEdges = nearestNode.OutgoingEdges.Values;
        // if (!outgoingEdges.Any())
        // {
        //     throw new InvalidOperationException($"No outgoing edges found for node at position: ({startLat}, {startLon})");
        // }
        // //TODO IST OutgoingEdges.First() vertretbar?
        // // Choose the first outgoing edge (or apply custom logic)
        // var startingEdge = outgoingEdges.First();
        

        
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
        
       

        Console.WriteLine($"Created SemiTruckDriver with ID: {driver.ID}");
        // Register the driver in the SemiTruckLayer
        _semiTruckLayer.Driver.Add(driver.ID, driver);
        Console.WriteLine($"Registered driver with ID: {driver.ID} to the SemiTruckLayer.");
        

        RegisterAgent(_semiTruckLayer, driver);
        Console.WriteLine($"Driver with ID: {driver.ID} successfully registered in the simulation.");
        
    
    }
}
