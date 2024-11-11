using Mars.Common.Core.Random;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;

namespace SOHModel.BigEvent;

/// <summary>
///     This <see cref="Traveler{HumanTravelerLayer}" /> entity uses a variety of modalities to reach its goal.
/// </summary>
public class Visitor : Traveler<HumanTravelerLayer>
{
    private readonly ISet<ModalChoice> _modalChoices = new HashSet<ModalChoice> { ModalChoice.Walking, ModalChoice.Train };

    public override void Init(HumanTravelerLayer layer)
    {
        base.Init(layer);

        Gender = (GenderType)RandomHelper.Random.Next(0, 2);
        OvertakingActivated = false;
    }

    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        return _modalChoices;
    }


    protected override MultimodalRoute FindMultimodalRoute()
    {
        try
        {
            return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoices());
        }
        catch (Exception ex) {
            if (ex.Message.Contains("no reachable train station found", System.StringComparison.CurrentCultureIgnoreCase) || ex.Message.Contains("no train route available", System.StringComparison.CurrentCultureIgnoreCase)) {
                return MultimodalLayer.Search(this, StartPosition, GoalPosition, [ModalChoice.Walking]);
            }
            else throw;
        }
    }
}