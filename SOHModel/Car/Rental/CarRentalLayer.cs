using Mars.Components.Environments;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Car.Model;
using SOHModel.Car.Parking;
using SOHModel.Domain.Graph;

namespace SOHModel.Car.Rental;

/// <summary>
///     The <code>CarRentalLayer</code> capsules the access to all <code>RentalCar</code>s.
/// </summary>
public class CarRentalLayer : AbstractLayer, ICarRentalLayer
{
    private string _carKeyAttributeName;
    private string _carValueToMatch;

    private IEntityManager _entityManager;

    [PropertyDescription(Name = "carKeyAttributeName")]
    public string CarKeyAttributeName
    {
        get => _carKeyAttributeName ?? "type";
        set => _carKeyAttributeName = value;
    }

    [PropertyDescription(Name = "carValueToMatch")]
    public string CarValueToMatch
    {
        get => _carValueToMatch ?? "Golf";
        set => _carValueToMatch = value;
    }

    private GeoHashEnvironment<RentalCar> HashEnvironment { get; set; }

    /// <summary>
    ///     Provides access to the <see cref="ISpatialGraphEnvironment" /> on which the <see cref="Car" />s move.
    /// </summary>
    public ISpatialGraphLayer StreetLayer { get; set; }

    public RentalCar Nearest(Position position)
    {
        return HashEnvironment.Explore(position).FirstOrDefault();
    }

    public bool Insert(RentalCar car)
    {
        return HashEnvironment.Insert(car);
    }

    public bool Remove(RentalCar car)
    {
        return HashEnvironment.Remove(car);
    }

    public ModalChoice ModalChoice => ModalChoice.CarRentalDriving;

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        var initialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        _entityManager = layerInitData?.Container?.Resolve<IEntityManager>();
        if (_entityManager == null)
            throw new ArgumentException(
                $"{nameof(CarParkingLayer)} requires {nameof(LayerInitData)} to resolve {nameof(IEntityManager)}");
        if (StreetLayer == null)
            throw new ArgumentException($"{nameof(CarParkingLayer)} requires {nameof(StreetLayer)}");

        var environment = StreetLayer.Environment;
        HashEnvironment = GeoHashEnvironment<RentalCar>.BuildByBBox(environment.BoundingBox, -1);

        if (layerInitData?.LayerInitConfig.File != null || layerInitData?.LayerInitConfig.Value != null)
        {
            //TODO if raster data or different data?
            var vectorLayer = new VectorLayer();
            vectorLayer.InitLayer(layerInitData);
            foreach (var entity in vectorLayer.Features)
            {
                var centroid = entity.VectorStructured.Geometry.Centroid;
                var rentalCar = EntityManager.Create<RentalCar>(CarKeyAttributeName, CarValueToMatch);
                rentalCar.Position = Position.CreateGeoPosition(centroid.X, centroid.Y);
                rentalCar.Environment = environment;
                environment.Insert(rentalCar, environment.NearestNode(rentalCar.Position));
                Insert(rentalCar);
            }
        }


        return initialized;
    }
}