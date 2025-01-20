Zielsetzung: 

Ziel dieses Projekts ist es den im Rahmen des Cooperative Intelligent Transport System (C-ITS) Projekts der Stadt Hamburg geplanten Dienst Traffic Signal Priority Request (TSP) in SOHH einzubinden. Im Kern sollen Einsatzfahrzeuge, Busse, etc. bei den entsprechenden Lichtsignalanlagen Anfragen auf eine priorisierte Behandlung stellen können, die dann zu einem kurzzeitig veränderten Ampelzyklus führt, damit diese Fahrzeuge (schneller) grün bekommen und entsprechend schneller an ihr Ziel gelangen. 

Teammitglieder und Rollen:

Leon Rickert,
Abdol-Rahman Karrar,
Paiman Karimy,
Ruben Marin Grez (Teamleiter)

Anleitung zur Auswertung und Visualisierung: 

Nachdem die Simulation ausgeführt wurde, liegen unter SOHC-ITSBOX/bin/Debug/net8.0 csv Dateien als Output vor. Um diese in geojson Dateien umzuwandeln, steht die csv_to_geojson.py Datei bereit. Beim Ausführen werden entweder eine geojson Datei für die CarDriver erstellt (output.geojson) oder eine für die EmergencyCarDriver (e_output.geojson), je nachdem welche Option der Nutzer zur Laufzeit wählt. Im Ordner auswertung sind Beispiel-Dateien zu finden. Bei den Beispiel-Dateien gibt es zum einen geojson Output mit EmergencyCarDrivern, die priority requests an Ampeln stellen und Output bei dem keine priority requests gestellt werden. 

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

Die Umsetzung des TSP erfolgt in der VehicleSteeringhandle.cs bzw. in TrafficLightController.cs.
Es wird das Prinzip der Red Truncation benutzt.
Dabei gibt das Feld SirenActive an, ob der entsprechende EmergencyCarDriver am TSP teilnimmt. 
Wird in VehicleSteeringHandle eine Kreuzung mit einer roten Ampel erkannt, wird durch die Methode OnDuty() festgestellt ob sich der EmergencyCarDriver im Einsatz befindet. Daraufhin wird der Entsprechende TrafficLightController gefetched und die Methode priorityRequest aufgerufen. Dann prüft der TrafficLightController, ob bereits ein priorityRequest vorliegt, falls ja, wird der neue ignoriert. Ist dies nicht der Fall, wird die Zykluslänge für die Ampelphase gekürzt und die ursprüngliche Länge zwischengespeichert. Dadurch wird die Länge der Rotphase verkürzt. Sobald die Ampel auf grün schaltet, wird die Ursprüngliche Zykluslänge wiederhergestellt. 






