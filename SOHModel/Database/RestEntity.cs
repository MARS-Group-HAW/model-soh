namespace SOHModel.Database;


public enum RestStateType { Rest, Refuel }

public enum RestEventType { Planned, Start, End }

public record RestEntity(
    Guid AgentId,
    long Tick,
    RestStateType StateType, // "Rest" or "Refuel"
    RestEventType EventType, // "Planned" (stop decided, facility search triggered), "Start" or "End"
    double Latitude,
    double Longitude,
    double? CurrentFuel = null,
    double? RemainingRouteMeters = null
);