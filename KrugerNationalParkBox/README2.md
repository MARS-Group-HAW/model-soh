# Kruger National Park: Base Model

## Resources

The directory `resources` contains a set of files with data that describe aspects of the KNP as well as the agents that explore it during a simulation.

### Agents and Entities

- **car.csv:** an initialization file that spawns a single car TODO at the beginning of the simulation
- **car_init.csv:** an initialization file that spawns a set of cars TODO at the beginning of the simulation
- **elephant1989_constant_population_7,5k.csv:** an initialization file that spawns 7,500 elephant TODO agents at the beginning of the simulation
- **elephant1989_constant_population_15k.csv:** an initialization file that spawns 15,000 elephant  TODO agents at the beginning of the simulation
- **OLDcar_init.csv:** a deprecated initialization file to spawn a set of cars TODO at the beginning of the simulation
- **tourist_scheduler.csv:** a scheduling file that spawns tourist TODO agents at parametrizable locations and intervals throughout a simulation
- **tourist_scheduler1.csv:** a scheduling file that spawns tourist TODO agents at parametrizable locations and intervals throughout a simulation

### Networks

- **knp_graph.geojson:** the drive graph of the KNP encoded in the GEOJSON format
- **knp_graph.graphml:** the drive graph of the KNP encoded in the GraphML format

### Layers

- **gis_raster_biomass_ts.zip:** a set of ASC files that make up a time series (from 1979 to 2099) of biomass amount/density? (TODO) in the KNP, encoded as a raster-layer
- **gis_raster_border.zip:** an ASC file containing the shape of the boundary of the KNP
- **gis_raster_shade.zip:** an ASC file containing the amount of shade available in the KNP (encoded as positive integer values on a grid)
- **merged_waters_fixed_with_fence_buffer.geojson:** the locations of waterholes in the KNP, encoded as a vector-layer
- **RCP4.5_2010_2050_temp.zip:** a time series that contains a *moderate* scenario of climate change development and its impact on the KNP for 2010-2050
- **RCP8.5_2010_2050_temp.zip:** a time series that contains a *severe* scenario of climate change development and its impact on the KNP for each day from 2010 to 2050
- **shade_layer_vectorized.geojson:** the amount of shade available in the KNP (encoded as a vector-layer file)
- **veg_knp_wifi_RCP45.zip:** a time series (from 1979 to 2099) of biomass amount/density? TODO in the KNP as a result of the moderate RCP 4.5 climate scenario, encoded as a raster-layer
- **veg_knp_wifi_CRP85.zip:** a time series (from 1979 to 2099) of biomass amount/density? TODO in the KNP as a result of the severe RCP 8.5 climate scenario, encoded as a raster-layer



## Agent Types

### Commuter

The commuter does cool things.



### DailyTourist



### OvernightTourist



### OSV



## Layers





## Tools

### Jupyter Notebooks



### C# Distance Table (creates csv)

