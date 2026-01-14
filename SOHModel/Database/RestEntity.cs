namespace SOHModel.Database;


public enum RestStateType { Rest, Refuel }

public enum RestEventType { Start, End }

public record RestEntity(
    Guid AgentId,
    long Tick,
    RestStateType StateType, // "Rest" or "Refuel"
    RestEventType EventType, // "Start" or "End"
    double Longitude,
    double Latitude,
    double? CurrentFuel = null
);