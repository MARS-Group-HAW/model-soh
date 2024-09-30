using System;
using System.Collections.Generic;
using System.Linq;
using Mars.Common;
using Mars.Common.Data;
using Mars.Components.Layers;
using Mars.Core.Data;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using NetTopologySuite.Geometries;

namespace SOHVeddelFloodBox;

public class HouseholdLayer : AbstractActiveLayer
{
    private readonly OutputBuilder _outputBuilder = OutputBuilder.Builder();

    // NUmber of households that are waiting and then evacuating
    private readonly double _delayHouseholds = 0.3;

    // Target Point all agents move towards
    private readonly Geometry _evacuationPoint = new Point(10.0213807, 53.5248662);

    // distance in m a familymember is considered separated form the rest of the family.
    // A meetingpoint the decided then where the family meets again
    private readonly int _familyDistanceThreshold = 20;

    private bool _hasSimulationEnded;

    // Time in seconds the delayed households will wait at max
    private readonly int _maxDelayTime = 20 * 60;

    // Time in seconds the delayed households will at least wait
    private readonly int _minDelayTime = 5 * 60;

    // Diatance the agent is considere at it's goal
    private readonly int _minTargetDistance = 2;

    // total number of housholds
    private readonly int _numHousehold = 770;

    // umber of households only have one agent
    private readonly int _numSingleHousehold = 458;

    // Number of houses used for the simulation. These are selected randomly for all houses
    private readonly int _numSpawnPoints = 233;

    // total number of agends
    private readonly int _population = 1415;

    // Number of households that are not evacuating
    private readonly double _waitingHouseholds = 0.1;

    private void AddConfigToOutput()
    {
        _outputBuilder.AddConfig("population", _population.ToString());
        _outputBuilder.AddConfig("household", _numHousehold.ToString());
        _outputBuilder.AddConfig("singleHousehold", _numSingleHousehold.ToString());
        _outputBuilder.AddConfig("familyDistanceThreshold", _familyDistanceThreshold.ToString());
        _outputBuilder.AddConfig("minTargetDistance", _minTargetDistance.ToString());
        _outputBuilder.AddConfig("numSpawnPoints", _numSpawnPoints.ToString());
        _outputBuilder.AddConfig("evacuationPoint", _evacuationPoint.ToString());
        _outputBuilder.AddConfig("minDelayTime", _minDelayTime.ToString());
        _outputBuilder.AddConfig("maxDelayTime", _maxDelayTime.ToString());
        _outputBuilder.AddConfig("WaitingHouseholds", _waitingHouseholds.ToString());
        _outputBuilder.AddConfig("delayHouseholds", _delayHouseholds.ToString());
    }

