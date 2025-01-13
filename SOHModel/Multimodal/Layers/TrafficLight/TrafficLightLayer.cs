using Mars.Common.Core;
using Mars.Common.Core.Logging;
using Mars.Common.IO;
using Mars.Components.Layers;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Car.Model;

namespace SOHModel.Multimodal.Layers.TrafficLight;

public class TrafficSignalLayer : VectorLayer<SOHModel.Multimodal.Layers.TrafficLight.TrafficLight>
{
    public bool IsInitialized { get; set; }
    
    [PropertyDescription(Name = "synchronizations")]
    public List<DateTime>? SynchronizationTimePoints { get; set; }

    [PropertyDescription(Name = "synchronizeAlwaysSince")]
    public DateTime? SynchronizeAlwaysSince { get; set; }

    [PropertyDescription]
    public ICarLayer CarLayer { get; set; } = default!;
    
    public override bool InitLayer(
        LayerInitData layerInitData, 
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);
        IsInitialized = true;
        return IsInitialized;
    }
}

public class TrafficLightLayer : AbstractActiveLayer
{
    public readonly CarLayer CarLayer;
    private readonly List<TrafficLightController> _trafficLightControllers;
    public readonly ILogger Logger = LoggerFactory.GetLogger(typeof(TrafficLightLayer));
    private string _layerInitFile;
    private string[] _trafficLightPositions;

    public bool IsInitialized { get; set; }
    
    public TrafficLightLayer(CarLayer carLayer)
    {
        CarLayer = carLayer;
        _trafficLightControllers = new List<TrafficLightController>();
    }

    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent);

        if (string.IsNullOrEmpty(layerInitData.LayerInitConfig.File))
            throw new ArgumentOutOfRangeException(nameof(layerInitData.LayerInitConfig.File),
                "Input file of traffic light layer cannot be null or empty");

        _layerInitFile = layerInitData.LayerInitConfig.File;
        Logger.LogInfo("Traffic light layer initializing from file \"" + _layerInitFile + "\"");

        //validation happens in the unzip method
        var inputDirectory = UnzipInputFile(_layerInitFile);

        //look for metadata.csv file
        //todo metadata.csv finden 
        var metaDataFile = Directory.EnumerateFiles(inputDirectory, "metadata.csv", SearchOption.AllDirectories)
            .FirstOrDefault();
        if (metaDataFile == null)
            throw new ArgumentNullException(nameof(metaDataFile),
                "No medata.csv file for traffic light layer found");

        //validate overall length
        var lines = File.ReadAllLines(metaDataFile);
        if (lines.Length < 2)
            throw new ArgumentOutOfRangeException(nameof(metaDataFile), 
                "There were no files specified in the metadata.csv for the traffic light layer");
        //check header contents
        var header = lines[0].Split(',');
        if (!header.Contains("date") || !header.Contains("file"))
            throw new ArgumentOutOfRangeException(nameof(metaDataFile), 
                "The metadata file must contain 'date' and 'file' columns");

        //figure out file and date columns
        var fileColumn = 0;
        for (var i = 0; i < header.Length; i++)
        {
            if (header[i] == "file")
                fileColumn = i;
        }

        //todo once we have more data, the whole content of this file has to be read
        var firstLine = lines[1].Split(',');
        var firstFileName = firstLine[fileColumn];
        var firstFile = Directory.EnumerateFiles(inputDirectory, firstFileName, SearchOption.AllDirectories)
            .FirstOrDefault();

        //validate overall file length
        var trafficLightFile = File.ReadAllLines(firstFile ?? throw new Exception());
        if (trafficLightFile.Length < 2)
            throw new ArgumentOutOfRangeException(nameof(trafficLightFile), 
                "Files to initialize traffic lights need to have a header " +
                "and at least one line with positions for the lights");
        //check header contents
        header = trafficLightFile[0].Split(',');
        if (!header.Contains("lat") || !header.Contains("lon"))
            throw new ArgumentOutOfRangeException(nameof(trafficLightFile), 
                "The traffic light file must contain 'lat' and 'lon' columns");

        //store for initialization later on
        _trafficLightPositions = trafficLightFile;

        return true;
    }

    public override void PreTick()
    {
        if (Context.CurrentTick == 1)
        {
            //figure out lat/lon columns
            var header = _trafficLightPositions[0].Split(',');
            int latColumn = 0, lonColumn = 0;
            for (var i = 0; i < header.Length; i++)
            {
                if (header[i] == "lat") latColumn = i;
                if (header[i] == "lon") lonColumn = i;
            }

            for (var i = 1; i < _trafficLightPositions.Length; i++)
            {
                var lineParts = _trafficLightPositions[i].Split(',');
                _trafficLightControllers.Add(
                    new TrafficLightController(this, CarLayer.Environment,
                        lineParts[latColumn].Value<double>(), lineParts[lonColumn].Value<double>()));
            }

            Logger.LogInfo(_trafficLightControllers.Count + " Traffic light controllers created");

            foreach (var trafficLightController in _trafficLightControllers)
                trafficLightController.GenerateTrafficSchedules();

            return;
        }

        foreach (var controller in _trafficLightControllers) controller.UpdateLightPhase();
    }
    
    private static string UnzipInputFile(string filename)
    {
        return filename.ExtractGzip();
    }
}