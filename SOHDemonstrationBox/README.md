# Simulation Model of Demonstration and Police Interaction with MARS

Project members: ***, ***, ***, ***, ***

## Tasks and Responsibilities
The work that was done for this project can be broken down (roughly) into the following categories.

- Recherche 
  - Demonstrationsdaten
- Datenakquise 
  - Erstellung von georeferenzierten Daten mit QGIS und `osmnx`
- Konfiguration 
  - Dynamisches Einladen von Geometrien
  - Scheduler
  - Parametrierung von Agenten
- Initialisierung 
  - Registrierung von Agenten und Layer
  - `PoliceChief` (initiale Verteilung von `Police`)
- Agenten 
  - Verhalten von `RadicalDemonstrator`
  - Verhalten der `Police`
  - Routenplanung und -anpassung
- Auswertung 
  - Erstellung von Diagrammen anhand von CSV-Ausgabedateien
  - Visualisierung in [kepler.gl](https://kepler.gl) (Einfärbung über agent_type))

The following table shows the tasks that each member worked on and contributed to. When there is no specification in parentheses, the member took part in all of the sublisted topics.

| Teilnehmer*in         | Aufgaben                                                                                                                                                             |
|-----------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| *** | Recherche, Datenakquise, Konfiguration (Scheduler, Parametrierung der Agenten), Initialisierung (`PoliceChief`, Verteilung von `Police`), Agenten, Auswertung ([kepler.gl](https://kepler.gl))               |
| ***           | Recherche, Konfiguration (Scheduler) Initalisierung (Agenten und Layer), Agenten (Verhalten von `RadicalDemonstrator`, `Police`), Auswertung ([kepler.gl](https://kepler.gl))                |
| ***      | Konfiguration (Scheduler, dynamisches Einladen von Geometrien), Agenten (Verhalten von `RadicalDemonstrator`), Auswertung ([kepler.gl](https://kepler.gl))                                             |
| ***     | Recherche, Datenakquise, Konfiguration (Scheduler, Parametrisierung der Agenten), Initialisierung (`PoliceChief`, Verteilung von `Police`), Agenten, Auswertung ([kepler.gl](https://kepler.gl)) |
| ***      | Datenakquise, Konfiguration (Scheduler, Parametrisierung der Agenten), Initalisierung, Agenten (Verhalten von `RadicalDemonstrator`, `Police`), Auswertung           |

## Project Overview 
This repository contains a simulation model of the "GeMAInsam Zukunft Gestalten" demonstration which took place in Hamburg, Germany on May 1st, 2022. The model is focused on interactions between radical demonstrators and police units. The goal is to study the examplary demonstration and derive optimal numbers of police units by comparing different simulation runs.

For a detailed description of the project, please see the presentation under `DemonstrationsProject/2022-06-08-final-praesentation.pdf`.

## Area of Interest

The following image illustrate the area of interst that was chosen for the model.

![OverviewMap](walkgraph_blockaden_route.png)

The map was generated using the GIS application (https://qgis.org)[QGIS]. The map in the background was obtained from the QGIS OpenStreetMap (OSM) plugin QuickOSM. The **red line** represents the demonstration route, going from north to south. The **blue points** on the left (dark blue) and right (light blue) of the demonstration route represent police stations at which `Police` agents might be present. The **thin black lines** on the traffic network represent the section of the Hamburg traffic network that is integrated into the model at runtime and on which the agents can move.

## Model Structure
The components and documents of the model are located in the following three directories:
- `SOHDemonstrationModel`: this is the model directory. It contains the directories `Agents` and `Layers`, which in turn include the files that define the model's agent types and layer types, respectively.
- `SOHDemonstrationBox`: this is the execution directory of the model. The files `config.json` and `Program.cs` as well as the initialization files are located here (in the directory `resources`).
- `DemonstrationsProject`: additional documents that were generated during the project, including presentation slides, a video of the simulation and results of the analysis of the data.

### Agent Types
- `Demonstrator`: a peaceful demonstrator who starts at the beginning of the demonstration route and moves to the end of the demonstration route without deviating from the demonstration route
- `RadicalDemonstrator`: a `Demonstrator` with radical intentions who is able to express radical behavior deviate from the demonstration route under certain conditions.
- `Police`: a squad of 10 police officers who guard one of the police stations (see image above) and can pursue `RadicalDemonstor` agents who express radical behavior
- `PoliceChief`: a special police officer who assigns `Police` agents to police stations at the beginning of a simulation

### Layer Types
- `DemonstrationLayer`: the layer on which all four agent types live
- `DemonstratorSchedulingLayer`: a scheduling layer that spawns a given number of `Demonstrator` and `RadicalDemonstrator` agents over a given period of time at a given interval
- `PoliceSchedulingLayer`: a scheduling layer for spawning `Police` agents (currently not used in the model).

## Model Configuration
This section lists the different configuration options and their locations within the model.

### General Configuration: Changing the number of agents
The number of `Demonstrator` and `RadicalDemonstrator` agents is defined in <code>demonstrator_scheduler.csv</code>, which is located in *\ki-projekt-demonstrationen\SOHDemonstrationBox\resources.

The number of `Police` agents can be changed in <code>config.json</code> located in *\ki-projekt-demonstrationen\SOHDemonstrationBox

### Configuring the RadicalDemonstrators
In *\ki-projekt-demonstrationen\SOHDemonstrationModel\Agents, there is a file <code>RadicalDemonstrator.cs</code> for the `RadicalDemonstrator` agent type. Here, the following configuration options are available:
- <code>MaxCurrentBreakoutCounter </code> -> Duration (in second) until a `RadicalDemonstrator` that is breaking out counts as escaped and its trail is considered lost.
- <code>DistanceForBreakingOut </code> -> The minimum distance (in meters) that must be between a `RadicalDemonstrator` and nearest `Police` for `RadicalDemonstrator` to break out
- <code>DistanceForReturning </code> -> The minimum distance (in meters) that must be between a `RadicalDemonstrator` and nearest `Police` for `RadicalDemonstrator` to return to demonstration route
- <code>MaxTicksUntilNextBreakout </code> -> Maximum number of ticks (i.e., seconds) until `RadicalDemonstrator` breaks out the next time (5 min * 60 sec/min = 300 sec)
- <code>MaxConditionCounter </code> -> Counter that represents the maximum condition/fitness of the `RadicalDemonstrator` (used to toggle between `Walking` and `Running`)

### Configuring the Police
In *\ki-projekt-demonstrationen\SOHDemonstrationModel\Agents, there is a file <code>Police.cs</code> for the Police agent type.

These are the configuration options:
- <code>MaxSearchDistance</code> -> The distance (in meters) that a `Police` agent can search for `RadicalDemonstrator` agents that are breaking out
- <code>MaxChasingCounter</code> -> Specifies the maximum number of ticks that a `Police` agent spends chasing a `RadicalDemonstrator` that is breaking out

### Potential Research Questions
Given the above parameter descriptions, a few exemplary research questions can be derived. These include, but are not limited to:
- How do different numbers of `Police` agents affect the number of successful breakouts by `RadicalDemonstrator` agents?
- How do different numbers of `RadicalDemonstrator` agents affect the success rate of `Police` agents?
- How do different levels of radicality (modelled by the property `DistanceForBreakingOut` of the `RadicalDemonstrator`) affect the success rate of `Police` agents?

----

## Running the model 
After configuring the model, the <code>Project.cs</code> file can be started by running `Program.cs` via an IDE (e.g., Rider). 

---

## Simulation Output and Visualization
After a simulation run, two <code>.csv</code> files <code>Police.csv</code> and <code>RadicalDemonstrator.csv</code> are generated in the *\ki-projekt-demonstrationen\SOHDemonstrationBox\bin\Debug\net6.0 folder. Here, the main parameter for evaluating the simulation can be found.

For `RadicalDemonstrator` agents, the following properties are interesting: 

- <code> BreakingOutCounter </code> -> number of ticks that a `RadicalDemonstrator` spent breaking out
- <code> BrokeOutCounter </code> -> number of times that a `RadicalDemonstrator` broke out
- <code> ReturningCounter </code> -> number of times that a `RadicalDemonstrator` returned to the demonstration route after breaking out
- <code> State </code> -> `State` of a `RadicalDemonstrator`, one of set {
    BreakingOut,
    Demonstrating,
    Returning,
    Escaped,
    Arrested}

For `Police` agent type, the following properities are interesting: 
- <code> ArrestCounter  </code> -> number of arrests made by a `Police`
- <code> currentChasingCounter </code> -> number of ticks the `Police` unit spent chasing `RadicalDemonstrator`
- <code> State </code> -> the `State` of a `Police`, one of set {
    Stationary,
    Chasing,
    Returning}

### Visualization
The agents' movement throughout a simulation can be visualized in [kepler.gl](https://kepler.gl).

The static resources like the demonstration route or the police stations can be added from the *\ki-projekt-demonstrationen\SOHDemonstrationBox\resources folder by into [kepler.gl](https://kepler.gl) via drag-and-drop. 

Additionally, the <code>trips.geojson</code> from the *\ki-projekt-demonstrationen\SOHDemonstrationBox\bin\Debug\net6.0 folder can be added to visualize the agents' movement and interactions over time.

## Quellen 
- [DEF17] Heribert Prantl. Pressefreiheit ist kein Schönwettergrundrecht. 2017. URL: https://www.deutschlandfunk.de/medien-nach-g20-pressefreiheit-ist-kein-100.html.
- [dpa21] Deutsche Presse-Agentur (dpa). CSU fordert mehr Geld für Sondereinsätze der Polizei. 2021. URL: https://www.nordbayern.de/politik/csu-fordert-mehr-geld-fur-sondereinsatze-der-polizei-1.10734650.
- [Ger22] Rose Gerdts-Schiffler. Teurer Extra-Service der Polizei. 2022. URL: https://www.weser-kurier.de/bremen/politik/teurer-extra-service-der-polizei-doc7e3wozwjn4z1acqqdcab.
- [Kin20] Juliane Kinast. Nachwuchssorge - Bundespolizei ändert Auswahltest. 2020. URL: https://www.wz.de/nrw/nachwuchssorge-bundespolizei-aendert-auswahltest-fuer-bewerber_aid-48447485.
- [Len21] Ulfia A. Lenfers u. a. „Improving Model Predictions—Integration of Real-Time Sensor Data into a Running Simulation of an Agent-Based Model“. In: Sustainability 13 (13. Juni 2021). Hrsg. von Philippe J Giabbanelli und Arika Ligmann-Zielinska, S. 7000. ISSN: 2071-1050. DOI: 10.3390/su13137000.
- [MDR22a] MDR Aktuell. UN-Experte: Systemversagen bei Polizeigewalt. 2022. URL: https://www.mdr.de/nachrichten/deutschland/corona-un-experte-melzer-zu-polizeigewalt-demos-100.html.
- [MDR22b] MDR Sachsen. Leipziger Polizei ermittelt nach Demo gegen Thüringer Kollegen. 2022. URL: https://www.mdr.de/nachrichten/sachsen/leipzig/leipzig-leipzig-land/ermittlungen-polizeigewalt-demonstration-100.html.
- [NDR21] Andrej Reisin. G20-Gipfel in Hamburg: Keine Anklage gegen Polizisten. 2021. URL: https://daserste.ndr.de/panorama/aktuell/G20-Gipfel-in-Hamburg-Keine-Anklage-gegen-Polizisten,gzwanzig418.html.
- [NDR22] Hamburg Journal. Tausende bei linken Demos am 1. Mai in Hamburg. 2022. URL: https://www.ndr.de/nachrichten/hamburg/Tausende-bei-linken-Demos-am-1-Mai-in-Hamburg,tagderarbeit174.html.
- [NP17] Markus Reuter. Journalistenverbände: Polizeigewalt gegen Reporter auf dem G20. 2017. URL: https://netzpolitik.org/2017/journalistenverbaende-polizeigewalt-gegen-reporter-auf-dem-g20/.
- [NRW17] Jochen Tack. NRW schafft die individualisierte Kennzeichnung ab. 2017. URL: https://polizei.nrw/artikel/nrw-schafft-die-individualisierte-kennzeichnung-ab.
- [ONL18] Handelsblatt Online. Polizeigewerkschaft rechnet mit Millionen-Kosten wegen Einsatz im Hambacher Forst. 2018. URL: https://www.wiwo.de/politik/deutschland/streit-um-hambacher-forst-polizeigewerkschaft-rechnet-mit-millionen-kosten-wegen-einsatz-im-hambacher-forst/23081452.html.
- [POL] Wikipedia. Polizeiverband. 2022. URL: https://de.wikipedia.org/wiki/Polizeiverband.
- [Pol19] Zum Ansehen der Polizei im Fokus der öffentlichen Meinung: neue Resultate aus Nordrhein-Westfalen. Jan-Volker Schwind. 2019. URL: https://www.kriminalpolizei.de/ausgaben/2019/juni/detailansicht-juni/artikel/zum-ansehen-der-polizei-im-fokus-der-oeffentlichen-meinung-neue-resultate-aus-nordrhein-westfalen.html.
- [TON22] EP t-online. So bereitet sich die Polizei in Hamburg auf den 1. Mai vor. 2022. URL: https://www.t-online.de/region/hamburg/news/id_92084000/1-mai-in-hamburg-auf-diese-demonstrationen-bereitet-sich-die-polizei-vor.html.
- [WOO02] Michael Wooldridge. An Introduction to Multiagent Systems. John Wiley & Sons, Ltd., Aug. 2002. ISBN: 978-0-47-051946-2.
- [Ver20] Bundesamt für Verfassungsschutz. Zahlen und Fakten. 2020. URL: https://www.verfassungsschutz.de/DE/themen/rechtsextremismus/zahlen-und-fakten/zahlen-und-fakten_node.html.
