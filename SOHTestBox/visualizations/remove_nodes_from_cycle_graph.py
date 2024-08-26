import geojson
from geojson import FeatureCollection

"""
Remove nodes from Cycle graph so it's less performance intesive to plot.

"""

# remove nodes from graph (only keep edges)
with open('../resources/kellinghusenstrasse_small_walk_graph.geojson') as f:
    routes = geojson.load(f)

features = []

for feature in routes['features']:

    if feature['geometry']['type'] == 'Point':
        continue

    if feature['geometry']['type'] == 'MultiPoint':
        continue

    features.append(feature)

    # if feature['geometry']['type'] == 'LineString':
    #    features.append(feature)

feature_collection = FeatureCollection(features)

with open('cycle_graph.geojson', 'w') as f:
    geojson.dump(feature_collection, f)
