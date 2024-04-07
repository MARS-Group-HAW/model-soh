using Mars.Common;
using Mars.Common.Core.Collections;
using Mars.Common.Core.Random;
using Mars.Components.Layers;
using Mars.Interfaces.Environments;

namespace SOHModel.Multimodal.Layers;

public class VectorServiceLayer : VectorLayer
{
    public Position RandomPosition()
    {
        if (Features.Count <= 0)
            return null;

        var feature = Features.ShuffleEnumerable(RandomHelper.Random).First();
        return feature.VectorStructured.Geometry.RandomPositionFromGeometry();
    }
}