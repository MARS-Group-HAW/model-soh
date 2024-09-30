using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Mars.Common;
using NetTopologySuite.Geometries;
using SOHModel.Multimodal.Model;

namespace SOHVeddelFloodBox;

public class OutputBuilder
{
    private const string FolderPath = "../../../out";
    private readonly Dictionary<string, string> _configs = new();
    private readonly Dictionary<int, int> _deadAgentsMap = new();

    private readonly Dictionary<int, int> _familyDistribution = new();
    private readonly Dictionary<GenderType, int> _genderDistribution = new();
    private readonly Dictionary<HouseholdType, int> _householdTypeDistribution = new();
    private readonly HashSet<Geometry> _houses = new();
    private readonly Dictionary<int, int> _meetingPointArrivalMap = new();
    private readonly Dictionary<int, double> _waterLevel = new();

    private OutputBuilder()
    {
    }

    public static OutputBuilder Builder()
    {
        return new OutputBuilder();
    }

    public void AddFamilyDistribution(int numOfPeople, int count)
    {
        if (!_familyDistribution.TryAdd(numOfPeople, count))
            _familyDistribution[numOfPeople] += count;
    }

    private void WriteFamilyDistribution()
    {
        var str = new StringBuilder();
        str.Append("FamilySize;Count").Append(Environment.NewLine);
        var keys = _familyDistribution.Keys.ToList();
        keys.Sort();
        foreach (var key in keys)
            str.Append(key).Append(';').Append(_familyDistribution[key]).Append(Environment.NewLine);
        File.WriteAllText(FolderPath + "/familyDistribution.csv", str.ToString());
    }

    public void AddConfig(string name, string value)
    {
        if (_configs.ContainsKey(name))
            _configs[name] = value;
        else
            _configs.Add(name, value);
    }

    private void WriteConfig()
    {
        var str = new StringBuilder();
        str.Append("Name;Value").Append(Environment.NewLine);
        foreach (var key in _configs.Keys)
            str.Append(key).Append(';').Append(_configs[key]).Append(Environment.NewLine);
        File.WriteAllText(FolderPath + "/config.csv", str.ToString());
    }

    public void AddHouses(Geometry house)
    {
        _houses.Add(house);
    }

    private void WriteHouses(bool isXFirst)
    {
        var str = new StringBuilder();
        str.Append("Coordinate").Append(Environment.NewLine);
        foreach (var house in _houses)
        {
            var pos = house.RandomPositionFromGeometry();
            if (isXFirst)
                str.Append('(').Append(pos.X).Append(',').Append(pos.Y).Append(')').Append(Environment.NewLine);
            else
                str.Append('(').Append(pos.Y).Append(',').Append(pos.X).Append(')').Append(Environment.NewLine);
        }

        File.WriteAllText(FolderPath + "/houses.csv", str.ToString());
    }

    public void AddGenderDistribution(GenderType gender, int count)
    {
        if (!_genderDistribution.TryAdd(gender, count))
            _genderDistribution[gender] += count;
    }

    private void WriteGenderDistribution()
    {
        var str = new StringBuilder();
        str.Append("Gender;Value").Append(Environment.NewLine);

        if (_genderDistribution.ContainsKey(GenderType.Male))
            str.Append("Male").Append(';').Append(_genderDistribution[GenderType.Male]).Append(Environment.NewLine);
        else
            str.Append("Male").Append(';').Append(1).Append(Environment.NewLine);

        if (_genderDistribution.ContainsKey(GenderType.Female))
            str.Append("Female").Append(';').Append(_genderDistribution[GenderType.Female]).Append(Environment.NewLine);
        else
            str.Append("Female").Append(';').Append(1).Append(Environment.NewLine);

        File.WriteAllText(FolderPath + "/genderDistribution.csv", str.ToString());
    }

    public void AddWaterLevel(int seconds, double level)
    {
        // need to be locked to prevent concurrent access
        lock (_waterLevel)
        {
            _waterLevel.TryAdd(seconds, level);
        }
    }


    public void AddTravelerFinished(int seconds)
    {
        // need to be locked to prevent concurrent access
        lock (_meetingPointArrivalMap)
        {
            if (!_meetingPointArrivalMap.TryAdd(seconds, 1))
                _meetingPointArrivalMap[seconds] += 1;
        }
    }

