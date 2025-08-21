#!/usr/bin/env python3
"""
validate_trips_are_walking.py

Validate that trips GeoJSON are truly WALKING:
- trips lie inside the AOI,
- trips overlap the Walking network (>= threshold),
- walking speeds are plausible.

Defaults are set for Casablanca · Sidi Maârouf case.
"""

import argparse
import json
from pathlib import Path
from typing import Optional, Tuple

import geopandas as gpd
import pandas as pd
from shapely.ops import unary_union
from shapely.geometry import LineString, MultiLineString, base as shapely_base


def parse_args():
    p = argparse.ArgumentParser(
        description="Validate that SOH trips are walking (AOI overlap, on-walk overlap, speed)."
    )
    p.add_argument("--trips", default="resources/HumanTraveler_trips.geojson", help="Trips GeoJSON path.")
    p.add_argument(
        "--walk",
        default="resources/casa_sidi_maarouf_walk_graph.geojson",
        help="Walking graph (edges) GeoJSON path.",
    )
    p.add_argument(
        "--aoi",
        default="resources/casa_sidi_maarouf_aoi.geojson",
        help="AOI polygon GeoJSON path (Feature or FeatureCollection).",
    )
    p.add_argument(
        "--drive",
        default=None,
        help="(Optional) Driving graph (edges) GeoJSON path; used to assert near-zero overlap.",
    )
    p.add_argument("--buffer-m", type=float, default=3.0, help="Buffer (m) around networks for overlap checks.")
    p.add_argument("--inside-thresh", type=float, default=0.98, help="Min fraction of trip inside AOI.")
    p.add_argument("--on-walk-thresh", type=float, default=0.98, help="Min fraction of trip on walk network.")
    p.add_argument("--on-drive-max", type=float, default=0.05, help="Max fraction of trip on drive network (if provided).")
    p.add_argument("--min-speed", type=float, default=0.2, help="Min walking speed (m/s).")
    p.add_argument("--max-speed", type=float, default=2.5, help="Max walking speed (m/s).")
    p.add_argument("--report-json", default="output/walk_validation_report.json", help="Where to write a JSON report.")
    p.add_argument("--issues-csv", default="output/walk_validation_issues.csv", help="Where to write a CSV of failures.")
    return p.parse_args()


def ensure_dir(p: Path):
    p.parent.mkdir(parents=True, exist_ok=True)


def choose_metric_crs(*gdfs: gpd.GeoDataFrame) -> str:
    """Pick a sensible local metric CRS using the first non-empty GDF's estimate_utm_crs."""
    for g in gdfs:
        if g is not None and not g.empty:
            try:
                return g.estimate_utm_crs()  # type: ignore[return-value]
            except Exception:
                continue
    return "EPSG:3857"  # fallback


def to_lines(geom: shapely_base.BaseGeometry) -> LineString | MultiLineString:
    if isinstance(geom, (LineString, MultiLineString)):
        return geom
    # Trips should be (Multi)LineString; if not, buffer tiny & skeletonize via boundary
    return geom.boundary  # best-effort fallback


def parse_duration_seconds(val) -> Optional[float]:
    """Accept numeric seconds or 'HH:MM:SS' strings."""
    if val is None or (isinstance(val, float) and pd.isna(val)):
        return None
    if isinstance(val, (int, float)):
        return float(val)
    if isinstance(val, str):
        s = val.strip()
        if ":" in s:
            try:
                h, m, sec = s.split(":")
                return int(h) * 3600 + int(m) * 60 + float(sec)
            except Exception:
                return None
        try:
            return float(s)
        except Exception:
            return None
    return None


def frac_length(a: shapely_base.BaseGeometry, b: shapely_base.BaseGeometry) -> float:
    if a.is_empty:
        return 0.0
    inter = a.intersection(b)
    # For robustness, handle Multi* and mixed outputs by summing lengths
    try:
        return inter.length / a.length if a.length > 0 else 0.0
    except Exception:
        return 0.0


