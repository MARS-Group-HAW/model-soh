using System.Collections.Generic;
using Mars.Common.Core.Random;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;

namespace SOHModel.BigEvent;

/// <summary>
///     This <see cref="Traveler{HumanTravelerLayer}" /> entity uses a variety of modalities to reach its goal.
/// </summary>
public class Visitor : Traveler<HumanTravelerLayer>
{
    private ISet<ModalChoice> _choices = new HashSet<ModalChoice> { ModalChoice.Walking };

    public override void Init(HumanTravelerLayer layer)
    {
        base.Init(layer);

        Gender = (GenderType)RandomHelper.Random.Next(0, 2);
        OvertakingActivated = false;
    }

    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        return _choices;
    }
}