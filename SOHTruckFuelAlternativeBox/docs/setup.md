# Setup and running

Run commands from `SOHTruckFuelAlternativeBox/` unless stated otherwise.

## Prerequisites

- .NET 10 SDK.
- PostgreSQL/PostGIS, either existing or provided by Docker Compose.
- The road-network GeoJSON files referenced by `config.json`. See
  [SOHLogisticsBox's preprocessing documentation](../../SOHLogisticsBox/README.md#map-export--preprocessing)
  rather than rebuilding the shared network differently for this box.

## Database

Copy `.env.example` to `.env` and configure `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USER`, and `DB_PASSWORD`.
Keep credentials out of version control.

```bash
cp .env.example .env
docker compose up -d
```

The Compose service is `mars_postgis`, publishing port 5432. If the output database does not exist, create it
once (the checked-in configuration uses `soh_truck_fuel_alternative`):

```bash
docker compose exec mars_postgis sh -c 'createdb -U "$POSTGRES_USER" soh_truck_fuel_alternative'
```

There are two output connections to configure consistently: the custom event logger reads `.env`, while MARS
agent output uses `globals.npgSqlOptions` in `config.json`. PostgreSQL is required by this box's entry point;
custom logging is not an optional fallback.

**Each run clears the registered custom output tables.** Export results before starting another run.
See [output and analysis](output.md).

## Resources and execution

The checked-in configuration references these shared networks in `resources/`:

- `autobahn_und_bundesstrassen_deutschland_elevation_21.geojson`
- `neighbors_elevation.geojson`

Place or symlink the required files there, or adjust the layer input paths. Large network files may need to be
obtained separately. The remaining truck, driver and facility CSVs are described in [configuration](configuration.md)
and [facilities](facilities.md).

```bash
dotnet run
```

`Program.cs` loads `.env` automatically. To use another scenario:

```bash
MARS_CONFIG_PATH=path/to/scenario.json dotnet run
```

Paths in the scenario are relative to the working directory. Successful execution ends with
`Executed iterations <N> lasted <duration>` and flushes custom database output.

Use `dotnet run` directly: `simulate.sh` is a legacy export helper with a hard-coded older framework and
additional table-changing operations, not the current setup path.
