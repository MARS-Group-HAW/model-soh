using System.IO;

namespace SOHTests;

public static class ResourcesConstants
{
    #region folder

    public static readonly string NetworkFolder = Path.Combine("res", "networks");
    private static readonly string VectorDataFolder = Path.Combine("res", "vector_data");
    public static readonly string AgentInitsFolder = Path.Combine("res", "agent_inits");
    private static readonly string EntityInitsFolder = Path.Combine("res", "entity_inits");
    public static readonly string TrafficLightsFolder = Path.Combine("res", "traffic_lights");

    #endregion

    #region agent entity

    public static readonly string CarCsv = Path.Combine(EntityInitsFolder, "car.csv");
    public static readonly string BicycleCsv = Path.Combine(EntityInitsFolder, "bicycle.csv");
    public static readonly string FerryCsv = Path.Combine(EntityInitsFolder, "ferry.csv");
    public static readonly string TrainCsv = Path.Combine(EntityInitsFolder, "train.csv");
    public static readonly string BusCsv = Path.Combine(EntityInitsFolder, "bus.csv");
    public static readonly string Bus113LineCsv = Path.Combine(EntityInitsFolder, "bus_113_line.csv");
    public static readonly string BigEventBusLinesCsv = Path.Combine(EntityInitsFolder, "big_event_bus_lines.csv");
    public static readonly string FerryLineCsv = Path.Combine(EntityInitsFolder, "ferry_line.csv");
    public static readonly string TrainU1LineCsv = Path.Combine(EntityInitsFolder, "train_u1_line.csv");
    public static readonly string FerryDriverCsv = Path.Combine(AgentInitsFolder, "ferry_driver.csv");
    public static readonly string TestFerryDriverCsv = Path.Combine(AgentInitsFolder, "ferry_driver_test.csv");
    public static readonly string DockWorkerCsv = Path.Combine(AgentInitsFolder, "dock_worker.csv");
    public static readonly string DockWorkerComplexCsv = Path.Combine(AgentInitsFolder, "dock_worker_complex.csv");
    public static readonly string BusPassengerCsv = Path.Combine(AgentInitsFolder, "bus_passenger_spawning.csv");


    #endregion

    #region traffic_ligths

    public static readonly string TrafficLightsAltona =
        Path.Combine(TrafficLightsFolder, "traffic_lights_altona.zip");

    public static readonly string TrafficLightGreenVeddelerDamm =
        Path.Combine(TrafficLightsFolder, "traffic_light_green_veddeler_damm.csv");

    public static readonly string TrafficLightRedVeddelerDamm =
        Path.Combine(TrafficLightsFolder, "traffic_light_red_veddeler_damm.csv");

    #endregion

    #region network

    public static readonly string RingNetwork = Path.Combine(NetworkFolder, "ring_network.geojson");

    public static readonly string TriangleNetwork = Path.Combine(NetworkFolder, "triangle_line_string.geojson");

    public static readonly string DriveGraphVeddelerDamm =
        Path.Combine(NetworkFolder, "drive_graph_veddeler_damm.geojson");

    public static readonly string FerryGraph =
        Path.Combine(NetworkFolder, "ferry_graph_hamburg.geojson");

    public static readonly string TrainU1Graph =
        Path.Combine(NetworkFolder, "hamburg_u1_graph.geojson");

    public static readonly string TrainU1NorthGraph =
        Path.Combine(NetworkFolder, "hamburg_u1_north_graph.geojson");

    public static readonly string Bus113Graph =
        Path.Combine(NetworkFolder, "hamburg_bus_113_graph.geojson");

    public static readonly string BigEventBusGraph =
        Path.Combine(NetworkFolder, "big_event_bus_graph.geojson");

    public static readonly string FerryContainerWalkingGraph =
        Path.Combine(NetworkFolder, "hamburg_south_graph_filtered.geojson");

    public static readonly string DriveGraphAltonaAltstadt =
        Path.Combine(NetworkFolder, "drive_graph_altona_altstadt.graphml");

    public static readonly string WalkGraphAltonaAltstadt =
        Path.Combine(NetworkFolder, "walk_graph_altona_altstadt.graphml");

    public static readonly string WalkGraphBus113Test = Path.Combine(NetworkFolder, "walk_graph_bustest113.geojson");

    public static readonly string WalkGraphLandungsbruecken =
        Path.Combine(NetworkFolder, "walk_graph_landungsbruecken_fischmarkt.geojson");

    public static readonly string DriveGraphFourWayIntersection =
        Path.Combine(NetworkFolder, "drive_graph_intersection.graphml");

    public static readonly string HamburgRailStationAreasDriveGraph =
        Path.Combine(NetworkFolder, "hamburg_rail_station_areas_drive_graph.geojson");

    #endregion

    #region vector

    public static readonly string TrainStationsU1 =
        Path.Combine(VectorDataFolder, "gateway", "hamburg_u1_stations.geojson");

    public static readonly string BusStations113 =
        Path.Combine(VectorDataFolder, "gateway", "hamburg_bus_113_stations.geojson");

    public static readonly string BigEventBusStations =
        Path.Combine(NetworkFolder, "big_event_bus_graph.geojson");

    public static readonly string FerryStations =
        Path.Combine(VectorDataFolder, "gateway", "hamburg_ferry_stations.geojson");

    public static readonly string ParkingAltonaAltstadt =
        Path.Combine(VectorDataFolder, "parking_altona_altstadt.geojson");

    public static readonly string RailroadStations =
        Path.Combine(VectorDataFolder, "gateway", "hamburg_railroad_stations.geojson");

    public static readonly string BicycleRentalAltonaAltstadt =
        Path.Combine(VectorDataFolder, "bicycle_rental_altona_altstadt.geojson");

    public static readonly string BuildingsAltonaAltstadt =
        Path.Combine(VectorDataFolder, "buildings_altona_altstadt.geojson");

    public static readonly string PoisAltonaAltstadt =
        Path.Combine(VectorDataFolder, "pois_altona_altstadt.geojson");

    public static readonly string LanduseAltonaAltstadt =
        Path.Combine(VectorDataFolder, "land_use_altona_altstadt.geojson");

    #endregion
}