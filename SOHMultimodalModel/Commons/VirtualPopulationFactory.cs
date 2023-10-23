// using System;
// using System.Collections.Generic;
// using Mars.Common.Core.Logging;
// using Mars.Interfaces;
// using Mars.Interfaces.Environments;
// using SOHDomain.Common;
// using SOHMultimodalModel.Layers;
// using SOHMultimodalModel.Planning;
//
// namespace SOHVirtualPopulationSpawnerLayer
// {
//     public static class VirtualPopulationFactory
//     {
//         private const double PercentMale = 48.85;
//         private const double PercentFemale = 51.15;
//         private const int PercentWorkers = 94;
//         private const double PartTimeWorkerInPercent = 26.9;
//         private const double Age18To20 = 6.9;
//         private const double Age21To24 = Age18To20 + 8.8;
//         private const double Age25To29 = Age21To24 + 12.1;
//         private const double Age30To34 = Age25To29 + 12.1;
//         private const double Age35To39 = Age30To34 + 11.4;
//         private const double Age40To44 = Age35To39 + 10.6;
//         private const double Age45To54 = Age40To44 + 19.4;
//         private const double Age55To59 = Age45To54 + 10;
//         private const double Age60To64 = 100;
//         private static SimulationContext _simulationContext;
//
//         public static Dictionary<DateTime, List<Tuple<Position, Position>>> GenerateCitizens(int amountOfAgents,
//             SimulationContext simulationContext)
//         {
//             _simulationContext = simulationContext;
//             var allTrips = new Dictionary<DateTime, List<Tuple<Position, Position>>>();
//
//             for (var i = 0; i < amountOfAgents; i++)
//             {
//                 var citizen = new DummyCitizen
//                 {
//                     Age = GenerateAge(),
//                     Gender = GenerateGender(),
//                     IsWorker = DecideIfWorker(),
//                     SimulationContext = simulationContext
//                 };
//
//                 if (citizen.IsWorker) citizen.IsPartTimeWorker = DecideIfPartTimeWorker();
//
//                 var trips = citizen.CalculateTrips();
//
//                 foreach (var trip in trips)
//                     if (allTrips.ContainsKey(trip.Key))
//                         allTrips[trip.Key].Add(new Tuple<Position, Position>(
//                             trip.Value.Item1, trip.Value.Item2));
//                     else
//                         allTrips.Add(trip.Key, new List<Tuple<Position, Position>>
//                         {
//                             new Tuple<Position, Position>(trip.Value.Item1, trip.Value.Item2)
//                         });
//             }
//
// //            var csv = new StringBuilder();
// //            foreach (KeyValuePair<DateTime,List<Tuple<Position,Position>>> keyValuePair in allTrips)
// //            {
// //                var newLine = $"{keyValuePair.Key.Hour}:{keyValuePair.Key.Minute},{keyValuePair.Value.Count}";
// //                csv.AppendLine(newLine);
// //            }
// //            
// //            File.AppendAllText("population.csv", csv.ToString());
//
//             return allTrips;
//         }
//
//
//         private static int GenerateAge()
//         {
//             var rand = new Random();
//             var randomDouble = rand.NextDouble() * 100;
//
//             if (randomDouble <= Age18To20) return rand.Next(18, 21);
//
//             if (randomDouble > Age18To20 && randomDouble <= Age21To24) return rand.Next(21, 25);
//
//             if (randomDouble > Age21To24 && randomDouble <= Age25To29) return rand.Next(25, 30);
//
//             if (randomDouble > Age25To29 && randomDouble <= Age30To34) return rand.Next(30, 35);
//
//             if (randomDouble > Age30To34 && randomDouble <= Age35To39) return rand.Next(35, 40);
//
//             if (randomDouble > Age35To39 && randomDouble <= Age40To44) return rand.Next(40, 45);
//
//             if (randomDouble > Age40To44 && randomDouble <= Age45To54) return rand.Next(45, 55);
//
//             if (randomDouble > Age45To54 && randomDouble <= Age55To59) return rand.Next(55, 60);
//
//             if (randomDouble > Age55To59 && randomDouble <= Age60To64) return rand.Next(60, 65);
//
//             return 0;
//         }
//
//         private static bool DecideIfWorker()
//         {
//             var rand = new Random();
//             var random = rand.NextDouble() * 100;
//
//             return random < PercentWorkers;
//         }
//
//         private static bool DecideIfPartTimeWorker()
//         {
//             var rand = new Random();
//             var random = rand.NextDouble() * 100;
//
//             return random < PartTimeWorkerInPercent;
//         }
//
//         private static int GenerateGender()
//         {
//             var rand = new Random();
//             var random = rand.NextDouble() * 100;
//
//             return random < PercentMale ? 0 : 1;
//         }
//     }
//
//     public class DummyCitizen
//     {
//         private Position _home;
//         private Position _startLocation;
//         private Position _work;
//         public SimulationContext SimulationContext { get; internal set; }
//         public int Gender { get; set; }
//         public int Age { get; set; }
//         public bool IsWorker { get; set; }
//         public bool IsPartTimeWorker { get; set; }
//
//         public Dictionary<DateTime, Tuple<Position, Position>> CalculateTrips()
//         {
//             var logger = LoggerFactory.GetLogger(typeof(DummyCitizen));
//             var placesToBe = new Dictionary<DateTime, Tuple<Position, Position>>();
//
//             _startLocation = MediatorLayer.GetRandomCoordinateInSimulatedArea();
//             var closestStartNode = MediatorLayer.CarLayer.GraphEnvironment.NearestNode(_startLocation);
//
//             while (!closestStartNode.OutgoingEdges.Any() || !closestStartNode.IncomingEdges.Any())
//             {
//                 logger.LogWarning("start location was dead end");
//                 _startLocation = MediatorLayer.GetRandomCoordinateInSimulatedArea();
//                 closestStartNode = MediatorLayer.CarLayer.GraphEnvironment.NearestNode(_startLocation);
//             }
//
//             _home = MediatorLayer.GetNextPoiOfType(_startLocation, NamesToCodeMapping.Buildings, 1);
//             var closestHomeNode = MediatorLayer.CarLayer.GraphEnvironment.NearestNode(_home);
//
//             while (!closestHomeNode.IncomingEdges.Any() || !closestHomeNode.OutgoingEdges.Any())
//             {
//                 logger.LogWarning("home location was dead end");
//                 closestHomeNode = MediatorLayer.CarLayer.GraphEnvironment.GetRandomNode();
//                 _home = closestHomeNode.Position;
//             }
//
//             _startLocation = MediatorLayer.GetRandomCoordinateInSimulatedArea();
//             _work = MediatorLayer.GetPoiWithOneOutOfManyTypes(_startLocation, new List<int>
//             {
//                 NamesToCodeMapping.Industrial, NamesToCodeMapping.Commercial
//             }, 5);
//             var closestWorkNode = MediatorLayer.CarLayer.GraphEnvironment.NearestNode(_work);
//
//             while (!closestWorkNode.IncomingEdges.Any() || !closestWorkNode.OutgoingEdges.Any() ||
//                    closestWorkNode.Position == closestHomeNode.Position)
//             {
//                 logger.LogWarning("work location was dead end");
//                 closestWorkNode = MediatorLayer.CarLayer.GraphEnvironment.GetRandomNode();
//                 _work = closestWorkNode.Position;
//             }
//
//             var dayPlan = DayPlanGenerator.CreateDayPlanForAgent(this, true);
//
//             var currentPosition = MediatorLayer.GetRandomCoordinateInSimulatedArea();
//             foreach (var action in dayPlan)
//             {
//                 var destination = GetCoordinateForAction(action, currentPosition, 2);
//
//                 var closestNode = MediatorLayer.CarLayer.GraphEnvironment.NearestNode(destination);
//
//                 while (!closestNode.IncomingEdges.Any() || !closestNode.OutgoingEdges.Any())
//                 {
//                     logger.LogWarning("closest node for action was dead end");
//                     closestNode = MediatorLayer.CarLayer.GraphEnvironment.GetRandomNode();
//                 }
//
//                 var distance = closestNode.Position.DistanceInKmTo(destination) * 1000;
//                 var route = MediatorLayer.CarLayer.GraphEnvironment.FindRoute(closestNode,
//                     MediatorLayer.CarLayer.GraphEnvironment.NearestNode(destination),
//                     (node, edge, arg3) => { return 0; });
//
//                 placesToBe.Add(action.StartTime, new Tuple<Position, Position>(currentPosition, destination));
//
//                 currentPosition = destination;
//             }
//
//             return placesToBe;
//         }
//
//         private Position GetCoordinateForAction(DayPlanAction action, Position startPoint, double maxRadius = -1D)
//         {
//             Position returnCoordinate = null;
//             bool doItAtHome;
//             switch (action.DayPlanActionType)
//             {
//                 case DayPlanActionType.Work:
//                     returnCoordinate = _work;
//                     break;
//                 case DayPlanActionType.HomeTime:
//                     returnCoordinate = _home;
//                     break;
//                 case DayPlanActionType.Eat:
//                     var rand = new Random();
//                     if (startPoint.DistanceInKmTo(_home) < 0.5)
//                     {
//                         //Wenn Agent unter 500m von seinem Home weg ist
//                         doItAtHome = rand.Next(100) < 80;
//                         returnCoordinate = doItAtHome
//                             ? _home
//                             : MediatorLayer.GetPoiWithOneOutOfManyTypes(startPoint, new List<int>
//                             {
//                                 NamesToCodeMapping.Catering, NamesToCodeMapping.Restaurant, NamesToCodeMapping.FastFood,
//                                 NamesToCodeMapping.Cafe, NamesToCodeMapping.Pub,
//                                 NamesToCodeMapping.Bar, NamesToCodeMapping.Bakery, NamesToCodeMapping.FoodCourt,
//                                 NamesToCodeMapping.Biergarten
//                             }, false, maxRadius);
//                     }
//                     else
//                     {
//                         doItAtHome = rand.Next(100) < 20;
//                         returnCoordinate = doItAtHome
//                             ? _home
//                             : MediatorLayer.GetPoiWithOneOutOfManyTypes(startPoint, new List<int>
//                             {
//                                 NamesToCodeMapping.Catering, NamesToCodeMapping.Restaurant, NamesToCodeMapping.FastFood,
//                                 NamesToCodeMapping.Cafe, NamesToCodeMapping.Pub,
//                                 NamesToCodeMapping.Bar, NamesToCodeMapping.Bakery, NamesToCodeMapping.FoodCourt,
//                                 NamesToCodeMapping.Biergarten
//                             }, maxRadius);
//                     }
//
//                     break;
//                 case DayPlanActionType.FreeTime:
//                     doItAtHome = new Random().NextDouble() < 0.5;
//                     returnCoordinate = doItAtHome
//                         ? _home
//                         : MediatorLayer.GetPoiWithOneOutOfManyTypes(startPoint,
//                             new List<int>
//                             {
//                                 NamesToCodeMapping.Buildings, NamesToCodeMapping.Theatre, NamesToCodeMapping.Nightclub,
//                                 NamesToCodeMapping.Cinema,
//                                 NamesToCodeMapping.ParkPoi, NamesToCodeMapping.Playground, NamesToCodeMapping.DogPark,
//                                 NamesToCodeMapping.SportsCenter, NamesToCodeMapping.Pitch,
//                                 NamesToCodeMapping.SwimmingPool, NamesToCodeMapping.TennisCourt,
//                                 NamesToCodeMapping.GolfCourse, NamesToCodeMapping.Stadium, NamesToCodeMapping.IceRink,
//                                 NamesToCodeMapping.Shopping, NamesToCodeMapping.Supermarket, NamesToCodeMapping.Bakery,
//                                 NamesToCodeMapping.Kiosk, NamesToCodeMapping.Mall, NamesToCodeMapping.DepartmentStore,
//                                 NamesToCodeMapping.Convenience, NamesToCodeMapping.Clothes, NamesToCodeMapping.Florist,
//                                 NamesToCodeMapping.Chemist, NamesToCodeMapping.Bookshop,
//                                 NamesToCodeMapping.Butcher, NamesToCodeMapping.ShoeShop, NamesToCodeMapping.Optician,
//                                 NamesToCodeMapping.Beverages, NamesToCodeMapping.Jeweller,
//                                 NamesToCodeMapping.GiftShop, NamesToCodeMapping.SportsShop,
//                                 NamesToCodeMapping.OutdoorShop, NamesToCodeMapping.MobilePhoneShop,
//                                 NamesToCodeMapping.ToyShop,
//                                 NamesToCodeMapping.BeautyShop, NamesToCodeMapping.ComputerShop,
//                                 NamesToCodeMapping.GardenCenter, NamesToCodeMapping.Hairdresser,
//                                 NamesToCodeMapping.Money,
//                                 NamesToCodeMapping.Bank, NamesToCodeMapping.Tourism, NamesToCodeMapping.Attraction,
//                                 NamesToCodeMapping.Museum, NamesToCodeMapping.Monument, NamesToCodeMapping.Memorial,
//                                 NamesToCodeMapping.Art, NamesToCodeMapping.Castle, NamesToCodeMapping.Ruins,
//                                 NamesToCodeMapping.Archaeological, NamesToCodeMapping.WaysiteCross,
//                                 NamesToCodeMapping.WaysideShrine, NamesToCodeMapping.Battlefield,
//                                 NamesToCodeMapping.Fort, NamesToCodeMapping.PicnicSite, NamesToCodeMapping.Viewpoint,
//                                 NamesToCodeMapping.Zoo, NamesToCodeMapping.ThemePark, NamesToCodeMapping.Forest,
//                                 NamesToCodeMapping.ParkLandUse, NamesToCodeMapping.Residential,
//                                 NamesToCodeMapping.Commercial,
//                                 NamesToCodeMapping.NatureReserve, NamesToCodeMapping.HealthLanduse,
//                                 NamesToCodeMapping.NationalPark
//                             }, false, maxRadius);
//                     break;
//                 case DayPlanActionType.Errands:
//                     returnCoordinate = MediatorLayer.GetPoiWithOneOutOfManyTypes(startPoint, new List<int>
//                     {
//                         NamesToCodeMapping.Commercial, NamesToCodeMapping.Shopping, NamesToCodeMapping.Supermarket,
//                         NamesToCodeMapping.Bakery, NamesToCodeMapping.Kiosk, NamesToCodeMapping.Mall,
//                         NamesToCodeMapping.DepartmentStore,
//                         NamesToCodeMapping.Convenience, NamesToCodeMapping.Clothes, NamesToCodeMapping.Florist,
//                         NamesToCodeMapping.Chemist, NamesToCodeMapping.Bookshop,
//                         NamesToCodeMapping.Butcher, NamesToCodeMapping.ShoeShop, NamesToCodeMapping.Optician,
//                         NamesToCodeMapping.Beverages, NamesToCodeMapping.Jeweller,
//                         NamesToCodeMapping.GiftShop, NamesToCodeMapping.SportsShop, NamesToCodeMapping.OutdoorShop,
//                         NamesToCodeMapping.MobilePhoneShop, NamesToCodeMapping.ToyShop,
//                         NamesToCodeMapping.BeautyShop, NamesToCodeMapping.ComputerShop, NamesToCodeMapping.GardenCenter,
//                         NamesToCodeMapping.Hairdresser, NamesToCodeMapping.Money,
//                         NamesToCodeMapping.Bank, NamesToCodeMapping.Kindergarten, NamesToCodeMapping.Police,
//                         NamesToCodeMapping.FireStation, NamesToCodeMapping.Postbox, NamesToCodeMapping.Postoffice,
//                         NamesToCodeMapping.Library, NamesToCodeMapping.Townhall, NamesToCodeMapping.Courthouse,
//                         NamesToCodeMapping.ArtsCenter, NamesToCodeMapping.MarketPlace, NamesToCodeMapping.Recycling,
//                         NamesToCodeMapping.School, NamesToCodeMapping.University, NamesToCodeMapping.College,
//                         NamesToCodeMapping.PublicBuilding, NamesToCodeMapping.Hospital, NamesToCodeMapping.Doctors,
//                         NamesToCodeMapping.Pharmacy, NamesToCodeMapping.Dentist, NamesToCodeMapping.Veterinary
//                     }, false, maxRadius);
//                     break;
//                 default:
//                     throw new ArgumentOutOfRangeException();
//             }
//
//             return returnCoordinate;
//         }
//     }
// }
//

