using Mars.Interfaces.Data;
using SOHMultimodalModel.Layers;

namespace SOHTests.Commons.Layer;

public class TestVectorLanduseLayer : VectorLanduseLayer
{
    /// <summary>
    ///     Provides a <see cref="VectorLanduseLayer" /> with initialization of given file.
    /// </summary>
    /// <param name="filePath">Locates the file that holds all vector data.</param>
    /// <returns>An initialized vector layer.</returns>
    public static VectorLanduseLayer Create(string filePath)
    {
        var layer = new TestVectorLanduseLayer();
        layer.InitLayer(new LayerInitData { LayerInitConfig = { File = filePath } });
        return layer;
    }
}