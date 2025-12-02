using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mars.Common.Core.Logging;
using Mars.Components.Layers;
using Mars.Components.Starter;
using Mars.Core.Simulation;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using SOHModel.Domain.Graph;
using SOHModel.Multimodal.Model;
using SOHModel.Train.Model;
using SOHModel.Train.Route;
using SOHModel.Train.Station;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Text;
using System.Text.Json;
using Microsoft.ML.OnnxRuntimeGenAI;

namespace SOHLLMImposterBox;

internal static class Program
{
    private static void Main(string[]? args)
    {
        LoggerFactory.SetLogLevel(LogLevel.Info);
        string basePath = Path.Combine(AppContext.BaseDirectory, "Resources", "models", "gemma-3-1b-it-GQA");
        string modelPath = Path.Combine(basePath, "model_fp16.onnx");

        using var model = new Model(modelPath);
        ;

        // Tokenizer laden (automatisch aus tokenizer.json / tokenizer.model)
        using var tokenizer = new Tokenizer(model);

        // Eingabetext
        string prompt = "Was ist 4 mal 4?";
        Console.WriteLine("Frage: " + prompt);

        // Prompt in Gemma-Chat-Format umwandeln
        // Gemma erwartet: <start_of_turn>user .... <end_of_turn><start_of_turn>model
        string formattedPrompt =
            "<start_of_turn>user\n" +
            prompt +
            "<end_of_turn>\n" +
            "<start_of_turn>model\n";

        // Tokenisierung
        var inputTokens = tokenizer.Encode(formattedPrompt);

        // Generation konfigurieren
        var sequences = tokenizer.Encode(prompt);

        Console.WriteLine("Typ: " + sequences.GetType().FullName);

        var methods = sequences.GetType().GetMethods();
        foreach (var m in methods)
        {
            Console.WriteLine("Methode: " + m.Name);
        }

        var props = sequences.GetType().GetProperties();
        foreach (var p in props)
        {
            Console.WriteLine("Property: " + p.Name);
        }

        
        // int[] ids = sequences.ToList().ToArray();

        using var genParams = new GeneratorParams(model);
        // genParams.SetInputIDs(ids.AsSpan(), (ulong)ids.Length, (ulong)1);

        genParams.SetSearchOption("max_length", 128);
        genParams.SetSearchOption("temperature", 0.0f); // greedy
        genParams.SetSearchOption("top_p", 1.0f);

        // Generator erstellen
        using var generator = new Generator(model, genParams);

        // Streaming-Decoder
        using var stream = tokenizer.CreateStream();

        Console.Write("Antwort: ");

        // Token-für-Token generieren
        while (!generator.IsDone())
        {
            // generator.ComputeLogits();
            generator.GenerateNextToken();

            // Letztes Token decodieren
            var seq = generator.GetSequence(0);
            int lastToken = seq[^1];
            string text = stream.Decode(lastToken);

            Console.Write(text);
        }

        Console.WriteLine();
        
        var description = new ModelDescription();
        description.AddLayer<SpatialGraphMediatorLayer>();
        description.AddLayer<TrainLayer>();
        description.AddLayer<TrainSchedulerLayer>();
        description.AddLayer<TrainStationLayer>();
        
        // Change this type to switch from static route layer to time-dependent GTFS-based layer and vice versa.
        // description.AddLayer<TrainRouteLayer>(new[] { typeof(ITrainRouteLayer) });
        description.AddLayer<TrainGtfsRouteLayer>(new[] {typeof(ITrainRouteLayer)});

        description.AddLayer<PassengerTravelerLayer>();
        description.AddLayer<AgentSchedulerLayer<PassengerTraveler, PassengerTravelerLayer>>(
            "PassengerTravelerSchedulerLayer");

        description.AddAgent<TrainDriver, TrainLayer>();
        description.AddAgent<PassengerTraveler, PassengerTravelerLayer>();

        description.AddEntity<Train>();

        ISimulationContainer application;
        if (args != null && args.Length != 0)
        {
            application = SimulationStarter.BuildApplication(description, args);
        }
        else
        {
            var file = File.ReadAllText("config.json");
            var simConfig = SimulationConfig.Deserialize(file);
            application = SimulationStarter.BuildApplication(description, simConfig);
        }

        var simulation = application.Resolve<ISimulation>();

        var watch = Stopwatch.StartNew();
        var state = simulation.StartSimulation();
        watch.Stop();

        Console.WriteLine($"Executed iterations {state.Iterations} lasted {watch.Elapsed}");
    }
}