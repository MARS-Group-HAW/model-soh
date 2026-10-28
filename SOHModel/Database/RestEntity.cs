namespace SOHModel.Database;


public enum RestStateType { Rest, Refuel, RestAndRefuel }

public enum RestEventType { Planned, Start, End }

public record RestEntity(
    Guid AgentId,
    long Tick,
    RestStateType StateType, // "Rest", "Refuel" or "RestAndRefuel" (legal break/rest combined with charging)
    RestEventType EventType, // "Planned" (stop decided, facility search triggered), "Start" or "End"
    double Latitude,
    double Longitude,
    double? CurrentFuel = null,
    double? RemainingRouteMeters = null
);