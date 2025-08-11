using Mars.Components.Layers;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Layers;
using SOHModel.SemiTruck.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;
using NetTopologySuite.Geometries;

namespace SOHModel.SemiTruck.RealTimeData
{
    /// <summary>
    /// Represents a closure info object that contains time intervals, type and coordinates of a road closure.
    /// </summary>
    public class ClosureInfo
    {
        public List<TimeInterval> Intervals { get; set; }
        public string Type { get; set; }
        public List<List<double>> Coordinates { get; set; }
    }

    /// <summary>
    /// Represents a warning (traffic jam) information object.
    /// </summary>
    public class WarningInfo
    {
        public List<TimeInterval> Intervals { get; set; }
        public string Type { get; set; }
        public List<List<double>> Coordinates { get; set; }
        public double AverageSpeedKmh { get; set; }
    }

    /// <summary>
    /// Holds begin and end timestamps of a time interval.
    /// </summary>
    public class TimeInterval
    {
        public string Begin { get; set; }
        public string End { get; set; }
    }

    /// <summary>
    /// Real-time data layer for handling scheduled road closures on German Autobahns.
    /// Fetches closure data, parses it and injects it into the SemiTruckLayer for simulation awareness.
    /// </summary>
    public class SemiTruckRealTimeLayer : AbstractLayer, ISteppedActiveLayer
    {
        [PropertyDescription] public SemiTruckLayer SemiTruckLayer { get; set; }

        private static readonly HttpClient httpClient = new HttpClient();
        private const string BaseUrl = "https://verkehr.autobahn.de/o/autobahn";

        // List of all German Autobahns to query for closure information
        private static readonly List<string> AutobahnIds = new()
        {
            "A1", "A2", "A3", "A4", "A5", "A6", "A7", "A8", "A9", "A10", "A11", "A12", "A13", "A14", "A15",
            "A17", "A19", "A20", "A21", "A23", "A24", "A25", "A26", "A27", "A28", "A29", "A30", "A31",
            "A33", "A36", "A37", "A38", "A39", "A40", "A42", "A43", "A44", "A45", "A46", "A48", "A49",
            "A52", "A57", "A59", "A60", "A61", "A62", "A63", "A64", "A65", "A66", "A67", "A70", "A71",
            "A72", "A73", "A81", "A92", "A93", "A94", "A95", "A96", "A98", "A99", "A100", "A111", "A113",
            "A115", "A117", "A143", "A210", "A215", "A226", "A255", "A261", "A270", "A281", "A320",
            "A352", "A369", "A445", "A448", "A480", "A485", "A516", "A524", "A535", "A542", "A544",
            "A553", "A555", "A559", "A560", "A562", "A565", "A573", "A620", "A623", "A640", "A643",
            "A648", "A650", "A659", "A661", "A671", "A831", "A861", "A980", "A995", "A99a"
        };

        private bool firstTickExecuted = false;
        private DateTime lastClosureUpdateTime = DateTime.MinValue;
        private DateTime lastConstructionUpdateTime = DateTime.MinValue;
        private DateTime lastWarningUpdateTime = DateTime.MinValue;

        private static readonly TimeSpan ClosureUpdateInterval = TimeSpan.FromMinutes(60);
        private static readonly TimeSpan ConstructionUpdateInterval = TimeSpan.FromHours(24);
        private static readonly TimeSpan WarningUpdateInterval = TimeSpan.FromHours(1);
        private bool isClosure;
        private bool isConstruction;

        // Avoid reprocessing known closures
        private readonly HashSet<string> knownClosures = new();
        private readonly HashSet<string> knownWarnings = new();


