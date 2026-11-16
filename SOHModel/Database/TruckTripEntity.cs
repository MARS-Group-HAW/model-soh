namespace SOHModel.Database;

public enum TruckTripOutcome { Completed, ZeroEnergy, NoLegalStop, NoReachableFuel, NoRoute }

public record TruckTripEntity(
    Guid AgentId, long Tick, DateTime SimulationTime, TruckTripOutcome Outcome,
    string Carrier, double FuelRemaining, double RemainingRouteMeters,
    double DrivingSinceBreakSeconds, double DrivingSinceDailyRestSeconds,
    double Latitude, double Longitude);