def main():
    args = parse_args()

    trips_path = Path(args.trips)
    walk_path = Path(args.walk)
    aoi_path = Path(args.aoi)
    drive_path = Path(args.drive) if args.drive else None

    if not trips_path.exists():
        raise FileNotFoundError(trips_path)
    if not walk_path.exists():
        raise FileNotFoundError(walk_path)
    if not aoi_path.exists():
        raise FileNotFoundError(aoi_path)

    trips = gpd.read_file(trips_path)
    walk = gpd.read_file(walk_path)
    aoi = gpd.read_file(aoi_path)

    drive = None
    if drive_path:
        if not drive_path.exists():
            raise FileNotFoundError(drive_path)
        drive = gpd.read_file(drive_path)

    # Pick a metric CRS (UTM) for accurate lengths
    metric_crs = choose_metric_crs(aoi, trips, walk)
    trips_m = trips.to_crs(metric_crs)
    walk_m = walk.to_crs(metric_crs)
    aoi_m = aoi.to_crs(metric_crs)
    drive_m = drive.to_crs(metric_crs) if drive is not None else None

    # Consolidate networks & buffer slightly to account for digitizing gaps
    walk_union = unary_union(walk_m.geometry)
    walk_buf = gpd.GeoSeries([walk_union], crs=metric_crs).buffer(args.buffer_m).iloc[0]

    drive_buf = None
    if drive_m is not None and not drive_m.empty:
        drive_union = unary_union(drive_m.geometry)
        drive_buf = gpd.GeoSeries([drive_union], crs=metric_crs).buffer(args.buffer_m).iloc[0]

    aoi_union = unary_union(aoi_m.geometry)

    # Iterate trips
    results = []
    for idx, row in trips_m.iterrows():
        geom = to_lines(row.geometry)
        if geom.is_empty or geom.length == 0:
            # Skip degenerate geometry
            results.append(
                dict(
                    index=idx,
                    stable_id=row.get("StableId", idx),
                    ok=False,
                    reason="empty_geometry",
                    frac_inside=0.0,
                    frac_on_walk=0.0,
                    frac_on_drive=0.0,
                    speed_m_s=None,
                    length_m=0.0,
                    duration_s=None,
                )
            )
            continue

        total_len = geom.length
        frac_inside = frac_length(geom, aoi_union)
        frac_on_walk = frac_length(geom, walk_buf)

        frac_on_drive = None
        if drive_buf is not None:
            frac_on_drive = frac_length(geom, drive_buf)
        else:
            frac_on_drive = 0.0  # no drive data provided → treat as 0

        # Speed: prefer Duration field if present; fallback to geometry length + unknown duration
        duration_s = parse_duration_seconds(row.get("Duration", None))
        # DistanceTraveled may be present (already measured by engine); else use geometry length
        distance_m = row.get("DistanceTraveled", None)
        try:
            distance_m = float(distance_m)
        except Exception:
            distance_m = total_len

        speed_m_s = (distance_m / duration_s) if (duration_s and duration_s > 0) else None

        bad_reasons = []
        if frac_inside < args.inside_thresh:
            bad_reasons.append(f"inside_AOI={frac_inside:.2%} < {args.inside_thresh:.0%}")
        if frac_on_walk < args.on_walk_thresh:
            bad_reasons.append(f"on_walk={frac_on_walk:.2%} < {args.on_walk_thresh:.0%}")
        if drive_buf is not None and frac_on_drive > args.on_drive_max:
            bad_reasons.append(f"on_drive={frac_on_drive:.2%} > {args.on_drive_max:.0%}")
        if speed_m_s is not None and not (args.min_speed <= speed_m_s <= args.max_speed):
            bad_reasons.append(f"speed={speed_m_s:.2f}m/s not in [{args.min_speed},{args.max_speed}]")

        ok = len(bad_reasons) == 0
        results.append(
            dict(
                index=idx,
                stable_id=row.get("StableId", idx),
                ok=ok,
                reason="; ".join(bad_reasons) if bad_reasons else "",
                frac_inside=round(frac_inside, 6),
                frac_on_walk=round(frac_on_walk, 6),
                frac_on_drive=round(frac_on_drive or 0.0, 6),
                speed_m_s=round(speed_m_s, 3) if speed_m_s is not None else None,
                length_m=round(total_len, 3),
                duration_s=round(duration_s, 3) if duration_s is not None else None,
            )
        )

    df = pd.DataFrame(results)
    total = len(df)
    passes = int(df["ok"].sum())
    fails = total - passes

    # Summary
    summary = {
        "files": {
            "trips": str(trips_path),
            "walk": str(walk_path),
            "drive": str(drive_path) if drive_path else None,
            "aoi": str(aoi_path),
        },
        "metric_crs": str(metric_crs),
        "thresholds": {
            "inside_thresh": args.inside_thresh,
            "on_walk_thresh": args.on_walk_thresh,
            "on_drive_max": args.on_drive_max,
            "speed_range_mps": [args.min_speed, args.max_speed],
            "buffer_m": args.buffer_m,
        },
        "counts": {"total_trips": total, "passes": passes, "fails": fails},
        "fail_reasons_top": (
            df.loc[~df.ok, "reason"].value_counts().to_dict() if fails > 0 else {}
        ),
        "stats": {
            "mean_speed_m_s": float(df["speed_m_s"].dropna().mean()) if df["speed_m_s"].notna().any() else None,
            "median_speed_m_s": float(df["speed_m_s"].dropna().median()) if df["speed_m_s"].notna().any() else None,
            "mean_frac_on_walk": float(df["frac_on_walk"].mean()) if total else None,
            "mean_frac_inside": float(df["frac_inside"].mean()) if total else None,
            "mean_frac_on_drive": float(df["frac_on_drive"].mean()) if total else None,
        },
    }

    # Write report + issues CSV
    ensure_dir(Path(args.report_json))
    ensure_dir(Path(args.issues_csv))
    with open(args.report_json, "w", encoding="utf-8") as f:
        json.dump(summary, f, indent=2)

    if fails > 0:
        df[~df.ok].to_csv(args.issues_csv, index=False)
    else:
        # Write an empty file with header for consistency
        df[~df.ok].to_csv(args.issues_csv, index=False)

    # Console output
    print(json.dumps(summary, indent=2))
    if fails > 0:
        print(f"\n❌ {fails}/{total} trips failed. See: {args.issues_csv}")
    else:
        print(f"\n✅ All {total} trips look like walking within thresholds.")


if __name__ == "__main__":
    main()
