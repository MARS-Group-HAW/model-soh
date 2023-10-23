# Installation

Open your terminal and navigate into this directory. 

Install `python3.8` (getting from [Python](https://www.python.org/downloads/))

Execute the following command:

```bash
pip3 install -r requirements.txt
```

# Start

Start the mini visualization by calling:

```bash
python3 main.py
```

Start you desired simulation and activate the visualization output in your configuration by setting the field `pythonVisualization` to `true`

```json
{
 "globals": {
   "deltaT": 1,
   "steps": 1000,
   "console": true,
   "pythonVisualization" : true
 }
  // ... your agent, entities and layer mappings
}
```

If the model is configured from within the `Program.cs` file, then setting the field `EnableSimpleVisualization` to `true` enables the visualization mode.

```c#
Globals =
{
    DeltaTUnit = TimeSpanUnit.Seconds,
    Steps = 1000,
    ShowConsoleProgress = true,
    EnableSimpleVisualization = true
}
// ... your agent, entities and layer mappings
```

