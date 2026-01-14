namespace SOHModel.Database;


public enum RestStateType { Rest, Refuel }

public enum RestEventType { Start, End }

public record RestEntity(
    Guid AgentId,
    long Tick,
    RestStateType StateType, // "Rest" or "Refuel"
    RestEventType EventType, // "Start" or "End"
    double Latitude,
    double Longitude,
    double? CurrentFuel = null
);