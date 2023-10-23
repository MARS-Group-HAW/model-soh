using Mars.Interfaces.Data;
using SOHMultimodalModel.Layers;

namespace SOHTests.Commons.Layer;

public class TestVectorPoiLayer : VectorPoiLayer
{
    /// <summary>
    ///     Provides a <see cref="VectorPoiLayer" /> with initialization of given file.
    /// </summary>
    /// <param name="filePath">Locates the file that holds all vector data.</param>
    /// <returns>An initialized vector layer.</returns>
    public static VectorPoiLayer Create(string filePath)
    {
        var layer = new TestVectorPoiLayer();
        layer.InitLayer(new LayerInitData { LayerInitConfig = { File = filePath } });
        return layer;
    }
}