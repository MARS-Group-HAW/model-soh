using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Core.Data.Wrapper.Memory;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation;

namespace SOHMultimodalModel.Output.Trips;

[Serializable]
public class TripsLine : LineString
{
    /// <summary>
    ///     The points of this <c>LineString</c>.
    /// </summary>
    private TripPosition[] _points;

    /// <summary>
    ///     Initializes a new instance of the <see cref="LineString" /> class.
    /// </summary>
    /// <remarks>
    ///     For create this <see cref="Geometry" /> is used a standard <see cref="GeometryFactory" />
    ///     with <see cref="PrecisionModel" /> <c> == </c> <see cref="PrecisionModels.Floating" />.
    /// </remarks>
    /// <param name="points">The coordinates used for create this <see cref="LineString" />.</param>
    /// <exception cref="ArgumentException">If too few points are provided</exception>
    public TripsLine(TripPosition[] points) : base(points)
    {
        _points = points;
    }

    /// <summary>
    ///     Gets a value to sort the geometry
    /// </summary>
    /// <remarks>
    ///     NOTE:<br />
    ///     For JTS v1.17 this property's getter has been renamed to <c>getTypeCode()</c>.
    ///     In order not to break binary compatibility we did not follow.
    /// </remarks>
    protected override SortIndexValue SortIndex => SortIndexValue.LineString;

    /// <summary>
    ///     Returns an array containing the values of all the vertices for
    ///     this geometry.
    /// </summary>
    public override Coordinate[] Coordinates => _points;

    /// <summary>
    /// </summary>
    public override Coordinate Coordinate => IsEmpty ? null : _points[0];

    /// <summary>
    /// </summary>
    public override Dimension Dimension => Dimension.Curve;

    /// <summary>
    /// </summary>
    public override Dimension BoundaryDimension
    {
        get
        {
            if (IsClosed) return Dimension.False;
            return Dimension.Point;
        }
    }

    /// <summary>
    /// </summary>
    public override bool IsEmpty => _points.Length == 0;

    /// <summary>
    /// </summary>
    public override int NumPoints => _points.Length;


    /// <summary>
    ///     Gets a value indicating if this <c>LINESTRING</c> is closed.
    /// </summary>
    public override bool IsClosed => !IsEmpty && GetCoordinateN(0).Equals2D(GetCoordinateN(NumPoints - 1));


    /// <summary>
    ///     Returns the name of this object's interface.
    /// </summary>
    /// <returns>"LineString"</returns>
    public override string GeometryType => TypeNameLineString;

    /// <summary>
    ///     Gets the OGC geometry type
    /// </summary>
    public override OgcGeometryType OgcGeometryType => OgcGeometryType.LineString;

    /// <summary>
    ///     Returns the boundary, or an empty geometry of appropriate dimension
    ///     if this <c>Geometry</c> is empty.
    ///     For a discussion of this function, see the OpenGIS Simple
    ///     Features Specification. As stated in SFS Section 2.1.13.1, "the boundary
    ///     of a Geometry is a set of Geometries of the next lower dimension."
    /// </summary>
    /// <returns>The closure of the combinatorial boundary of this <c>Geometry</c>.</returns>
    public override Geometry Boundary => new BoundaryOp(this).GetBoundary();

    /// <summary>
    ///     Gets an array of <see cref="double" /> ordinate values
    /// </summary>
    /// <param name="ordinate">The ordinate index</param>
    /// <returns>An array of ordinate values</returns>
    public override double[] GetOrdinates(Ordinate ordinate)
    {
        return null;
    }

    /// <summary>
    /// </summary>
    /// <returns></returns>
    protected override Envelope ComputeEnvelopeInternal()
    {
        if (IsEmpty)
            return new Envelope();

        //Convert to array, then access array directly, to avoid the function-call overhead
        //of calling Getter millions of times. ToArray may be inefficient for
        //non-BasicCoordinateSequence CoordinateSequences. [Jon Aquino]
        var coordinates = _points;
        var minx = coordinates[0].X;
        var miny = coordinates[0].Y;
        var maxx = coordinates[0].X;
        var maxy = coordinates[0].Y;
        for (var i = 1; i < coordinates.Length; i++)
        {
            minx = minx < coordinates[i].X ? minx : coordinates[i].X;
            maxx = maxx > coordinates[i].X ? maxx : coordinates[i].X;
            miny = miny < coordinates[i].Y ? miny : coordinates[i].Y;
            maxy = maxy > coordinates[i].Y ? maxy : coordinates[i].Y;
        }

        return new Envelope(minx, maxx, miny, maxy);
    }

    /// <summary>
    /// </summary>
    /// <param name="other"></param>
    /// <param name="tolerance"></param>
    /// <returns></returns>
    public override bool EqualsExact(Geometry other, double tolerance)
    {
        if (!IsEquivalentClass(other))
            return false;

        var otherLineString = (LineString)other;
        if (_points.Length != otherLineString.NumPoints)
            return false;

        var cec = Factory.GeometryServices.CoordinateEqualityComparer;
        return !_points.Where((t, i) =>
            !cec.Equals(t, otherLineString.GetCoordinateN(i), tolerance)).Any();
    }

    /// <summary>
    /// </summary>
    /// <param name="filter"></param>
    public override void Apply(ICoordinateFilter filter)
    {
        foreach (var t in _points) filter.Filter(t);
    }

    /// <summary>
    ///     Performs an operation on the coordinates in this <c>Geometry</c>'s.
    /// </summary>
    /// <remarks>
    ///     If the filter reports that a coordinate value has been changed,
    ///     <see cref="Geometry.GeometryChanged" /> will be called automatically.
    /// </remarks>
    /// <param name="filter">The filter to apply</param>
    public override void Apply(ICoordinateSequenceFilter filter)
    {
    }

    /// <summary>
    ///     Performs an operation with or on this <c>Geometry</c> and its
    ///     subelement <c>Geometry</c>s (if any).
    /// </summary>
    /// <param name="filter">
    ///     The filter to apply to this <c>Geometry</c> (and
    ///     its children, if it is a <c>GeometryCollection</c>).
    /// </param>
    public override void Apply(IGeometryFilter filter)
    {
        filter.Filter(this);
    }

    /// <summary>
    ///     Performs an operation with or on this Geometry and its
    ///     component Geometry's. Only GeometryCollections and
    ///     Polygons have component Geometry's; for Polygons they are the LinearRings
    ///     of the shell and holes.
    /// </summary>
    /// <param name="filter">The filter to apply to this <c>Geometry</c>.</param>
    public override void Apply(IGeometryComponentFilter filter)
    {
        filter.Filter(this);
    }

    /// <inheritdoc cref="Geometry.CopyInternal" />
    /// >
    protected override Geometry CopyInternal()

    {
        var points = (TripPosition[])_points.Clone();
        return new TripsLine(points);
    }

    /// <summary>
    ///     Normalizes a <c>LineString</c>.  A normalized <c>LineString</c>
    ///     has the first point which is not equal to it's reflected point
    ///     less than the reflected point.
    /// </summary>
    public override void Normalize()
    {
    }

    /// <inheritdoc cref="Geometry.IsEquivalentClass" />
    protected override bool IsEquivalentClass(Geometry other)
    {
        return other is LineString;
    }

    /// <inheritdoc cref="Geometry.CompareToSameClass(object)" />
    protected override int CompareToSameClass(object o)
    {
        return 0;
    }

    protected override int CompareToSameClass(object o, IComparer<CoordinateSequence> comp)
    {
        return 0;
    }
}