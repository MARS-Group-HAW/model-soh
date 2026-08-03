using System;
using System.IO;

namespace SOHTests.SOHLogisticsTests
{
    /// <summary>
    /// Shared path resolution for the Germany highway GeoJSON used by logistics network tests.
    /// The file is intentionally gitignored (too large for the repo); tests should skip when absent.
    /// </summary>
    internal static class GeoJsonTestResources
    {
        public const string Elevation08FileName = "autobahn_und_bundesstrassen_deutschland_elevation_08.geojson";

        public const string MissingSkipReason =
            "Germany road network GeoJSON is not present (gitignored / not committed; too large). " +
            "Extract SOHLogisticsBox/resources/autobahn_und_bundesstrassen_deutschland.rar locally, " +
            "or obtain autobahn_und_bundesstrassen_deutschland_elevation_08.geojson, to run these tests.";

        public static string ResolveElevation08Path()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
            return Path.Combine(projectRoot, "SOHLogisticsBox", "resources", Elevation08FileName);
        }
    }
}
