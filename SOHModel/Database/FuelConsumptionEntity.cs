using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model;

namespace SOHModel.Database;

public record FuelConsumptionEntity(
    Guid AgentId,
    long Tick,
    
    FuelStrategyType StrategyType,
    FuelCarrierType FuelCarrierType,
    
    double Tank2WheelEfficiency,
    
    double CurrentFuelCarrierAmount,    // in its unit (L, kg, kWh, etc.)
    double ConsumedAmount,              // in its unit (L, kg, kWh, etc.)
    string EnergyFuelDisplayUnit,
    
    double CurrentEnergyPerUnit,        // in J
    double ConsumedEnergyPerUnit        // in J
);