using System.Text.Json;
using Mars.Components.Layers;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SOHModel.SemiTruck.Model;

namespace SOHModel.SemiTruck.RealTimeData;
/// <summary>
/// Represents a weather-affected area with a polygon geometry and metadata.
/// Influences the speed factor of vehicles driving through this zone.
/// </summary>
public class WeatherZone
{
    /// <summary>
    /// The spatial area of the weather zone represented as a polygon (in WGS84 coordinates).
    /// </summary>
    public Polygon Area { get; set; }
    /// <summary>
    /// Type of weather (e.g., "rain", "snow", "storm", etc.).
    /// </summary>
    public string Type { get; set; } = "normal";
    /// <summary>
    /// The start time when the weather effect begins.
    /// </summary>
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    /// <summary>
    /// The end time when the weather effect ends.
    /// </summary>
    public DateTime EndTime { get; set; } = DateTime.MaxValue;
    /// <summary>
    /// Speed factor applied to vehicles within this zone (1.0 = normal speed, <1.0 = reduced speed).
    /// </summary>
    public double SpeedFactor { get; set; } = 1.0; // 1.0 = normal
}

/// <summary>
/// A simulation layer that integrates real-time weather data into the SemiTruck model.
/// Weather zones affect truck speed dynamically based on warnings from the DWD API (Warnwetter).
/// </summary>
public class SemiTruckWeatherLayer : AbstractLayer, ISteppedActiveLayer
{
    private readonly GeometryFactory _geometryFactory = new GeometryFactory();
    /// <summary>
    /// HTTP client to fetch real-time weather data (handles compression).
    /// </summary>
    private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

    /// <summary>
    /// All predefined weather zones across the simulation map (8x8 km grid).
    /// </summary>
    public List<WeatherZone> AllZones { get; set; } = new();
    /// <summary>
    /// Currently active weather zones (where speed factor is reduced).
    /// </summary>
    public List<WeatherZone> ActiveZones { get; private set; } = new();

    private DateTime _lastUpdateTime = DateTime.MinValue;
    /// <summary>
    /// Interval after which weather data is refreshed (30 minutes).
    /// </summary>
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(30);
    
    /// <summary>
    /// Reference to the simulation's SemiTruck layer to access the simulation time.
    /// </summary>
    public SemiTruckLayer SemiTruckLayer { get; set; }

    public SemiTruckWeatherLayer()
    {
        InitializeBaseZones();
    }
    
    

    /// <summary>
    /// Initializes a grid of 0.5° x 0.5° tiles (approx. 55km² each) covering Germany.
    /// These zones are later used to attach weather data.
    /// </summary>
    private void InitializeBaseZones()
    {
        for (double lat = 47.0; lat <= 55.0; lat += 0.5)
        {
            for (double lon = 6.0; lon <= 15.0; lon += 0.5)
            {
                var coords = new[]
                {
                    new Coordinate(lon, lat),
                    new Coordinate(lon + 0.5, lat),
                    new Coordinate(lon + 0.5, lat + 0.5),
                    new Coordinate(lon, lat + 0.5),
                    new Coordinate(lon, lat)
                };
                AllZones.Add(new WeatherZone
                {
                    Area = _geometryFactory.CreatePolygon(coords)
                });
            }
        }
    }

