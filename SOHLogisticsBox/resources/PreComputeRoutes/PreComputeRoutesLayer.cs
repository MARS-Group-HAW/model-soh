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

public class PreComputeRoutesLayer : AbstractLayer, ISemiTruckLayer, ISpatialGraphLayer, ISteppedActiveLayer
{
    public ModalChoice ModalChoice { get; }

    public ISpatialGraphEnvironment Environment { get; set; }

    public List<Coordinate> EntryExitCoordinates { get; private set; } = new();

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        // Straßennetz laden
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

        // Entry/Exit laden
        var success = LoadEntryExitNodesFromConfig(layerInitData);
        if (!success || EntryExitCoordinates.Count < 2) return false;

        var routesList = new List<object>();
        int routeCounter = 0;

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

        // Zielordner sicherstellen
        var outputDir = Path.Combine(AppContext.BaseDirectory, "resources", "precomputed_routes");
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, "all_routes.json");
        var json = JsonConvert.SerializeObject(routesList, Formatting.Indented);
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"[✓] {routeCounter} Routen erfolgreich gespeichert nach: {outputPath}");
        // // Rekonstruktion als Test
        // var rebuiltEdges = edgeIds
        //     .Select(idStr =>
        //     {
        //         if (int.TryParse(idStr, out var id) && Environment.Edges.TryGetValue(id, out var edge))
        //             return edge;
        //         return null;
        //     })
        //     .Where(edge => edge != null)
        //     .ToList();
        //
        //
        // var rebuiltStops = rebuiltEdges
        //     .Select(edge => new EdgeStop(edge))
        //     .ToList();
        //
        // var rebuiltRoute = new Route(false);
        // rebuiltRoute.Stops.AddRange(rebuiltStops);

        return true;
    }

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

    public void Tick() { }
    public void PreTick() { }
    public void PostTick() { }
}