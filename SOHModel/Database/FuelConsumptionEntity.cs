using SOHModel.Domain.Model;
using SOHModel.SemiTruck.Model;

namespace SOHModel.Database;

public record FuelConsumptionEntity(
    Guid AgentId,
    long Tick,
    
    FuelStrategyType StrategyType,
    EnergyType EnergyType,
    
    double CurrentEnergyLevel,
    double EnergyUsed
);