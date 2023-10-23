<h1 align="center">SmartOpenHH Resources</h1>

This directory contains external data and files that are required by the SmartOpenHamburg models during compilation and execution.

`ResourcesConstants.cs` is the central location of attributes that store paths to the different directories that contain files. These attributes can be called from anywhere within your model.

## Directory tree

`res` → contains the directories that hold different types of model-external data and files

​	`agent_inits` → contains initialization files (.csv) that list attribute values of agents of a specific agent type at the start of a simulation

​	`entity_inits` → contains initialization files (.csv) that list attribute values of entities of a specific entity type at the start of a simulation

​	`networks` → contains graphs that are used to generate the SpatialGraphEnvironment (SGE) of a model (SGE is the graph that agents can move on).

​	`sim_configs` → contains simulation configuration files (json) that allow for model configuration before starting a simulation

​	`traffic_lights` → contains files that list coordinates (longitude, latitude) of traffic lights in a simulation area

​	`traffic_lights_altona` → contains files that list coordinates (longitude, latitude) of traffic lights specifically for the district Altona, Hamburg

​	`traffic_lights_harburg` → contains files that list coordinates (longitude, latitude) of traffic lights specifically for the district Harburg, Hamburg

​	`vector_data` → contains files (geojson) with geospatial information that is used to populate layers of a model



For more information on how to prepare and process external data for a MARS LIFE model, go to https://mars.haw-hamburg.de/articles/intro.html and check out the articles under Data Sources.