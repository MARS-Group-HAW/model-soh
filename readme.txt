Zielsetzung: 

Ziel dieses Projekts ist es den im Rahmen des Cooperative Intelligent Transport System (C-ITS) Projekts der Stadt Hamburg geplanten Dienst Traffic Signal Priority Request (TSP) in SOHH einzubinden. Im Kern sollen Einsatzfahrzeuge, Busse, etc. bei den entsprechenden Lichtsignalanlagen Anfragen auf eine priorisierte Behandlung stellen können, die dann zu einem kurzzeitig veränderten Ampelzyklus führt, damit diese Fahrzeuge (schneller) grün bekommen und entsprechend schneller an ihr Ziel gelangen. 

Teammitglieder und Rollen:

Leon Rickert,
Abdol-Rahman Karrar,
Paiman Karimy,
Ruben Marin Grez (Teamleiter)

Design Überblick: 

Wir haben eine neue Klasse EmergencyCarDriver erstellt, die den CarDriver zum Vorbild hat. 

plantuml code: 

@startuml

namespace SOHModel.Car.Model {
    class EmergencyCarDriver {
        - CarSteeringHandle _steeringHandle
        - UnregisterAgent _unregister
        - ISpatialGraphEnvironment _environment
        - CarLayer Layer
        - Car Car
        - bool SirenActive
        - string StableId
        - string TrafficCode
        - bool OvertakingActivated
        - bool BrakingActivated
        
        + EmergencyCarDriver(CarLayer, RegisterAgent, UnregisterAgent, int, double, double, double, double, ISpatialEdge, string, string)
        + void ActivateSiren()
        + void DeactivateSiren()
        + bool OnDuty()
        + void Notify(PassengerMessage)
        + override void Tick()
        + Position Position { get; set; }
        + Route Route { get; }
        + double Latitude { get; }
        + double Longitude { get; }
        + string NextTrafficLightPhase { get; }
        + double Velocity { get; set; }
        + double VelocityInKm { get; }
        + double MaxSpeed { get; set; }
        + double SpeedLimit { get; }
        + double RemainingDistanceOnEdge { get; }
        + double PositionOnEdge { get; }
        + bool GoalReached { get; }
        + double RemainingRouteDistanceToGoal { get; }
        + string CurrentEdgeId { get; }
    }
}

@enduml
