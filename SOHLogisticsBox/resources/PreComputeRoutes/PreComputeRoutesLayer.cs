namespace SOHRoutingOptimization.resources.PreComputeRoutes;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Mars.Common.Core.Collections;
using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Interfaces.Model;
using NetTopologySuite.Geometries;
using Newtonsoft.Json;
using ServiceStack;
using SOHModel.Domain.Graph;
using SOHModel.SemiTruck.Model;
/// <summary>
/// A simulation layer that precomputes shortest routes between all pairs of entry and exit points.
/// These routes are exported to a file for later use (e.g. by agents detecting they are on a predefined route).
/// </summary>
public class PreComputeRoutesLayer : AbstractLayer, ISemiTruckLayer, ISpatialGraphLayer, ISteppedActiveLayer
{
    /// <summary>
    /// The default modal choice for this layer is CarDriving.
    /// </summary>
    public ModalChoice ModalChoice { get; }

    /// <summary>
    /// The spatial graph environment in which the routing takes place.
    /// </summary>
    public ISpatialGraphEnvironment Environment { get; set; }
    
    /// <summary>
    /// A list of coordinates that represent detected motorway entry and exit points.
    /// </summary>
    public List<Coordinate> EntryExitCoordinates { get; private set; } = new();

    /// <summary>
    /// Initializes the layer, sets up the environment, loads entry/exit points, computes shortest paths, and stores them.
    /// </summary>
    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        // Load the road network environment
        if (Mapping.Value is ISpatialGraphEnvironment input)
        {
            Environment = input;
        }
        else if (!string.IsNullOrEmpty(layerInitData.LayerInitConfig.File))
        {
            Environment = new SpatialGraphEnvironment(new Input
            {
                File = layerInitData.LayerInitConfig.File,
                InputConfiguration = new InputConfiguration
                {
                    IsBiDirectedImport = layerInitData.LayerInitConfig.InputConfiguration?.IsBiDirectedImport ?? false
                }
            });
        }

        if (Environment == null) return false;

        // Load entry/exit nodes from configuration
        var success = LoadEntryExitNodesFromConfig(layerInitData);
        if (!success || EntryExitCoordinates.Count < 2) return false;

        var routesList = new List<object>();
        int routeCounter = 0;

        // Compute shortest routes between each pair of entry/exit nodes (limited to first 100 for testing)
        for (int i = 0; i < Math.Min(100, EntryExitCoordinates.Count); i++)
        {
            for (int j = 0; j < Math.Min(100, EntryExitCoordinates.Count); j++)
            {
                if (i == j) continue;

                var start = EntryExitCoordinates[i];
                var end = EntryExitCoordinates[j];

                var fromNode = Environment.NearestNode(Mars.Interfaces.Environments.Position.CreateGeoPosition(start.X, start.Y));
                var toNode = Environment.NearestNode(Mars.Interfaces.Environments.Position.CreateGeoPosition(end.X, end.Y));

                if (fromNode == null || toNode == null) continue;

                var route = Environment.FindShortestRoute(fromNode, toNode);
                if (route?.Stops == null || route.Stops.Count == 0) continue;

                var edgeIds = route.Stops
                    .Select(stop => stop.Edge?.GetId())
                    .Where(id => id != null)
                    .Select(id => id!.ToString())
                    .ToList();

                var routeData = new
                {
                    routeId = $"route_{++routeCounter:D6}",
                    startCoordinate = new[] { start.X, start.Y },
                    endCoordinate = new[] { end.X, end.Y },
                    edgeIds = edgeIds
                };

                routesList.Add(routeData);
            }
        }

        // Save the generated routes to a file
        var outputDir = Path.Combine(AppContext.BaseDirectory, "resources", "precomputed_routes");
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, "all_routes.json");
        var json = JsonConvert.SerializeObject(routesList, Formatting.Indented);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"[✓] {routeCounter} Routen erfolgreich gespeichert nach: {outputPath}");

        return true;
    }

    /// <summary>
    /// Loads the list of entry and exit node coordinates from a JSON configuration file.
    /// The file must be listed as an input in the LayerInitConfig and named "entry_exit_nodes.json".
    /// </summary>
    /// <param name="layerInitData">Layer configuration passed during initialization</param>
    /// <returns>True if the coordinates were successfully loaded; otherwise false</returns>
    private bool LoadEntryExitNodesFromConfig(LayerInitData layerInitData)
    {
        try
        {
            var entryInput = layerInitData.LayerInitConfig.Inputs
                .FirstOrDefault(i => Path.GetFileName(i.File)
                    .Equals("entry_exit_nodes.json", StringComparison.OrdinalIgnoreCase));

            if (entryInput == null) return false;

            var fullPath = Path.Combine(AppContext.BaseDirectory, entryInput.File);
            if (!File.Exists(fullPath)) return false;

            var json = File.ReadAllText(fullPath);
            var rawCoords = JsonConvert.DeserializeObject<List<List<double>>>(json);
            if (rawCoords == null || rawCoords.Count == 0) return false;

            foreach (var pair in rawCoords)
            {
                if (pair.Count == 2)
                    EntryExitCoordinates.Add(new Coordinate(pair[0], pair[1]));
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Not used for precomputation; included to fulfill interface requirements.
    /// </summary>
    public void Tick() { }
    /// <summary>
    /// Not used for precomputation; included to fulfill interface requirements.
    /// </summary>
    public void PreTick() { }
    /// <summary>
    /// Not used for precomputation; included to fulfill interface requirements.
    /// </summary>
    public void PostTick() { }
}