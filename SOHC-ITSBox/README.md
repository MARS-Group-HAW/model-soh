# SOHC-ITSBox

## Table of Contents
- [Project: Smart-Open-Hamburg Cooperative Intelligent Transport Systems](#project-smart-open-hamburg-cooperative-intelligent-transport-systems)
    - [Overview and Objective](#overview-and-objective)
        - [What is a C-ITS?](#what-is-a-c-its)
    - [ModelDescription](#modeldescription)
    - [Globals](#globals)
    - [Layer Mappings](#layer-mappings)
    - [AgentMappings](#agentmappings)

## Project: Smart-Open-Hamburg Cooperative Intelligent Transport Systems
___

### Overview and Objective

#### What is a C-ITS?
A Cooperative Intelligent Transport System (C-ITS) extends the concept of Intelligent Transport Systems by enabling communication between vehicles, infrastructure, and other road users. This cooperation enhances the overall efficiency and safety of the transport system.

Currently, a prototype of a Cooperative Intelligent Transport System (C-ITS) is being developed in Hamburg. However, the impact of features such as automatic traffic light phase changes upon the approach of buses and emergency services (police and ambulances) on city traffic remains unclear.

**Objective**  
The goal of this project is to add the necessary elements to the Smart Open Hansestadt Hamburg (SOHH) project to conduct a study through simulations.

## ModelDescription
___
This section should describe the model used in the project, including its components and how they interact.

## Globals
___
This section should list and describe any global variables or settings used throughout the project.

## Layer Mappings
___
This section should explain the mappings between different layers in the project, detailing how data flows between them.

## AgentMappings
___
This section should describe the mappings for different agents within the project, including their roles and interactions.

## Datenakquisition

**1. Karte aus dem Area of Interest ausschneiden und einfügen**

Hierfür auf die Steps 1 und 2 aus dem MARS geo-referenced Model Starter im folgendem Link ausführen. Als Ergebnis bekommt man eine Geojson in der 5 Koordinaten drinnen sind, die ein Polygon bilden. Der Link dazu: https://github.com/MARS-Group-HAW/blueprint-geovector/blob/main/README.md

- Diese geojson wird für das CarLayer verwendet und dienen den Autos als Fahrstrecke. Hier heißt sie c_its_teststrecke_street_graph.geojson und wird in config.json eingebunden.
- Die 5 Koordinaten aus dem Polygon werden für Schritt 2 verwendet.

**2. Koordinaten der Ampeln**

Zuerst müssen alle Koordianten der Ampeln aus dem Area of Interest aus Schritt 1 gefunden werden. Dafür wird das Script fetch_traffic_lights.py genutzt. Dieses Script sucht und fetched aus der API 'https://tld.iot.hamburg.de/v1.0/Datastreams' aus allen Ampeln (aus Hamburg), die Ampeln aus dem Gebiet für das man sich interessiert. Füge dafür in die Variable polygon_coords die 5 Koordinaten aus dem Polygon aus Schritt 1 ein.

Das Ergebnis steht in traffic_lights_observations.json. Um nur Koordianten daraus zu extrahieren verwende das Script generate_csv.py. Der Output traffic_lights_observations.csv enthällt jetzt nur die Ampel-Koordinaten aus dem Area of Interest.

**3. Live Ampel-Phasen**

Um die Live Ampel-Phasen zu den jeweiligen Ampeln