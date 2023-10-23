using Mars.Interfaces.Data;
using SOHMultimodalModel.Layers;

namespace SOHTests.Commons.Layer;

public class TestVectorBuildingsLayer : VectorBuildingsLayer
{
    /// <summary>
    ///     Provides a <see cref="VectorBuildingsLayer" /> with initialization of given file.
    /// </summary>
    /// <param name="filePath">Locates the file that holds all vector data.</param>
    /// <returns>An initialized vector layer.</returns>
    public static VectorBuildingsLayer Create(string filePath)
    {
        var layer = new TestVectorBuildingsLayer();
        layer.InitLayer(new LayerInitData { LayerInitConfig = { File = filePath } });
        return layer;
    }
}