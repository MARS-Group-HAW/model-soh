using Mars.Interfaces.Environments;

namespace SOHModel.Multimodal.Layers.TrafficLight;

public class TrafficLight
{
    public TrafficLight(TrafficLightPhase currentLightPhase, int startGreenPhaseTick, int startYellowPhaseTick,
        int startRedPhaseTick)
    {
        TrafficLightPhase = currentLightPhase;
        StartGreenTick = startGreenPhaseTick;
        StartYellowTick = startYellowPhaseTick;
        StartRedTick = startRedPhaseTick;
    }

    public int StartGreenTick { get; }
    public int StartYellowTick { get; }
    public int StartRedTick { get; }
    public TrafficLightPhase TrafficLightPhase { get; set; }
}