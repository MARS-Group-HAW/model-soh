# SOHAnomalyBox

This project is a specialized multi-agent simulation built on the **MARS (Multi-Agent Research and Simulation)** framework. It models visitor behavior, pedestrian dynamics, and social interactions within the specific context of the Hamburg Christmas Market (Rathausmarkt) located in front of the historic city hall.

---

## 1. Project Overview
The primary goal of this simulation is to analyze how large crowds interact with confined urban spaces during major events. By simulating individual agents with varying needs and preferences, the model provides insights into crowding patterns, stall popularity, and overall pedestrian flow.

### Key Simulation Targets:
- **Agent Mobility**: Realistic movement across a geospatial graph representing sidewalks and market areas.
- **Micro-Social Behavior**: Agents react to internal states (needs) and environmental cues (market stalls).
- **Automated Analytics**: Real-time extraction of simulation metrics for post-hoc analysis.

---

## 2. Simulation Scenario: Rathausmarkt 2024
The model is pre-configured for the **Christmas Season of 2024**. 
- **Start Time**: December 10th, 2024, at 18:00 (Peak time).
- **Location**: Hamburg Rathausmarkt.
- **Environment**: A high-density Christmas market environment with over 80 individual stalls.

---

## 3. Layer Architecture
The simulation follows the standard MARS layer-based architecture, where each layer handles a specific aspect of the environment or agent management.

### a) SpatialGraphMediatorLayer
This layer is the backbone of movement. It loads a GeoJSON graph (`walk_graph_rathausmarkt.geojson`) which defines all walkable paths.
- **Modality**: Supports "Walking".
- **Graph Type**: Bidirected graph (agents can walk in both directions on any edge).

### b) MarketLayer
Manages the "static" entities of the market.
- **Entity**: `MarketStall`.
- **Function**: Spatially indexes the stalls so agents can queries "nearest stalls" or find specific types of products.
- **Bounding Box**: Defined in `config.json` via four GPS corners to clip the interactive area.

### c) MarketTravelerLayer
The "acting" layer for pedestrians.
- **Host**: Managing the `DesireMarketTraveler` agents.
- **Context**: Provides social context for agents (knowing who else is in the vicinity).

### d) MarketTravelerSchedulerLayer
A specialized scheduler layer that populates the simulation over time.
- **Input**: `human_traveler_rathausmarkt.csv`.
- **Logic**: Reads a schedule and spawns agents at specific intervals (bursts or steady flows) to simulate the arrival of visitors from nearby public transport stations.

---

## 4. Agents and Entities

### 4.1. DesireMarketTraveler (The Visitor)
Each traveler has a state machine or utility-based decision logic.
- **Need States**:
    - `hunger`: Increases over time. Drives the agent to find food stalls.
    - `thirst`: Increases over time. Drives the agent to find drink stalls.
    - `budget`: Limits the number of interactions an agent can have.
- **Movement**:
    - Uses A* pathfinding on the spatial graph.
    - Variable `PreferredSpeed` (default 1.4 m/s).
- **Output**: Trajectories can be exported to PostgreSQL for visualization.

### 4.2. MarketAnalyticsAgent (The Observer)
A singleton agent (count=1) that doesn't walk but "monitors" the simulation.
- **Output Frequency**: Configurable (e.g., every 60 seconds).
- **Data Points**: Active visitor count, average satisfaction, and stall occupancy levels.

---

## 5. Configuration Guide (`config.json`)

The `config.json` file is the central place to tune the simulation.

### Global Settings
- `deltaT`: Step size in simulation units (default: 1 second).
- `startPoint` & `endPoint`: Define the simulation window.
- `npgSqlOptions`: Database credentials for the PostgreSQL export.

---

## 6. Infrastructure & Deployment

### Docker Setup
The project uses a PostGIS-enabled database container to handle spatial data.
- **Image**: `postgis/postgis:18-3.6`
- **Port**: `5432`
- **Volume**: Persists data in `.postgres-data` to survive container restarts.

### Database Initialization
A critical file is `init_mars_schema.sql`. MARS usually creates columns in lowercase by default. Our SQL script uses an **Event Trigger** to automatically:
1. Detect when MARS creates a table (e.g., `desiremarkettraveler`).
2. Rename columns to **PascalCase** (e.g., `activecapability` -> `ActiveCapability`).
3. Ensure compatibility with the C# model's expectations during runtime.

---

## 7. How to Use

### Prerequisites
- .NET 8.0 SDK
- Docker Desktop (or Linux Docker Engine)

### Step 1: Initialize Database
Run the following in the project root:
```powershell
docker-compose up -d
```

### Step 2: Build Project
```powershell
dotnet build
```

### Step 3: Run Simulation
```powershell
dotnet run
```
*Note: Ensure that the `config.json` is located in the output directory (bin) or next to the .csproj file.*

---

## 8. Analyzing Results

Once the simulation finishes (or while it is running), you can query the database:

**Check Visitor Counts:**
```sql
SELECT tick, "ActiveVisitors" 
FROM rathausmarkt_2024.marketanalyticsagent 
ORDER BY tick DESC;
```

**Visualize Trajectories:**
Export the `rathausmarkt_2024.desiremarkettraveler_trips` table to a GeoJSON and upload it to [Kepler.gl](https://kepler.gl).

---