        /// <summary>
        /// Called by the simulation every tick. Periodically updates known closures.
        /// </summary>
        public void Tick()
        {
            var currentTime = SemiTruckLayer.Context.CurrentTimePoint ?? DateTime.MinValue;

            // Initialize timestamps on first run
            if (!firstTickExecuted)
            {
                firstTickExecuted = true;
                lastClosureUpdateTime = currentTime - ClosureUpdateInterval;
                lastConstructionUpdateTime = currentTime - ConstructionUpdateInterval;
                lastWarningUpdateTime = currentTime - WarningUpdateInterval;
            }

            // Determine whether it is time to update closures/constructions
            bool updateClosures = currentTime - lastClosureUpdateTime >= ClosureUpdateInterval;
            bool updateConstructions = currentTime - lastConstructionUpdateTime >= ConstructionUpdateInterval;
            bool updateWarnings = currentTime - lastWarningUpdateTime >= WarningUpdateInterval;

            if (!updateClosures && !updateConstructions && !updateWarnings) return;

            foreach (var autobahnId in AutobahnIds)
            {
                if (updateClosures || updateConstructions)
                {
                    // Fetch and parse closure data for each Autobahn
                    var closures = FetchAndParseRoadAsync(autobahnId).GetAwaiter().GetResult();

                    foreach (var closure in closures)
                    {
                        if (closure.Intervals == null || closure.Coordinates == null)
                            continue;

                        foreach (var interval in closure.Intervals)
                        {
                            if (interval.Begin == null || interval.End == null) continue;

                            var startTime = DateTime.Parse(interval.Begin);
                            var endTime = DateTime.Parse(interval.End);

                            // Convert to NTS Coordinates for spatial use
                            var coords = closure.Coordinates
                                .Select(pair => new Coordinate(pair[0], pair[1]))
                                .ToList();

                            // Create a unique key for this closure to avoid reprocessing
                            string coordsKey = string.Join(";", coords.Select(c => $"{c.X:F6},{c.Y:F6}"));
                            string closureKey = $"{autobahnId}|{startTime:o}|{endTime:o}|{coordsKey}";

                            if (knownClosures.Contains(closureKey)) continue;

                            if (isClosure && updateClosures)
                            {
                                knownClosures.Add(closureKey);

                                var block = new SemiTruckLayer.ScheduledRoadClosureByCoordinates(
                                    id: Guid.NewGuid().ToString(),
                                    startTime: startTime,
                                    endTime: endTime,
                                    coordinates: coords
                                );
                                SemiTruckLayer.ScheduledClosuresByCoordinates.Add(block);
                            }
                            // Uncomment to add speed reduction for construction sites
                            // else if (isConstruction && updateConstructions)
                            // {
                            //     knownClosures.Add(closureKey);
                            //
                            //     var block = new SemiTruckLayer.ScheduledSpeedReductionByCoordinates(
                            //         id: Guid.NewGuid().ToString(),
                            //         startTime: startTime,
                            //         endTime: endTime,
                            //         coordinates: coords,
                            //         reducedSpeedKmh: 60
                            //     );
                            //     SemiTruckLayer.ScheduledSpeedReductionsByCoordinates.Add(block);
                            // }
                        }
                    }
                }

                // Warnings (Staus) -> speed reductions
                if (updateWarnings)
                {
                    var warnings = FetchAndParseWarningAsync(autobahnId).GetAwaiter().GetResult();

                    foreach (var warning in warnings)
                    {
                        if (warning.Intervals == null || warning.Coordinates == null) continue;

                        // take all intervals; for warnings we ensure End = Begin + 1h in parser
                        foreach (var interval in warning.Intervals)
                        {
                            if (interval.Begin == null || interval.End == null) continue;

                            var startTime = DateTime.Parse(interval.Begin);
                            var endTime = DateTime.Parse(interval.End);

                            var coords = warning.Coordinates
                                .Select(pair => new Coordinate(pair[0], pair[1]))
                                .ToList();

                            string coordsKey = string.Join(";", coords.Select(c => $"{c.X:F6},{c.Y:F6}"));
                            string warnKey =
                                $"WARN|{autobahnId}|{startTime:o}|{endTime:o}|{coordsKey}|{Math.Round(warning.AverageSpeedKmh)}";

                            if (knownWarnings.Contains(warnKey)) continue;

                            knownWarnings.Add(warnKey);

                            var block = new SemiTruckLayer.ScheduledSpeedReductionByCoordinates(
                                id: Guid.NewGuid().ToString(),
                                startTime: startTime,
                                endTime: endTime,
                                coordinates: coords,
                                reducedSpeedKmh: (int)Math.Max(1, Math.Round(warning.AverageSpeedKmh))
                            );
                            SemiTruckLayer.ScheduledSpeedReductionsByCoordinates.Add(block);
                        }
                    }
                }
            }

            // Update last fetch timestamps
            if (updateClosures) lastClosureUpdateTime = currentTime;
            if (updateConstructions) lastConstructionUpdateTime = currentTime;
            if (updateWarnings) lastWarningUpdateTime = currentTime;
        }

        public void PreTick()
        {
        }

        public void PostTick()
        {
        }

        /// <summary>
        /// Fetches closure data from the Autobahn API and parses it into a list of closures.
        /// </summary>
        public async Task<List<ClosureInfo>> FetchAndParseRoadAsync(string roadId)
        {
            var closuresList = new List<ClosureInfo>();
            var url = $"{BaseUrl}/{roadId}/services/closure";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("closure", out var closures)) return closuresList;

