using System;

namespace SOHModel.Domain.Model;

/// <summary>
/// provides conversions between fuel carrier amounts and energy.
/// </summary>
public static class FuelCarrierEnergyConverter
{
    /// <summary>
    /// returns the energy content in joules for one unit of the given fuel carrier.
    /// <remarks>
    /// <list type="table">
    ///   <listheader>
    ///     <term>Fuel Carrier (Unit)</term>
    ///     <description>Energy Content per Unit</description>
    ///   </listheader>
    ///   <item>
    ///     <term>Fuel (Diesel) [L]</term>
    ///     <description>36 MJ (Lower Heating Value)</description>
    ///   </item>
    ///   <item>
    ///     <term>Battery [kWh]</term>
    ///     <description>3.6 MJ</description>
    ///   </item>
    ///   <item>
    ///     <term>Hydrogen [kg]</term>
    ///     <description>120 MJ (Lower Heating Value)</description>
    ///   </item>
    /// </list>
    /// </remarks>
    /// </summary>
    public static double GetJoulesPerUnit(FuelCarrierType type)
    {
        return type switch
        {
            FuelCarrierType.Fuel => 36_000_000,           // 36 MJ/L (diesel)
            FuelCarrierType.Battery => 3_600_000,         // 3.6 MJ/kWh
            FuelCarrierType.Hydrogen => 120_000_000,      // 120 MJ/kg
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported fuel carrier type.")
        };
    }

    /// <summary>
    /// Converts an amount in the carrier's native unit (L, kWh, kg, ...) to joules.
    /// </summary>
    public static double ToJoules(double amount, FuelCarrierType type)
    {
        return amount * GetJoulesPerUnit(type);
    }

    /// <summary>
    /// converts energy in joules to an amount in the carrier's native unit (L, kWh, kg, ...).
    /// </summary>
    public static double FromJoules(double joules, FuelCarrierType type)
    {
        return joules / GetJoulesPerUnit(type);
    }
    
    public static string GetDisplayUnit(FuelCarrierType type)
    {
        return type switch
        {
            FuelCarrierType.Fuel => "L",
            FuelCarrierType.Battery => "kWh",
            FuelCarrierType.Hydrogen => "kg",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported fuel carrier type.")
        };
    }
}
