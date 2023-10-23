using System.Collections.Generic;
using Mars.Interfaces.Environments;

namespace SOHMultimodalModel.Model;

public class PassengerTraveler : Traveler<PassengerTravelerLayer>
{
    public override void Init(PassengerTravelerLayer layer)
    {
        base.Init(layer);

        EnableCapability(ModalChoice.Train);
        EnableCapability(ModalChoice.Bus);
    }

    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        return Capabilities;
    }
}