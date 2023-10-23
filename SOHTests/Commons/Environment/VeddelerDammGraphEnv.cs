using System.Collections.Generic;
using Mars.Common.Collections.Graph;
using Mars.Components.Environments;
using Mars.Interfaces.Environments;

namespace SOHTests.Commons.Environment;

public static class VeddelerDammGraphEnv
{
    public static ISpatialGraphEnvironment CreateInstance(double lengthE0 = 1150, double lengthE1 = 10000,
        double maxSpeedE0 = 50, double maxSpeedE1 = 50)
    {
        var environment = new SpatialGraph();

        var node0 = environment.AddNode(9.981279, 53.527625,
            new Dictionary<string, object>
            {
                { "name", "start" }
            });
        var node1 = environment.AddNode(9.998142, 53.524941,
            new Dictionary<string, object>
            {
                { "name", "middle" }
            });
        var node2 = environment.AddNode(10.011156, 53.522961,
            new Dictionary<string, object>
            {
                { "name", "goal" }
            });

        environment.AddEdge(node0, node1,
            new Dictionary<string, object>
            {
                { "name", "Veddeler Damm Ost 1" }, { "osmid", "1" },
                { "maxspeed", maxSpeedE0 }
            },
            null, lengthE0);

        environment.AddEdge(node1, node2,
            new Dictionary<string, object>
            {
                { "name", "Veddeler Damm Ost 2" }, { "osmid", "2" },
                { "maxspeed", maxSpeedE1 }
            },
            null, lengthE1);

        return new SpatialGraphEnvironment(environment);
    }
}