using System.Text.Json;
using Mars.Components.Layers;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using SOHModel.SemiTruck.Model;

namespace SOHModel.SemiTruck.RealTimeData;

public class WeatherZone
{
    public Polygon Area { get; set; }
    public string Type { get; set; } = "normal";
    public DateTime StartTime { get; set; } = DateTime.MinValue;
    public DateTime EndTime { get; set; } = DateTime.MaxValue;
    public double SpeedFactor { get; set; } = 1.0; // 1.0 = normal
}

public class SemiTruckWeatherLayer : AbstractLayer, ISteppedActiveLayer
{
    private readonly GeometryFactory _geometryFactory = new GeometryFactory();
    private readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    });

    public List<WeatherZone> AllZones { get; set; } = new();
    public List<WeatherZone> ActiveZones { get; private set; } = new();

    private DateTime _lastUpdateTime = DateTime.MinValue;
    private readonly TimeSpan _updateInterval = TimeSpan.FromMinutes(30);
    
    
    public SemiTruckLayer SemiTruckLayer { get; set; }

    public SemiTruckWeatherLayer()
    {
        InitializeBaseZones();
    }
    
    

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
                    string eventName = warning.GetProperty("event").GetString()?.ToLower() ?? "";
                    long start = warning.GetProperty("start").GetInt64();
                    long end = warning.GetProperty("end").GetInt64();
                    var startTime = DateTimeOffset.FromUnixTimeMilliseconds(start).DateTime;
                    var endTime = DateTimeOffset.FromUnixTimeMilliseconds(end).DateTime;

                    double speedFactor = eventName switch
                    {
                        var e when e.Contains("regen") => 0.9,
                        var e when e.Contains("schnee") => 0.7,
                        var e when e.Contains("glätte") => 0.6,
                        var e when e.Contains("gewitter") => 0.85,
                        var e when e.Contains("sturm") => 0.8,
                        _ => 1.0
                    };

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
                    Console.WriteLine($"[Wetter] Fehler beim Parsen: {ex.Message}");
                }
            }

            foreach (var zone in AllZones)
            {
                zone.Type = "normal";
                zone.SpeedFactor = 1.0;
                zone.StartTime = DateTime.MinValue;
                zone.EndTime = DateTime.MaxValue;
            }

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

            ActiveZones = AllZones.Where(z => z.SpeedFactor < 1.0).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Wetter] Fehler beim Abruf: {ex.Message}");
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

    public void PrintWeatherZonesForLocation(double lat, double lon)
    {
        var point = _geometryFactory.CreatePoint(new Coordinate(lon, lat));
        foreach (var zone in AllZones)
        {
            if (zone.Area.Contains(point))
            {
                Console.WriteLine($"[Wetter] In Zone: {zone.Type}, SpeedFactor: {zone.SpeedFactor}, gültig bis {zone.EndTime:t}");
                return;
            }
        }

        Console.WriteLine("[Wetter] Keine passende Zone gefunden.");
    }

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