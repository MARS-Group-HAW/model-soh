using System.Collections.Generic;
using Mars.Interfaces.Environments;

namespace SOHMultimodalModel.Model;

/// <summary>
///     Constants used within the <see cref="Traveler{TLayer}" />
/// </summary>
public static class TravelerConstants
{
    public static readonly ICollection<ModalChoice> DefaultChoices =
        new[] { ModalChoice.Walking, ModalChoice.CyclingRentalBike, ModalChoice.CarDriving };
}