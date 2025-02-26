using System;
using System.Collections.Generic;
using System.Drawing.Printing;
using System.Linq;
using Mars.Common.IO.Csv;
using Mars.Components.Environments;
using Mars.Core.Data;
using Mars.Interfaces;
using Mars.Interfaces.Model;
using Mars.Interfaces.Model.Options;
using Mars.Interfaces.Environments;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Train.Model;
using SOHModel.Train.Route;
using SOHModel.Train.Station;
using SOHTests.Commons.Layer;
using SOHModel.Multimodal.Model;
using SOHModel.Domain.Graph;

using Xunit;
using Xunit.Abstractions;

namespace SOHTests.TrainModelTests;

public class TrainPassengerTests : IClassFixture<TrainRouteLayerFixture>
{
    private readonly ITestOutputHelper _output;
    private readonly TrainLayer _trainLayer;
    private readonly TrainRouteLayerFixture _trainRouteLayerFixture;
    private readonly PassengerTravelerLayer _passengerLayer;
    private readonly SpatialGraphMediatorLayer _spatialGraphLayer;
    
    public TrainPassengerTests(TrainRouteLayerFixture trainRouteLayerFixture, ITestOutputHelper output)
    {
        _trainRouteLayerFixture = trainRouteLayerFixture;
        _output = output;
        _trainLayer = new TrainLayer(trainRouteLayerFixture.TrainRouteLayer)
        {
            Context = SimulationContext.Start2020InSeconds,
            EntityManager = new EntityManagerImpl(CsvReader.MapData(ResourcesConstants.TrainCsv)),
            GraphEnvironment = new SpatialGraphEnvironment(new SpatialGraphOptions
            {
                GraphImports = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.TrainU1Graph,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true,
                        }
                    }
                }
            })
        };
        _spatialGraphLayer = new SpatialGraphMediatorLayer();
        _spatialGraphLayer.InitLayer(new LayerInitData
        {
            LayerInitConfig =
            {
                Inputs = new List<Input>
                {
                    new()
                    {
                        File = ResourcesConstants.WalkGraphHamburg,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true,
                            Modalities = new HashSet<SpatialModalityType>
                            {
                                SpatialModalityType.Walking,
                            }
                        }
                    },
                    new()
                    {
                        File = ResourcesConstants.TrainU1Graph,
                        InputConfiguration = new InputConfiguration
                        {
                            IsBiDirectedImport = true,
                            Modalities = new HashSet<SpatialModalityType>
                            {
                                SpatialModalityType.TrainDriving,
                            }
                        }
                    }
                }
            }
        });
        _passengerLayer = new PassengerTravelerLayer
        {
            Context = SimulationContext.Start2020InSeconds,
            SpatialGraphMediatorLayer = _spatialGraphLayer,
            TrainStationLayer = _trainRouteLayerFixture.TrainStationLayer
        };
        _passengerLayer.InitLayer(new LayerInitData(), (_, _) => { }, (_, _) => { });
    }

    [Fact]
    public void TestPassengerEntersTrain()
    {
        var driver = new TrainDriver(_trainLayer, (_, _) => { })
        {
            Line = "U1",
            MinimumBoardingTimeInSeconds = 10
        };
        
        var start = Position.CreateGeoPosition(53.67638,10.00143);
        var goal = Position.CreateGeoPosition(53.61874,10.03663);
        
        var passenger = new PassengerTraveler()
        {
            StartPosition = start,
            GoalPosition = goal,
        };
        passenger.Init(_passengerLayer);
        var route = _passengerLayer.Search(passenger, start, goal, ModalChoice.Train);
        passenger.MultimodalRoute = route;
        
        var ticks = 2000;

        for (var tick = 0; tick < ticks; tick++, _trainLayer.Context.UpdateStep(), _passengerLayer.Context.UpdateStep())
        {
            passenger.Tick();
        }
    }
    
    
}