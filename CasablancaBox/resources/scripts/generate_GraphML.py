import osmnx as ox
from shapely import wkt

# your AOI polygon:
AOI_WKT = "POLYGON((-7.6915 33.4825, -7.6915 33.5456, -7.6162 33.5456, -7.6162 33.4825, -7.6915 33.4825))"
aoi = wkt.loads(AOI_WKT)

G_walk = ox.graph_from_polygon(aoi, network_type="walk", simplify=True, retain_all=False, truncate_by_edge=True)
G_drive = ox.graph_from_polygon(aoi, network_type="drive", simplify=True, retain_all=False, truncate_by_edge=True)

ox.save_graphml(G_walk,  "resources/casa_sidi_maarouf_walk.graphml")
ox.save_graphml(G_drive, "resources/casa_sidi_maarouf_drive.graphml")
