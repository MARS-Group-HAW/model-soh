using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Common.Core;
using Mars.Common.Core.Collections;
using Mars.Components.Layers;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using Mars.Numerics;
using NetTopologySuite.Geometries;
using SOHDomain.Common;
using Position = Mars.Interfaces.Environments.Position;

namespace SOHMultimodalModel.Layers;

public class MediatorLayer : AbstractLayer
{
    public MediatorLayer(VectorLanduseLayer landUseLayer, VectorPoiLayer poiLayer,
        VectorBuildingsLayer buildingsLayer)
    {
        VectorLandUseLayer = landUseLayer;
        VectorPoiLayer = poiLayer;
        VectorBuildingsLayer = buildingsLayer;
    }

    public VectorLanduseLayer VectorLandUseLayer { get; }

    public VectorPoiLayer VectorPoiLayer { get; }

    public VectorBuildingsLayer VectorBuildingsLayer { get; }


    // Nearest object of given position k-NN (k=1)
    // Random object within radius (window/sphere query + select random object)
    public Position GetNextPoiOfType(Position position, int type, bool takeClosest = true,
        double radius = -1d)
    {
        bool Predicate(IVectorFeature vector)
        {
            vector.VectorStructured.Data.TryGetValue("code", out var typeAsCode);
            return typeAsCode != null && int.Parse(typeAsCode.ToString()) == type;
        }

        var layer = GetLayer(type);

        // var query = new Query
        // {
        //     SelectionClause = new EqualityCondition
        //     {
        //         Attribute = "mappingCode", Value = type
        //     }
        //     // --> POSTGIS = SELECT * FROM mediator_layer WHERE INTERSECT(X,Y) and code = type
        // };

        return GetFor(layer, position, radius, takeClosest, Predicate);
    }

    private IVectorLayer<VectorFeature> GetLayer(int typeCode)
    {
        return typeCode switch
        {
            OsmFeatureCodes.Buildings => VectorBuildingsLayer,
            > 7200 and < 8000 => VectorLandUseLayer,
            _ => VectorPoiLayer
        };
    }

    private static Position GetFor(IVectorLayer<VectorFeature> layer, Position position, double radius,
        bool takeClosest,
        Func<IVectorFeature, bool> predicate)
    {
        IVectorFeature data;

        if (takeClosest)
        {
            data = layer.Nearest(position.PositionArray, predicate);

            if (data != null && radius >= 0)
            {
                var isInRange = data.VectorStructured.Geometry.CoordinateEnumerable()
                    .Any(coordinate =>
                        Distance.Haversine(position.X, position.Y, coordinate.X, coordinate.Y) <= radius);

                return isInRange ? data.VectorStructured.Geometry.Coordinate.ToPosition() : null;
            }
        }
        else
        {
            // TODO: Replace first feature access with random selection.
            data = layer.Features.Where(predicate).FirstOrDefault();
        }

        return data?.VectorStructured.Geometry.Coordinate.ToPosition();
    }

    public Position GetPoIofTypeInRange(Position coordinate, int typeRangeStart, int typeRangeEnd,
        bool takeClosest = true, double maxRadius = -1)
    {
        var typeIds = OsmFeatureCodes.TypesAsList
            .Where(type => type > typeRangeStart && type < typeRangeEnd).ToList();
        return FindPoiPosition(coordinate, typeIds, takeClosest, maxRadius);
    }

    private Position FindPoiPosition(Position position, IList<int> typeIds, bool takeClosest = true,
        double maxRadius = -1)
    {
        if (!typeIds.Any()) throw new ArgumentException("There is no type given for the query.");

        bool Predicate(VectorFeature vector)
        {
            vector.VectorStructured.Data.TryGetValue("code", out var typeAsCode);
            return typeAsCode != null && typeIds.Contains(typeAsCode.Value<int>());
        }

        foreach (var type in typeIds.RandomEnumerable())
        {
            var layer = GetLayer(type);

            var result = takeClosest
                ? layer.Nearest(position.PositionArray, Predicate)
                : layer.Explore(position.PositionArray, maxRadius, Predicate).FirstOrDefault();

            if (result != null)
            {
                if (maxRadius >= 0)
                {
                    if (result.VectorStructured.Geometry is Point point)
                    {
                        if (position.DistanceInMTo(point.X, point.Y) <= maxRadius)
                            return new Position(point.X, point.Y);
                    }
                    else
                    {
                        var centroid = result.VectorStructured.Geometry.Centroid;
                        if (position.DistanceInMTo(centroid.X, centroid.Y) <= maxRadius)
                            return new Position(centroid.X, centroid.Y);
                    }
                }
                else
                {
                    return result.VectorStructured.Geometry.Coordinate.ToPosition();
                }
            }
        }

        return null;
    }


    public Position FindNextNearestLocationForAnyTarget(Position coordinate, IEnumerable<int> typeIds,
        bool takeClosest = true, double maxRadius = -1)
    {
        // Filter out all type codes which are not valid
        var types = typeIds.Where(type => OsmFeatureCodes.TypesAsList.Contains(type)).ToList();

        if (!types.Any())
            throw new ArgumentException("No types provided to query for");

        return FindPoiPosition(coordinate, types, takeClosest, maxRadius);
    }
}