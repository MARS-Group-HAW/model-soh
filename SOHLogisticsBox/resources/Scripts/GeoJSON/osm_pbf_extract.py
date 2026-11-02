"""
Reads highway ways (motorway|trunk|primary incl. links) from a local OSM extract (Geofabrik .osm.pbf) with pyosmium
and writes a small OSM XML that OSMnx can load via ``ox.graph_from_xml``.
This replaces the Overpass API, whose public server rejects the large border-zone queries.
"""

import xml.sax.saxutils as su

import osmium

KEEP_TAGS = ("highway", "oneway", "junction", "maxspeed", "lanes", "maxheight", "maxlength", "maxweight",
             "maxwidth", "surface", "incline", "ref", "name", "access", "motor_vehicle", "hgv")
HIGHWAYS = {"motorway", "motorway_link", "trunk", "trunk_link", "primary", "primary_link"}


def extract(pbf_paths, out_xml, bbox):
    """bbox = (west, south, east, north); ways with no node inside are skipped."""
    w, s, e, n = bbox
    nodes, ways = {}, []
    for path in pbf_paths:
        fp = osmium.FileProcessor(path, osmium.osm.NODE | osmium.osm.WAY).with_locations().with_filter(
            osmium.filter.KeyFilter("highway"))
        for way in fp:
            if not way.is_way() or way.tags.get("highway") not in HIGHWAYS:
                continue
            refs, coords = [], []
            try:
                for nd in way.nodes:
                    refs.append(nd.ref)
                    coords.append((nd.lon, nd.lat))
            except osmium.InvalidLocationError:
                continue
            if not any(w <= x <= e and s <= y <= n for x, y in coords):
                continue
            nodes.update(zip(refs, coords))
            tags = {k: way.tags[k] for k in KEEP_TAGS if k in way.tags}
            ways.append((way.id, refs, tags))
    with open(out_xml, "w", encoding="utf-8") as f:
        f.write('<?xml version="1.0" encoding="UTF-8"?>\n<osm version="0.6">\n')
        for ref, (x, y) in nodes.items():
            f.write(f'<node id="{ref}" lat="{y}" lon="{x}" version="1"/>\n')
        for wid, refs, tags in ways:
            f.write(f'<way id="{wid}" version="1">\n')
            f.writelines(f'<nd ref="{r}"/>\n' for r in refs)
            f.writelines(f'<tag k="{k}" v={su.quoteattr(v)}/>\n' for k, v in tags.items())
            f.write("</way>\n")
        f.write("</osm>\n")
    return len(nodes), len(ways)
