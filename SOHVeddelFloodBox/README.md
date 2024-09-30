<h1 align="center"> Projekt: Evakuierungszenario für Sturmfluten</h1>

# Teilnehmer:

| Teilnehmer | Rolle                                         |
|------------|-----------------------------------------------|
| ***        | Demographie, Verhalten der Agenten            |
| ***        | Straßen-Höhenmodell und Wassermodell          |
| ***        | Visualisierung und Layer aus QGIS             |
| ***        | Visualisierung und Layer aus QGIS             |
| ***        | Demographie, Verhalten der Agenten            |
| ***        | Verhalten, Haushaltspawning, Logdateiein, ... |

# Motivation

- Naturkatastrophen häufen sich, werden stärker
- Klimawandel
- Klimaschutz
- Sturmflut 1962

# Forschungsfrage

- Welche Auswirkungen haben Frühwarnsysteme auf die Evakuierung bei einer Sturmflut?
    - Szenario 1: ein Frühwarnsystem (Evakuierung eine Stunde vor der Flut)
    - Szenario 2: kein Frühwarnsystem
- Welche Auswirkungen hat unterschiedliches Verhalten auf die Evakuierung?
- Bezug: Aufklärung in Risikogebieten

# Erklärung der genutzten Layer

- HouseholdLayer: enthält die Haushalte (hieraus werden die Agenten gespawnt)
- SpatialGraphMediatorLayer: enthält das Straßennetz mit den ergänzten Höhenangaben als Attribut
- WaterLevelLayer: enthält eine Tick()-Methode zum Steigen des Wasserpegels

# Fazit/Interpretation der Ergebnisse

- Annahme bestätigt, dass Frühwarnsystem Leben rettet
- Verspätet startende Agenten sterben trotzdem
- Informationsfluss und Aufklärung sehr wichtig
    - (Wann sollte man sich nach der Warnung auf den Weg machen?)
- Sprachbarrieren vermindern

# Ausblick

- Auf andere Stadteile ausweiten
- Auf andere Paniksituationen ausweiten
- Menschen die mit Autos/ Fahrrädern flüchten
- Engpässe in den Straßen
- Transport der Ankommenden an der Sammelstelle

# Abschlusspräsentation

Die Abschlusspräsentation ist in [Veddel_Hochwasser_final.pdf](Veddel_Hochwasser_final.pdf)

# Anleitung (Starten der Simulation)

- Es muss die `SOHTravellingBox` genutzt / gestartet werden

- In der `config.json` können die Parameter für Überflutung angepasst werden
    - `ticks_before_start`: Die Zeit in Sekunden bevor die Flut steigt
    - `height_per_tick`: Das Steigen der Flut in m/S
- In der `HouseholdLayer` können die Rahmenbedingungen für die Agentensimulation festgelegt werden:
    - `population`: total number of agents
    - `numHousehold`: total number of housholds
    - `numSingleHousehold`: number of households only have one agent (must be smaller than households)
    - `familyDistanceThreshold`: distance in m a familymember is considered separated form the rest of the family. A
      meetingpoint the decided then where the family meets again;
    - `minTargetDistance`: Distance the agent is considere at it's goal
    - `numSpawnPoints`: Number of houses used for the simulation. These are selected randomly for all houses
    - `WaitingHouseholds`: Number of households that are not evacuating
    - `delayHouseholds`: NUmber of households that are waiting and then evacuating
    - `minDelayTime`: Time in seconds the delayed households will at least wait
    - `maxDelayTime`: Time in seconds the delayed households will wait at max
- `Anschließend` kann die Simulation über Rider kompliert und gestartet werden

- im Ordner "/SOHTravellingBox/out" werden Logdateien zur Simulation erzeugt
    - `arrivalGraph.csv`: enthält die überlebenden Agenten über die Zeit
    - `deadAgentsGraph.csv`: enthält die gestorbenen Agenten über die Zeit
    - `waterLevel.csv`: enthält den Wasserpegel über die Zeit
    - `houses.csv`: enthält die Geo-Koordinaten der genutzten Haushalte
    - `config.csv`: enthält alle Konfigparameter
    - `familyDistribution`: enthält die Verteilung der größe Haushalte
    - `genderDistribution.csv`: enthält die Geschlechtervteilung
    - `householdTypeDistribution.csv`: enthält die Verteilung der 3 Agent Typen (Aktiv, Delayed, Inactive)

- Die `trips.geojson` kann in Kepler.gl dargestellt werden ("/SOHTravellingBox/bin/Debug/net6.0/trips.geojson")

- Über das Jupyter-Notebook `simulaion_analysis.ipynnb` können die Ergebnisse graphisch visualisiert werden
    - Die Simulation über Rider starten
    - Nach Beendigung werden CSV-Dateien im Ordner /SOHTravellingBox/out erstellt
    - simulaion_analysis.ipynnb ausführen um die Visualisierung zu aktualisiren

- Für die Präsentation wurden zusätzlich Grafiken aus der `presentation_analysis.ipynb` genutzt
    - Als Input dienen hier die Simulationensergebnisse der Ordner `/SOHTravellingBox/out*`
