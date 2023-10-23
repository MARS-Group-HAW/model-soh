using System;
using Mars.Common.IO.Csv;
using Mars.Interfaces;
using Mars.Interfaces.Environments;

namespace SOHMultimodalModel.Output.Ticks;

public class TickOutputAdapter
{
    private static readonly DateTime ReferenceDateTime = new(1970, 1, 1);
    private readonly CsvWriter _writer;

    public TickOutputAdapter()
    {
        _writer = new CsvWriter("trips.csv") { Delimiter = ',' };
        _writer.WriteLine(new object[]
        {
            "lon", "lat", "id", "velocity", "tick", "time"
        });
    }

    private int GetTime(ITickOutputAgent agent)
    {
        var clock = agent.Context.CurrentTimePoint.GetValueOrDefault();
        return (int)clock.Subtract(ReferenceDateTime).TotalSeconds;
    }

    public void SaveTick(ITickOutputAgent agent)
    {
        _writer.WriteLine(new object[]
        {
            agent.Position.X, agent.Position.Y, agent.StableId, agent.Velocity * 3.6,
            agent.Context.CurrentTick, GetTime(agent)
        });
    }
}

public interface ITickOutputAgent
{
    ISimulationContext Context { get; }
    Position Position { get; }
    double Velocity { get; }
    int StableId { get; }
}