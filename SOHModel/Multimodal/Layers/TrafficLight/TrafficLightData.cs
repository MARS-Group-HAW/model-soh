using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SOHModel.Multimodal.Layers.TrafficLight;

public class TrafficLightData
{
    private readonly Dictionary<string, List<int>> _coordinatesData;

    // Konstruktor: JSON-Datei laden und Daten in Dictionary konvertieren
    public TrafficLightData(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Die Datei {filePath} wurde nicht gefunden.");
        }
        var jsonData = File.ReadAllText(filePath);
        _coordinatesData = JsonConvert.DeserializeObject<Dictionary<string, List<int>>>(jsonData);
    }

    // Liste der Phasen für eine gegebene Koordinate abrufen
    public List<int> GetPhasesForCoordinate(double latitude, double longitude)
    {
        // Koordinate in das JSON-Format umwandeln
        string coordinateToFind = $"({latitude}, {longitude})";

        int counter = 0;
        // Durchsuche die Koordinatenpaare in der JSON-Datei
        foreach (var entry in _coordinatesData.Keys)
        {
            string normalizedCoordinateToFind = coordinateToFind.Replace("\r\n", "\n").Replace("\r", "\n").Trim();
            string normalizedEntry = entry.Replace("\r\n", "\n").Replace("\r", "\n").Trim();

            Console.WriteLine($"var: {++counter} Comparing entry: '{normalizedEntry}' with coordinate: '{normalizedCoordinateToFind}'");

            if (normalizedEntry.Contains(normalizedCoordinateToFind, StringComparison.OrdinalIgnoreCase))
            {
                // Gib die gesamte Liste der Phasen zurück
                return _coordinatesData[entry];
            }
        }

        throw new KeyNotFoundException($"Keine Phasen für die Koordinate {coordinateToFind} gefunden.");
    }

}

