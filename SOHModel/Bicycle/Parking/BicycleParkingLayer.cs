using Mars.Common.Core.Random;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Layers;
using SOHModel.Bicycle.Model;
using SOHModel.Domain.Graph;

namespace SOHModel.Bicycle.Parking;

public class BicycleParkingLayer : VectorLayer<BicycleParkingLot>, IBicycleParkingLayer
{
    /// <summary>
    ///     Provides access to the <see cref="ISpatialGraphEnvironment" /> on which the <see cref="Bicycle" />s move.
    /// </summary>
    public ISpatialGraphLayer GraphLayer { get; set; }

    private new IEntityManager EntityManager { get; set; }

    /// <summary>
    ///     Creates a bicycle near given position within given radius with a probability near a parking lot.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="radius">
    ///     Defines the radius within that the bicycle should be located. Uses a random node if no position
    ///     within the radius could be found.
    /// </param>
    /// <param name="useBikeAndRideParkingPercentage">Probability that the bicycle is locked in a parking lot.</param>
    /// <param name="keyAttribute">Identifies the types attribute name of the input file for bicycles.</param>
    /// <param name="type">Identifies the type of bicycle that should be created.</param>
    /// <returns>A bicycle of given type at a node that tries to fit given spatial limitations, on a random node otherwise.</returns>
    public Model.Bicycle CreateOwnBicycleNear(Position position, double radius, double useBikeAndRideParkingPercentage,
        string keyAttribute = "type", string type = "city")
    {
        var bicycle = EntityManager.Create<Model.Bicycle>(keyAttribute, type);
        bicycle.Environment = GraphLayer.Environment;

        ISpatialNode node = null;
        if (RandomHelper.Random.NextDouble() < useBikeAndRideParkingPercentage)
        {
            var bicycleParkingLot = Region(position.PositionArray, radius).FirstOrDefault();
            if (bicycleParkingLot != null)
            {
                node = GraphLayer.Environment.NearestNode(bicycleParkingLot.Position);
                bicycle.BicycleParkingLot = bicycleParkingLot;
            }
        }
        else
        {
            node = FindAnySpatialNode(position, radius);
        }

        node ??= GraphLayer.Environment.GetRandomNode();

        bicycle.Environment.Insert(bicycle, node);
        return bicycle;
    }

    public ModalChoice ModalChoice => ModalChoice.CyclingOwnBike;

    public override bool InitLayer(LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        var initialized = base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        EntityManager = layerInitData?.Container?.Resolve<IEntityManager>();
        return initialized;
    }

    private ISpatialNode FindAnySpatialNode(Position position, double radius)
    {
        var orderedEnumerable = GraphLayer.Environment.NearestNodes(position, radius).ToList();
        // TODO Replace with better implementation
        var count = orderedEnumerable.Count;

        if (count <= 1) return orderedEnumerable.FirstOrDefault();

        var anyIndex = RandomHelper.Random.Next(count - 1);

        return orderedEnumerable.ElementAt(anyIndex);
    }
}