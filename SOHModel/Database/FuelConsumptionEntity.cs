using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model;

namespace SOHModel.Database;

public record FuelConsumptionEntity(
    Guid AgentId,
    long Tick,
    
    FuelStrategyType StrategyType,
    FuelCarrierType FuelCarrierType,
    
    double Tank2WheelEfficiency,
    
    double FuelCarrierAmount,                 // total tank, in its unit (L, kg, kWh, etc.)
    double ConsumedAmount,                    // consumed in the same tick, in its unit (L, kg, kWh, etc.)
    double NormalizedFuelCarrierAmount,       // in relation to the fuel tank size
    
    string EnergyFuelDisplayUnit,
    
    double CurrentEnergyPerUnit,              // in J
    double ConsumedEnergyPerUnit              // in J
);

public record RoadLoadEntity(
    Guid AgentId,
    long Tick,
    
    double Velocity,
    double Acceleration, 
    double Mass, 
    double Area,
    double Incline,
    double Efficiency,
    
    double FRolling,
    double FDrag,
    double FGradient,
    double FAccel,
    double FTotal,
    
    double PowerWatts,
    double EnergyJoules
);