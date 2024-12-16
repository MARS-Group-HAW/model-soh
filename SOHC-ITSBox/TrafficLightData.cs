using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace SOHC_ITSBox
{
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

            // Durchsuche die Koordinatenpaare in der JSON-Datei
            foreach (var entry in _coordinatesData.Keys)
            {
                if (entry.Contains(coordinateToFind))
                {
                    // Gib die gesamte Liste der Phasen zurück
                    return _coordinatesData[entry];
                }
            }

            throw new KeyNotFoundException($"Keine Phasen für die Koordinate {coordinateToFind} gefunden.");
        }
    }
}
