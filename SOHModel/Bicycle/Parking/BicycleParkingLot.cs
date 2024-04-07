using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;

namespace SOHModel.Bicycle.Parking;

public class BicycleParkingLot : IVectorFeature
{
    /// <summary>
    ///     The centroid of this rental station.
    /// </summary>
    public Position Position { get; private set; }

    public VectorStructuredData VectorStructured { get; set; }

    public void Init(ILayer layer, VectorStructuredData data)
    {
        var centroid = data.Geometry.Centroid;
        Position = Position.CreatePosition(centroid.X, centroid.Y);
        VectorStructured = data;
    }

    //TODO manage capacity

    public void Update(VectorStructuredData data)
    {
        //do nothing
    }
}