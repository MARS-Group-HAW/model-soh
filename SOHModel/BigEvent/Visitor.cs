using Mars.Common.Core.Random;
using Mars.Interfaces.Annotations;
using Mars.Interfaces.Environments;
using Microsoft.AspNetCore.Components.Sections;
using SOHModel.Bicycle.Parking;
using SOHModel.Multimodal.Model;

namespace SOHModel.BigEvent;

/// <summary>
///     This <see cref="Traveler{BigEventLayer}" /> entity uses a variety of modalities to reach its goal.
/// </summary>
public class Visitor : Traveler<BaseWalkingLayer>
{
    private ISet<ModalChoice> _choices;
    private ModalChoice _preferred;
    [PropertyDescription] public IBicycleParkingLayer BicycleParkingLayer { get; set; }

    public override void Init(BaseWalkingLayer layer)
    {
        base.Init(layer);
        OvertakingActivated = true;
        _choices = new ModalityChooser().Evaluate(this);
        _choices.Add(ModalChoice.Walking);
        
        handleLogic();
        const int radiusInM = 1000;
        
        if (_choices.Contains(ModalChoice.CyclingOwnBike) && BicycleParkingLayer != null)
        {
            Bicycle = BicycleParkingLayer.CreateOwnBicycleNear(StartPosition, radiusInM, 1.0);
            Console.WriteLine("Bike created at " + Bicycle.Position);
        }

        if (_choices.Contains(ModalChoice.CarDriving) && CarParkingLayer != null)
        {
            Car = CarParkingLayer.CreateOwnCarNear(StartPosition, radiusInM);
            Console.WriteLine("Car created at " + Car.Position);
        }
    }
    

    /**
     * This method handles the logic of the modal choices.
     * It removes the modal choices that are not compatible with the selected modal choice.
     * For example, if the visitor chooses to drive a car, they won't be able to choose to take the bus or train or co-drive the car.
     */
    private void handleLogic()
    {
        var modalProbabilities = new Dictionary<ModalChoice, double>
        {
            { ModalChoice.CarDriving, UsesCar },
            { ModalChoice.CoDriving, UsesCoDriving },
            { ModalChoice.Bus, UsesBus },
            { ModalChoice.Train, UsesTrain },
            { ModalChoice.CyclingOwnBike, UsesBike }
        };
        
        var filteredChoices = _choices
            .Where(choice => modalProbabilities.ContainsKey(choice))
            .ToDictionary(choice => choice, choice => modalProbabilities[choice]);
        
        if (filteredChoices.Any())
        {
            _preferred = filteredChoices
                .OrderByDescending(kvp => kvp.Value) 
                .First().Key; 
        } else {
            _preferred = ModalChoice.Walking;
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
            Console.WriteLine("Preffered modal choice: " + _preferred);
            return MultimodalLayer.Search(this, StartPosition, GoalPosition, _preferred);
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
        HashSet<ModalChoice> choices = new();
        if (RandomHelper.Random.NextDouble() < attributes.UsesCar)
            choices.Add(ModalChoice.CarDriving);
        
        if (RandomHelper.Random.NextDouble() < attributes.UsesCoDriving)
            choices.Add(ModalChoice.CoDriving);

        if (RandomHelper.Random.NextDouble() < attributes.UsesBike)
            choices.Add(ModalChoice.CyclingOwnBike);

        if (RandomHelper.Random.NextDouble() < attributes.UsesTrain)
            choices.Add(ModalChoice.Train);
        
        if (RandomHelper.Random.NextDouble() < attributes.UsesBus)
            choices.Add(ModalChoice.Bus);
        
        return choices;
    }
}