    public override bool InitLayer(LayerInitData layerInitData, RegisterAgent registerAgentHandle = null,
        UnregisterAgent unregisterAgent = null)
    {
        if (!base.InitLayer(layerInitData, registerAgentHandle, unregisterAgent))
            return false;

        AddConfigToOutput();
        var households = new List<Household>();
        var familyHouseholds = new List<Household>();
        var remainingPeople = _population;
        var spawnPoints = ParseHouses(layerInitData);

        var familyHouseholdsCount = _numHousehold - _numSingleHousehold;
        var countHouseholdsPerSpawnpoints = _numHousehold / spawnPoints.Count;

        // every Household gets a member
        foreach (var spawnPoint in spawnPoints)
            for (var i = 0; i <= countHouseholdsPerSpawnpoints; i++)
            {
                households.Add(new Household(1, spawnPoint));
                remainingPeople -= 1;
            }

        Console.WriteLine("Households cnt: " + households.Count);
        Console.WriteLine("remainng ppl: " + remainingPeople);

        SetHouseholdTypes(households);

        // every familyHousehold gets a second Person
        for (var i = 0; i <= familyHouseholdsCount; i++)
        {
            households[i].IncrementNumFamilyMembers(1);
            familyHouseholds.Add(households[i]);
            remainingPeople -= 1;
        }

        //remaining people get shuffled to familys
        while (remainingPeople >= 1)
        {
            familyHouseholds[new Random().Next(0, familyHouseholds.Count)].IncrementNumFamilyMembers(1);
            remainingPeople -= 1;
        }

        Console.WriteLine("remainng ppl: " + remainingPeople);

        var agentManager = layerInitData.Container.Resolve<IAgentManager>();
        var data = (IEnumerable<IDomainData>)Enumerable.Range(0, _population)
            .Select((Func<int, StructuredData>)(_ => new StructuredData()));

        var travelers = agentManager.Create<VeddelTraveler, WaterLevelLayer>(data, null).ToList();
        
        var j = 0;
        foreach (var currHousehold in households)
        {
            for (var i = 0; i < currHousehold.NumFamilyMembers; i++)
            {
                travelers[j].StartPosition = currHousehold.Address.RandomPositionFromGeometry();
                travelers[j].EvacuationPoint = _evacuationPoint.RandomPositionFromGeometry();
                travelers[j].FamilyDistanceThreshold = _familyDistanceThreshold;
                travelers[j].MinTargetDistance = _minTargetDistance;
                travelers[j].OutputBuilder = _outputBuilder;

                _outputBuilder.AddGenderDistribution(travelers[j].Gender, 1);
                RegisterAgent(null, travelers[j]);
                currHousehold.AddFamilyMember(travelers[j]);
                j += 1;
            }

            _outputBuilder.AddFamilyDistribution(currHousehold.NumFamilyMembers, 1);
        }
        /*for (int i = 0; i < travelers.Count; i++)
        {
            travelers[i].StartPosition = spawnPoints[i].RandomPositionFromGeometry();
            this.RegisterAgent(null, travelers[i]);
        }*/

        return true;
    }

    private List<Geometry> ParseHouses(LayerInitData layerInitData)
    {
        var geometry = new List<Geometry>();
        IEnumerable<object> households = layerInitData.LayerInitConfig.Inputs.Import();

        foreach (IDomainData domainData in households)
            if (domainData is IGeometryData dataRow)
            {
                if (dataRow.Geometry == null) throw new Exception("Invalid geometry in household input file");
                geometry.Add(dataRow.Geometry);
            }

        var rnd = new Random();
        var selected = new List<Geometry>();

        for (var i = 0; i < _numSpawnPoints; i++)
        {
            var index = rnd.Next(geometry.Count);
            selected.Add(geometry[index]);
            _outputBuilder.AddHouses(geometry[index]);
            geometry.Remove(geometry[index]);
        }

        return selected;
    }

    private void SetHouseholdTypes(List<Household> households)
    {
        var numOfDelayHouseholds = (int)(households.Count * _delayHouseholds);
        var numOfWaitingHouseholds = (int)(households.Count * _waitingHouseholds);

        _outputBuilder.AddHouseholdType(HouseholdType.DELAY, numOfDelayHouseholds);
        _outputBuilder.AddHouseholdType(HouseholdType.WAITING, numOfWaitingHouseholds);
        _outputBuilder.AddHouseholdType(HouseholdType.WALKING,
            households.Count - numOfDelayHouseholds - numOfWaitingHouseholds);
        var rnd = new Random();

        var copy = new List<Household>(households);

        for (var i = 0; i < Math.Max(numOfDelayHouseholds, numOfWaitingHouseholds); i++)
        {
            if (i < numOfDelayHouseholds)
            {
                var index = rnd.Next(copy.Count);
                copy[index].Type = HouseholdType.DELAY;
                copy[index].DelayTime = rnd.Next(_minDelayTime, _maxDelayTime);
                copy.Remove(copy[index]);
            }

            if (i < numOfWaitingHouseholds)
            {
                var index = rnd.Next(copy.Count);
                copy[index].Type = HouseholdType.WAITING;
                copy.Remove(copy[index]);
            }
        }

        foreach (var household in copy) household.Type = HouseholdType.WALKING;
    }

    public override void PostTick()
    {
        if ((GetCurrentTick() < 0 || GetCurrentTick() == Context.MaxTicks) && !_hasSimulationEnded)
        {
            _outputBuilder.Build();
            _hasSimulationEnded = true;
        }
    }
}