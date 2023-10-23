using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Mars.Interfaces.Agents;
using Mars.Interfaces.Environments;
using SOHBicycleModel.Common;
using SOHBicycleModel.Model;
using SOHBicycleModel.Steering;
using SOHCarModel.Model;
using SOHCarModel.Steering;
using SOHDomain.Steering.Common;

namespace SOHTests.Commons.Agent;

[SuppressMessage("ReSharper", "UnassignedGetOnlyAutoProperty")]
public class TestCapabilitiesAgent : IModalCapabilitiesAgent, ICarSteeringCapable, IBicycleSteeringCapable
{
    public TestCapabilitiesAgent(params ModalChoice[] choices) : this(choices.ToHashSet())
    {
    }

    public TestCapabilitiesAgent(ISet<ModalChoice> choices)
    {
        ModalChoices = choices;
    }

    public double DriverRandom { get; }
    public DriverType DriverType { get; }
    public double CyclingPower { get; }
    public double Mass { get; }
    public double Gradient { get; }
    public Bicycle Bicycle { get; set; }
    public Position Position { get; set; }

    public void Notify(PassengerMessage passengerMessage)
    {
        throw new NotImplementedException();
    }

    public bool OvertakingActivated { get; }
    public bool BrakingActivated { get; set; }
    public Car Car { get; set; }
    public bool CurrentlyCarDriving { get; }

    public void Tick()
    {
        throw new NotImplementedException();
    }

    public Guid ID { get; set; }
    public ISet<ModalChoice> ModalChoices { get; }
}