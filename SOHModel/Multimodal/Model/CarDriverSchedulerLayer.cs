using Mars.Common;
using Mars.Common.Core;
using Mars.Components.Layers;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Layers;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using SOHModel.Car.Model;

namespace SOHModel.Multimodal.Model;

public class CarDriverSchedulerLayer : SchedulerLayer
{
   private readonly CarLayer _carLayer; 
   
   public CarDriverSchedulerLayer (CarLayer carDriverLayer)
   {
      _carLayer = carDriverLayer;
   }
   
   private static void Register(ILayer layer, ITickClient tickClient)
   {
      //do nothing
   }

   private static void Unregister(ILayer layer, ITickClient tickClient)
   {
      //do nothing
   }

   protected override void Schedule(SchedulerEntry dataRow)
   {
      
      var start = dataRow.SourceGeometry.RandomPositionFromGeometry();
      var goal = dataRow.TargetGeometry.RandomPositionFromGeometry();
      //var drivemode = dataRow.Data.TryGetValue("driveMode", out var driveMode) ? driveMode.Value<int>() : 0;

      var cardriver = new CarDriver
      (
         _carLayer,
         Register,
         Unregister,
         3,
         start.Latitude,
         start.Longitude,
         goal.Latitude,
         goal.Longitude
      );

      //carDriver.Init(_carLayer);

      _carLayer.Driver.Add(cardriver.ID, cardriver);
      RegisterAgent(_carLayer, cardriver);
      //UnregisterAgent(_carLayer, carDriver);
      
   }
}