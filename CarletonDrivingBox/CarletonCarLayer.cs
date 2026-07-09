using System.Linq;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Data;
using Mars.Interfaces.Layers;
using SOHModel.Car.Model;

namespace SOHCarletonDrivingBox;

/// <summary>
/// Carleton campus car layer. Skips init spawn when agent config has no schedule file
/// (scheduler-only setups use <see cref="CarletonCarDriverSchedulerLayer"/> instead).
/// </summary>
public class CarletonCarLayer : CarLayer
{
    public override bool InitLayer(
        LayerInitData layerInitData,
        RegisterAgent? registerAgentHandle = null,
        UnregisterAgent? unregisterAgent = null)
    {
        var initData = new LayerInitData(layerInitData.Context)
        {
            Container = layerInitData.Container,
            LayerInitConfig = layerInitData.LayerInitConfig,
            AgentInitConfigs = layerInitData.AgentInitConfigs
                .Where(config => !string.IsNullOrEmpty(config.File))
                .ToList()
        };

        return base.InitLayer(initData, registerAgentHandle, unregisterAgent);
    }
}