                foreach (var closure in closures.EnumerateArray())
                {
                    if (!closure.TryGetProperty("description", out var descriptionElement)) continue;

                    var descLines = descriptionElement.EnumerateArray()
                        .Select(e => e.GetString()).ToList();

                    var timeBlock = ExtractTimeBlock(descLines);
                    var lastBlock = ExtractLastBlock(descLines);

                    if (lastBlock.Count == 0) continue;

                    string lastLine = lastBlock[^1];
                    var lower = lastLine.ToLower();
                    // Determine if it's a closure or construction based on description keywords
                    isClosure = lower.Contains("sperr");
                    isConstruction = lower.Contains("baustelle") ||
                                     lower.Contains("sanierung") ||
                                     lower.Contains("instandsetzung") ||
                                     lower.Contains("bau") ||
                                     lower.Contains("arbeit");

                    // Currently only process closures due to resources (construction handling can be enabled if needed)
                    // if (!isClosure && !isConstruction) continue;
                    if (!isClosure) continue;
                    var intervals = ParseTimeLines(timeBlock);

                    // Parse geometry data if available
                    var coords = new List<List<double>>();
                    if (closure.TryGetProperty("geometry", out var geom) &&
                        geom.TryGetProperty("coordinates", out var coordinates))
                    {
                        foreach (var coord in coordinates.EnumerateArray())
                        {
                            coords.Add(new List<double> { coord[0].GetDouble(), coord[1].GetDouble() });
                        }
                    }

                    closuresList.Add(new ClosureInfo
                    {
                        Intervals = intervals,
                        Type = lastLine,
                        Coordinates = coords
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching data for {roadId}: {ex.Message}");
            }

            return closuresList;
        }

        /// <summary>
        /// Fetches traffic jams (Staus) from the Autobahn API and parses it into a list of jams.
        /// </summary>
        public async Task<List<WarningInfo>> FetchAndParseWarningAsync(string roadId)
        {
            var warningsList = new List<WarningInfo>();
            var url = $"{BaseUrl}/{roadId}/services/warning";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Accept", "application/json");

                var response = await httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("warning", out var warnings) ||
                    warnings.ValueKind != JsonValueKind.Array)
                {
                    return warningsList;
                }

                foreach (var warning in warnings.EnumerateArray())
                {
                    // Read description
                    List<string> descLines = new();
                    if (warning.TryGetProperty("description", out var descriptionElement) &&
                        descriptionElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var e in descriptionElement.EnumerateArray())
                            descLines.Add(e.GetString() ?? string.Empty);
                    }

                    var timeBlock = ExtractTimeBlock(descLines);
                    var intervals = ParseTimeLines(timeBlock);

                    // For traffic jams with no endtime, Endtime = startTime + 1h after that we check the API again
                    foreach (var interval in intervals)
                    {
                        if (!string.IsNullOrWhiteSpace(interval.Begin) && string.IsNullOrWhiteSpace(interval.End))
                        {
                            var begin = DateTime.Parse(interval.Begin);
                            interval.End = begin.AddHours(1).ToString("o");
                        }
                    }
                    
                    // In case no time is in the information we take currentTime as start and +1h as endTime
                    if (intervals.Count == 0)
                    {
                        var now = (SemiTruckLayer?.Context?.CurrentTimePoint) ?? DateTime.UtcNow;
                        intervals.Add(new TimeInterval
                        {
                            Begin = now.ToString("o"),
                            End = now.AddHours(1).ToString("o")
                        });
                    }

                    // Read geometry
                    var coords = new List<List<double>>();
                    if (warning.TryGetProperty("geometry", out var geom) &&
                        geom.TryGetProperty("coordinates", out var coordinates) &&
                        coordinates.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var coord in coordinates.EnumerateArray())
                        {
                            if (coord.ValueKind == JsonValueKind.Array && coord.GetArrayLength() >= 2)
                            {
                                var lon = coord[0].GetDouble();
                                var lat = coord[1].GetDouble();
                                coords.Add(new List<double> { lon, lat });
                            }
                        }
                    }
                    else
                    {
                        continue;
                    }

                    // Ignore if only one coordinate 
                    if (coords.Count <= 1)
                    {
                        continue;
                    }

