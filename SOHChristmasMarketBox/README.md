# SOHChristmasMarketBox

ChristmasMarketBox is a scenario that models the movement of agents within a marketplace, using the central Christmas Market of Hamburg as a realistic environment. It provides a simple yet powerful framework to simulate, visualize, and compare different pedestrian movement models.

## Model Description

The simulation runs by spawning a number of `MarketTravelers` who are tasked with visiting the Christmas Market. Their behavior is dependent by the state the agents are in:

1.  **Walking To Market:** The agent navigates from the starting point to the market.
2.  **On Market:** Once inside, the agent randomly selects a market stall, moves towards it, waits for a short time, and then chooses a new stall. While walking around the market, the travelers use specific movement models. This loop continues until a random chance determines it's time to leave.
3.  **Walking Home:** The agent navigates from its current position back to its starting point and is then removed from the simulation.

## Getting Started

### Prerequisites

SOHChristmasMarketBox can be executed as soon as all of the required dependencies are installed. Please check the [main README.md file](https://github.com/MARS-Group-HAW/model-soh) for this.

### Running Instructions

Visit the root directory of the project and run the following command: 
```
dotnet build
```

After successfully building the project, you can continue navigating into this box and run the simulation by using following commands:

```
cd SOHChristmasMarketBox
dotnet run
```

### Configuration

This scenario offers configuration options for running the simulation. While in the `SOHChristmasMarketBox` directory:

`Program.cs` is the main entry point for the simulation. Agents, Layers and Entities can be added here for the simulation.

`config.json` is read by the simulation and contains customizable parameters:

*   **Simulation Time**
    *   `startPoint`
    *   `endPoint`

*   **Agent Configuration**
    *   Set agent `name` to switch movement models (e.g., `OptimalStepsMarketTraveler`).
    *   Adjust agent parameters like `PreferredSpeed`.

*   **Environment Files**
    *   **Walk Graph:** `walk_graph_rathausmarkt.geojson`
    *   **Market Stalls:** `market_stalls.geojson`
    *   **Agent Spawn Schedule:** `human_traveler_rathausmarkt.csv`

*   **Market Edges**
    *   `topLeftCorner`
    *   `topRightCorner`
    *   `bottomRightCorner`
    *   `bottomLeftCorner`

> **Important**: Please make sure that when selecting a different agent in `config.json`, the same agent must also be registered in `Program.cs`.


## Project Structure

Classes that this project uses for the simulations are located in `SOHModel/ChristmasMarket`.

The project is organized into several folders, each with a specific function. This separation of concerns makes the simulation easier to understand, maintain, and extend.

```
.
└── SOHModel/
    └── ChristmasMarket/
        ├── Agents/
        ├── Analytics/
        ├── Entities/
        ├── Layers/
        ├── MovementModels/
        └── Utils/
```

### Agents

This folder contains the core actors of the simulation. The abstract `MarketTraveler` class defines the high-level behavior of a visitor, such as choosing which stall to visit and deciding when to leave the market. The concrete implementations use different movement models to navigate the environment.

- `MarketTraveler.cs` (abstract base class)
- `CollisionFreeSpeedMarketTraveler.cs`
- `OptimalStepsMarketTraveler.cs`
- `SocialForceMarketTraveler.cs`

### Analytics

This directory holds classes responsible for gathering, processing, and outputting data from the simulation. It allows for the analysis of the simulation's results, such as stall popularity and average visitor duration.

- `ChristmasMarketAnalysics.cs`

### Entities

Entities are passive objects within the simulation environment. This folder defines the structure and types of objects that agents can interact with, such as the market stalls.

- `MarketStall.cs`
- `MarketStallType.cs`

### Layers

Layers are responsible for managing the simulation's environment and the agents within it. The `MarketLayer` handles the static components like the market boundaries and the locations of stalls, while the `MarketTravelerLayer` manages the dynamic agents.

- `MarketLayer.cs`
- `MarketTravelerLayer.cs`
- `IMarketTravelerLayer.cs` (Interface)

### MovementModels

This folder contains the different algorithms that control how agents move within the market. Each model implements the `IPedestrianMovementModel` interface, allowing for different agent behaviors to be easily swapped and tested.

- `IPedestrianMovementModel.cs` (Interface)
- `CollisionFreeSpeedMovementModel.cs`
- `OptimalStepsMovementModel.cs`
- `SocialForcesMovementModel.cs`

### Utils

This directory contains helper classes and utility functions that are used across the project. These utilities handle common tasks like geographic calculations and conversions.

- `MarketCoordinateConversionUtils.cs`
- `PolygonUtils.cs`

## Future Work

Thinking about implementing a new movement model into this scenario? Integrating a new movement model is pretty straight forward due to the project design. Here’s how you can do it:

1.  **Create the Movement Logic**<br> In the `MovementModels` folder, create a new class that implements the `IPedestrianMovementModel` interface. This class will contain the core algorithm for your new model inside the `CalculateNextPosition()` method.

2.  **Create the Agent**<br> In the `Agents` folder, create a new agent class that inherits from the abstract `MarketTraveler` class (e.g., `NewModelMarketTraveler.cs`).

3.  **Link Both**<br> Inside your new agent class, simply create an instance of your new movement model and call it from the overridden `CalculateNextMovementStep()` method. The `MarketTraveler` base class will handle the rest.

4.  **Add them to the scenario**<br> Make sure to update the `Program.cs` file to add your new agent to the simulation and update the name of the agent within the `config.json` file.

5. **Run the Simulation**<br> You're all set! Now you can get to start testing your own model.