    public void AddTravelerDead(int seconds)
    {
        // need to be locked to prevent concurrent access
        lock (_deadAgentsMap)
        {
            if (!_deadAgentsMap.TryAdd(seconds, 1))
                _deadAgentsMap[seconds] += 1;
        }
    }

    private void WriteWaterLevel()
    {
        var str = new StringBuilder();
        str.Append("Seconds;Meter").Append(Environment.NewLine);
        lock (_waterLevel)
        {
            if (_waterLevel.Count != 0)
            {
                var maxSeconds = _waterLevel.Keys.Max();
                for (var i = 0; i <= maxSeconds; i++)
                    str.Append(i).Append(';').Append(_waterLevel[i]).Append(Environment.NewLine);
            }
        }

        File.WriteAllText(FolderPath + "/waterLevel.csv", str.ToString());
    }

    private void WriteTravelerFinished()
    {
        var str = new StringBuilder();
        str.Append("Seconds;Number").Append(Environment.NewLine);
        lock (_meetingPointArrivalMap)
        {
            if (_meetingPointArrivalMap.Count != 0)
            {
                var maxSeconds = _meetingPointArrivalMap.Keys.Max();
                var arrived = 0;
                for (var i = 0; i <= maxSeconds; i++)
                    if (_meetingPointArrivalMap.ContainsKey(i))
                    {
                        arrived += _meetingPointArrivalMap[i];
                        str.Append(i).Append(';').Append(arrived).Append(Environment.NewLine);
                    }
            }
        }

        File.WriteAllText(FolderPath + "/arrivalGraph.csv", str.ToString());
    }

    private void WriteTravelerDead()
    {
        var str = new StringBuilder();
        str.Append("Seconds;Number").Append(Environment.NewLine);
        lock (_deadAgentsMap)
        {
            if (_deadAgentsMap.Count != 0)
            {
                var maxSeconds = _deadAgentsMap.Keys.Max();
                var dead = 0;
                for (var i = 0; i <= maxSeconds; i++)
                    if (_deadAgentsMap.ContainsKey(i))
                    {
                        dead += _deadAgentsMap[i];
                        str.Append(i).Append(';').Append(dead).Append(Environment.NewLine);
                    }
            }
        }

        File.WriteAllText(FolderPath + "/deadAgentsGraph.csv", str.ToString());
    }

    public void AddHouseholdType(HouseholdType type, int count)
    {
        if (!_householdTypeDistribution.TryAdd(type, count))
            _householdTypeDistribution[type] += count;
    }

    public void WriteHouseholdTypeDistribution()
    {
        var str = new StringBuilder();
        str.Append("Type;Count").Append(Environment.NewLine);

        if (_householdTypeDistribution.ContainsKey(HouseholdType.DELAY))
            str.Append("Delay").Append(';').Append(_householdTypeDistribution[HouseholdType.DELAY])
                .Append(Environment.NewLine);
        else
            str.Append("Delay").Append(';').Append(1).Append(Environment.NewLine);

        if (_householdTypeDistribution.ContainsKey(HouseholdType.WAITING))
            str.Append("Waiting").Append(';').Append(_householdTypeDistribution[HouseholdType.WAITING])
                .Append(Environment.NewLine);
        else
            str.Append("Waiting").Append(';').Append(1).Append(Environment.NewLine);

        if (_householdTypeDistribution.ContainsKey(HouseholdType.WALKING))
            str.Append("Walking").Append(';').Append(_householdTypeDistribution[HouseholdType.WALKING])
                .Append(Environment.NewLine);
        else
            str.Append("Walking").Append(';').Append(1).Append(Environment.NewLine);

        File.WriteAllText(FolderPath + "/householdTypeDistribution.csv", str.ToString());
    }

    public void Build()
    {
        if (Directory.Exists(FolderPath) == false)
            Directory.CreateDirectory(FolderPath);

        WriteFamilyDistribution();
        WriteConfig();
        WriteHouses(false);
        WriteGenderDistribution();
        WriteTravelerFinished();
        WriteHouseholdTypeDistribution();
        WriteTravelerDead();
    }

    public void BuildWaterCsv()
    {
        WriteWaterLevel();
    }
}