                    // Read averageSpeed
                    double? avgSpeed = null;
                    if (warning.TryGetProperty("averageSpeed", out var avgElement))
                    {
                        if (avgElement.ValueKind == JsonValueKind.String)
                        {
                            var s = avgElement.GetString();
                            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var valStr))
                                avgSpeed = valStr;
                        }
                        else if (avgElement.ValueKind == JsonValueKind.Number &&
                                 avgElement.TryGetDouble(out var valNum))
                        {
                            avgSpeed = valNum;
                        }
                    }

                    if (avgSpeed is null)
                    {
                        continue;
                    }

                    // Determine Type
                    string typeLine = "";
                    var lastBlock = ExtractLastBlock(descLines);
                    if (lastBlock.Count > 0) typeLine = lastBlock[^1];

                    warningsList.Add(new WarningInfo
                    {
                        Intervals = intervals,
                        Type = typeLine,
                        Coordinates = coords,
                        AverageSpeedKmh = avgSpeed.Value
                    });
                    
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching warnings for {roadId}: {ex.Message}");
            }

            return warningsList;
        }


        /// <summary>
        /// Extracts the first block of time-related lines from a closure description.
        /// </summary>
        private List<string> ExtractTimeBlock(List<string> lines)
        {
            var block = new List<string>();
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) break;
                block.Add(line.Trim());
            }

            return block;
        }

        /// <summary>
        /// Extracts the last non-empty block from a list of lines.
        /// </summary>
        private List<string> ExtractLastBlock(List<string> lines)
        {
            var blocks = new List<List<string>>();
            var current = new List<string>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed == "")
                {
                    if (current.Count > 0)
                    {
                        blocks.Add(new List<string>(current));
                        current.Clear();
                    }
                }
                else
                {
                    current.Add(trimmed);
                }
            }

            if (current.Count > 0)
                blocks.Add(current);

            return blocks.Count > 0 ? blocks[^1] : new List<string>();
        }

        /// <summary>
        /// Parses date and time information from a list of strings into standardized time intervals.
        /// </summary>
        private List<TimeInterval> ParseTimeLines(List<string> lines)
        {
            var intervals = new List<TimeInterval>();

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                // Format: "Beginn: 01.01.24 um 14:00 Uhr"
                var matchBegin = Regex.Match(trimmed, @"Beginn:\s*(\d{2}\.\d{2}\.\d{2})\s*um\s*(\d{2}:\d{2}) Uhr");
                if (matchBegin.Success)
                {
                    var date = matchBegin.Groups[1].Value;
                    var time = NormalizeTime(matchBegin.Groups[2].Value);
                    var dt = DateTime.ParseExact($"{date} {time}", "dd.MM.yy HH:mm", null);
                    intervals.Add(new TimeInterval { Begin = dt.ToString("o"), End = null });
                    continue;
                }

                // Format: "Ende: 02.01.24 um 06:00 Uhr"
                var matchEnd = Regex.Match(trimmed, @"Ende:\s*(\d{2}\.\d{2}\.\d{2})\s*um\s*(\d{2}:\d{2}) Uhr");
                if (matchEnd.Success && intervals.Count > 0 && intervals[^1].End == null)
                {
                    var date = matchEnd.Groups[1].Value;
                    var time = NormalizeTime(matchEnd.Groups[2].Value);
                    var dt = DateTime.ParseExact($"{date} {time}", "dd.MM.yy HH:mm", null);
                    intervals[^1].End = dt.ToString("o");
                    continue;
                }

                // Format: "01.01.24 von 14:00 bis 18:00"
                var matchSimple = Regex.Match(trimmed, @"(\d{2}\.\d{2}\.\d{2}) von (\d{2}:\d{2}) bis (\d{2}:\d{2})");
                if (matchSimple.Success)
                {
                    var date = matchSimple.Groups[1].Value;
                    var start = NormalizeTime(matchSimple.Groups[2].Value);
                    var end = NormalizeTime(matchSimple.Groups[3].Value);
                    var dtDate = DateTime.ParseExact(date, "dd.MM.yy", null);
                    var begin = dtDate.Date + TimeSpan.Parse(start);
                    var endDt = dtDate.Date + TimeSpan.Parse(end);
                    intervals.Add(new TimeInterval { Begin = begin.ToString("o"), End = endDt.ToString("o") });
                    continue;
                }

                // Format: "01.01.24 14:00 bis zum 02.01.24 06:00"
                var matchCross = Regex.Match(trimmed,
                    @"(\d{2}\.\d{2}\.\d{2}) (\d{2}:\d{2}) bis zum (\d{2}\.\d{2}\.\d{2}) (\d{2}:\d{2})");
                if (matchCross.Success)
                {
                    var d1 = matchCross.Groups[1].Value;
                    var t1 = matchCross.Groups[2].Value;
                    var d2 = matchCross.Groups[3].Value;
                    var t2 = matchCross.Groups[4].Value;
                    var begin = DateTime.ParseExact($"{d1} {t1}", "dd.MM.yy HH:mm", null);
                    var end = DateTime.ParseExact($"{d2} {t2}", "dd.MM.yy HH:mm", null);
                    intervals.Add(new TimeInterval { Begin = begin.ToString("o"), End = end.ToString("o") });
                    continue;
                }
            }

            return intervals;
        }

        /// <summary>
        /// Normalizes time strings. Converts 24:00 to 00:00 to avoid DateTime.Parse errors.
        /// </summary>
        private string NormalizeTime(string time)
        {
            return time.Trim() == "24:00" ? "00:00" : time;
        }
    }
}