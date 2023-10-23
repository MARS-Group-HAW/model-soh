using System.Linq;
using Mars.Interfaces;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Rental;
using SOHCarModel.Rental;
using SOHDomain.Graph;
using SOHDomain.Model;
using SOHFerryModel.Station;
using SOHMultimodalModel.Multimodal;
using SOHMultimodalModel.Routing;
using SOHTrainModel.Station;

namespace SOHTests.Commons.Layer;

/// <summary>
///     Starts in 2020 with delta t in seconds. Provides the standard implementation of a route finder.
/// </summary>
public class TestMultimodalLayer : AbstractMultimodalLayer, IMultimodalLayer
{
    public TestMultimodalLayer(ISpatialGraphEnvironment sidewalkEnvironment, params IModalLayer[] modalTypeToLayer)
    {
        Context = SimulationContext.Start2020InSeconds;

        SpatialGraphMediatorLayer ??= new SpatialGraphMediatorLayer
        {
            Context = Context,
            Environment = sidewalkEnvironment
        };

        BicycleRentalLayer = modalTypeToLayer.OfType<BicycleRentalLayer>().FirstOrDefault();
        CarRentalLayer = modalTypeToLayer.OfType<CarRentalLayer>().FirstOrDefault();
        FerryStationLayer = modalTypeToLayer.OfType<FerryStationLayer>().FirstOrDefault();
        TrainStationLayer = modalTypeToLayer.OfType<TrainStationLayer>().FirstOrDefault();
    }

    [PropertyDescription] public GatewayLayer GatewayLayer { get; set; }

    /// <summary>
    ///     Holds the environment that can be used for pedestrians to move.
    /// </summary>
    public ISpatialGraphEnvironment SidewalkEnvironment => SpatialGraphMediatorLayer.Environment;

    /// <summary>
    ///     Holds the environment that can be used for street vehicles to move.
    /// </summary>
    public ISpatialGraphEnvironment StreetEnvironment => SpatialGraphMediatorLayer.Environment;

    public new ISimulationContext Context { get; }
}