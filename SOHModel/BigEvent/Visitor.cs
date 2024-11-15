using Mars.Common.Core.Random;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using SOHModel.Multimodal.Model;

namespace SOHModel.BigEvent;

/// <summary>
///     This <see cref="Traveler{BigEventLayer}" /> entity uses a variety of modalities to reach its goal.
/// </summary>
public class Visitor : Traveler<BigEventLayer>
{
    private ISet<ModalChoice> _choices;

    public override void Init(BigEventLayer layer)
    {
        base.Init(layer);
        OvertakingActivated = true;

        _choices = new ModalityChooser().Evaluate(this);
        _choices.Add(ModalChoice.Walking);

        handleLogic();
        
    const int radiusInM = 100;
        // if (_choices.Contains(ModalChoice.CyclingOwnBike) && BicycleParkingLayer != null)
        //     Bicycle = BicycleParkingLayer.CreateOwnBicycleNear(StartPosition, radiusInM, UsesBikeAndRide);
        //
        // if (_choices.Contains(ModalChoice.CarDriving) && CarParkingLayer != null)
        //     Car = CarParkingLayer.CreateOwnCarNear(StartPosition, radiusInM);
    }

    /**
     * This method handles the logic of the modal choices.
     * It removes the modal choices that are not compatible with the selected modal choice.
     * For example, if the visitor chooses to drive a car, they won't be able to choose to take the bus or train or co-drive the car.
     */
    private void handleLogic()
    {
        if (_choices.Contains(ModalChoice.CarDriving))
        {
            _choices.Remove(ModalChoice.Bus);
            _choices.Remove(ModalChoice.Train);
            _choices.Remove(ModalChoice.CyclingOwnBike);
            _choices.Remove(ModalChoice.CoDriving);
        }
        if (_choices.Contains(ModalChoice.CoDriving))
        {
            _choices.Remove(ModalChoice.Bus);
            _choices.Remove(ModalChoice.Train);
            _choices.Remove(ModalChoice.CyclingOwnBike);
            _choices.Remove(ModalChoice.CarDriving);
        }
    }

    protected override IEnumerable<ModalChoice> ModalChoices()
    {
        return _choices;
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
    
    #region input

    [PropertyDescription(Name = "usesBike")]
    public double UsesBike { get; set; }

    [PropertyDescription(Name = "usesCar")] 
    public double UsesCar { get; set; }
    
    [PropertyDescription(Name = "usesCoDriving")]
    public double UsesCoDriving { get; set; }
    
    [PropertyDescription(Name = "usesTrain")]
    public double UsesTrain { get; set; }
    
    [PropertyDescription(Name = "usesBus")]
    public double UsesBus { get; set; }

    #endregion
}

public class ModalityChooser
{
    public ISet<ModalChoice> Evaluate(Visitor attributes)
    {
        if (RandomHelper.Random.NextDouble() < attributes.UsesCar)
            return new HashSet<ModalChoice> { ModalChoice.CarDriving };
        
        if (RandomHelper.Random.NextDouble() < attributes.UsesCoDriving)
            return new HashSet<ModalChoice> { ModalChoice.CoDriving };

        if (RandomHelper.Random.NextDouble() < attributes.UsesBike)
            return new HashSet<ModalChoice> { ModalChoice.CyclingOwnBike };

        if (RandomHelper.Random.NextDouble() < attributes.UsesTrain)
            return new HashSet<ModalChoice> { ModalChoice.Train };
        
        if (RandomHelper.Random.NextDouble() < attributes.UsesBus)
            return new HashSet<ModalChoice> { ModalChoice.Bus };
        
        return new HashSet<ModalChoice> { ModalChoice.Walking };
    }
}