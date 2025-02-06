using Mars.Common.Core.Random;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Bicycle.Parking;
using SOHModel.Multimodal.Model;

namespace SOHModel.BigEvent;

/// <summary>
/// A resident is a <see cref="Traveler{BaseWalkingLayer}" /> entity that uses a car and represents the people who
/// live in the area of the Barclays arena or just drive on the nearby streets.
/// </summary>
public class Resident : Traveler<BaseWalkingLayer>
{
    
    /**
     * This method initializes the resident.
     */
    public override void Init(BaseWalkingLayer layer)
    {
        base.Init(layer);
        OvertakingActivated = true;
        Car = CarParkingLayer.CreateOwnCarNear(StartPosition);
    }
    

    protected override MultimodalRoute FindMultimodalRoute()
    {
        //Console.WriteLine("Preferred modal choice: " + _preferred);
        //Console.Write("Visitor's start position: " + StartPosition);
        return MultimodalLayer.Search(this, StartPosition, GoalPosition, ModalChoice.CarDriving);
    }
    
}