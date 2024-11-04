using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Annotations;
using SOHModel.Car.Model;

namespace SOHModel.Multimodal.Model;

public class CarDriverSchedulerLayer : SchedulerLayer
{
   private readonly CarLayer _carLayer; 
   
   public CarDriverSchedulerLayer (CarLayer carDriverLayer)
   {
      _carLayer = carDriverLayer;
   }

   protected override void Schedule(SchedulerEntry dataRow)
   {
      var start = dataRow.SourceGeometry.RandomPositionFromGeometry();
      var goal = dataRow.TargetGeometry.RandomPositionFromGeometry();

      var cardriver = new CarDriver
      (
         _carLayer,
         RegisterAgent,
         UnregisterAgent,
         dataRow.Data.TryGetValue("driveMode", out var driveMode) ? driveMode.Value<int>() : 0,
         dataRow.Data.TryGetValue("startLat", out var startLat) ? startLat.Value<double>() : 0,
         dataRow.Data.TryGetValue("startLot", out var startLot) ? startLot.Value<double>() : 0,
         dataRow.Data.TryGetValue("destLat", out var destLat) ? destLat.Value<double>() : 0,
         dataRow.Data.TryGetValue("destLot", out var destLot) ? destLot.Value<double>() : 0,
         null, //TODO add missing parameter
         "",
         "german"
      );
      //carDriver.Init(_carLayer);

      _carLayer.Driver.Add(cardriver.ID, cardriver);
      RegisterAgent(_carLayer, cardriver);
      //UnregisterAgent(_carLayer, carDriver);
   }
}