    /// <summary>
    /// Downloads and parses live weather warnings from the German Weather Service (DWD).
    /// Updates the weather zones with speed impact factors based on event types.
    /// </summary>
    public async Task UpdateWeatherAsync()
    {

        var newWarnings = new List<WeatherZone>();
        var url = "https://s3.eu-central-1.amazonaws.com/app-prod-static.warnwetter.de/v16/warnings_nowcast.json";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            // Console.WriteLine(content[..Math.Min(500, content.Length)] + "...");

            using var doc = JsonDocument.Parse(content);
            if (!doc.RootElement.TryGetProperty("warnings", out var warnings)) return;

            foreach (var warning in warnings.EnumerateArray())
            {
                try
                {
                    // Extract core warning metadata
                    string eventName = warning.GetProperty("event").GetString()?.ToLower() ?? "";
                    long start = warning.GetProperty("start").GetInt64();
                    long end = warning.GetProperty("end").GetInt64();
                    var startTime = DateTimeOffset.FromUnixTimeMilliseconds(start).DateTime;
                    var endTime = DateTimeOffset.FromUnixTimeMilliseconds(end).DateTime;

                    // Determine speed factor based on event type
                    double speedFactor = eventName switch
                    {
                        var e when e.Contains("regen") => 0.9,
                        var e when e.Contains("schnee") => 0.7,
                        var e when e.Contains("glätte") => 0.6,
                        var e when e.Contains("gewitter") => 0.85,
                        var e when e.Contains("sturm") => 0.8,
                        _ => 1.0
                    };

                    // Skip zones with no impact
                    if (speedFactor >= 1.0) continue;
                    if (!warning.TryGetProperty("regions", out var regions)) continue;

                    foreach (var region in regions.EnumerateArray())
                    {
                        if (!region.TryGetProperty("polygonGeometry", out var geom)) continue;

                        var reader = new GeoJsonReader();
                        var geometry = reader.Read<Geometry>(geom.GetRawText());

                        foreach (var poly in geometry is Polygon p ? new[] { p } :
                                 geometry is MultiPolygon mp ? mp.Geometries.Cast<Polygon>() :
                                 Enumerable.Empty<Polygon>())
                        {
                            newWarnings.Add(new WeatherZone
                            {
                                Area = poly,
                                Type = eventName,
                                StartTime = startTime,
                                EndTime = endTime,
                                SpeedFactor = speedFactor
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Weather] Parsing error: {ex.Message}");
                }
            }

            // Reset all zones to normal state
            foreach (var zone in AllZones)
            {
                zone.Type = "normal";
                zone.SpeedFactor = 1.0;
                zone.StartTime = DateTime.MinValue;
                zone.EndTime = DateTime.MaxValue;
            }

            // Apply new warnings to overlapping zones
            foreach (var warningZone in newWarnings)
            {
                foreach (var baseZone in AllZones)
                {
                    if (baseZone.Area.Intersects(warningZone.Area))
                    {
                        baseZone.Type = warningZone.Type;
                        baseZone.SpeedFactor = warningZone.SpeedFactor;
                        baseZone.StartTime = warningZone.StartTime;
                        baseZone.EndTime = warningZone.EndTime;
                    }
                }
            }

            // Update the list of active zones (speed < 100%)
            ActiveZones = AllZones.Where(z => z.SpeedFactor < 1.0).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Weather] API fetch failed: {ex.Message}");
        }
    }

    public void Tick()
    {
        var now = SemiTruckLayer.Context.CurrentTimePoint ?? DateTime.Now;
        if ((now - _lastUpdateTime) >= _updateInterval)
        {
            _lastUpdateTime = now;
            _ = UpdateWeatherAsync(); // fire and forget
        }
    }

   

    public void PreTick() { }
    public void PostTick() { }

    public long GetCurrentTick() => 0;
    public void SetCurrentTick(long currentStep) { }

    /// <summary>
    /// Prints the weather zone that contains a given location.
    /// Helpful for debugging or real-time tracking.
    /// </summary>
    public void PrintWeatherZonesForLocation(double lat, double lon)
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(lon, lat));
        foreach (var zone in AllZones)
        {
            if (zone.Area.Contains(point))
            {
                Console.WriteLine($"[Weather] Zone: {zone.Type}, SpeedFactor: {zone.SpeedFactor}, valid until {zone.EndTime:t}");
                return;
            }
        }
        Console.WriteLine("[Weather] No matching zone found.");
    }

    /// <summary>
    /// Outputs all currently active (non-normal) weather zones with metadata and location.
    /// </summary>
    public void PrintAllActiveWeatherZones()
    {
        Console.WriteLine("[Wetter] Aktive Wetterzonen (≠ normal):");
        foreach (var zone in ActiveZones)
        {
            var centroid = zone.Area.Centroid;
            Console.WriteLine($"Zone: {zone.Type}, SpeedFactor: {zone.SpeedFactor}, " +
                              $"Start: {zone.StartTime:g}, End: {zone.EndTime:g}, " +
                              $"Mittelpunkt: {centroid.Y:F5}, {centroid.X:F5}");
        }
    }
}