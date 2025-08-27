#!/usr/bin/env python3
# -*- coding: utf-8 -*-
r"""
Evaluate multimodal trips from a CSV  
"""

from pathlib import Path
from typing import Any, Dict, List, Optional
import json
import re

import numpy as np
import pandas as pd
import matplotlib.pyplot as plt


# ========= USER SETTINGS (EDIT IF NEEDED) =====================================
INPUT_PATH = Path(r"PATH")  #Please put the PaTH HERE
OUTDIR     = INPUT_PATH.parent / "eval_outputs"
MODALITIES = ["Walking", "Bus", "CyclingRentalBike", "Train"]  # enforced order
TOPN_SEQUENCES = 20
# ==============================================================================


# ---------- Utilities ----------

def ensure_dir(p: Path) -> None:
    p.mkdir(parents=True, exist_ok=True)


def _norm_name(s: str) -> str:
    """Normalize column name for matching (lowercase, strip non-alnum)."""
    return re.sub(r"[^a-z0-9]+", "", s.lower())


def map_columns(df: pd.DataFrame) -> Dict[str, str]:
    """
    Flexible header mapping. Returns canonical -> actual name for:
      creation_id, agent_type, stableid, activecapability,
      routemainmodality, routemodalities, geometry (optional)
    """
    actual = {_norm_name(c): c for c in df.columns}
    candidates = {
        "creation_id":        ["creationid", "creation_id", "cid", "id"],
        "agent_type":         ["agenttype", "agent_type", "type"],
        "stableid":           ["stableid", "stable_id", "sid"],
        "activecapability":   ["activecapability", "active_capability", "capability", "cap"],
        "routemainmodality":  ["routemainmodality", "route_main_modality", "routemainmode", "mainmodality", "mainmode"],
        "routemodalities":    ["routemodalities", "route_modalities", "routesequence", "routemodes", "modalities", "modes", "sequence"],
        "geometry":           ["geometry", "geom", "wkt", "the_geom"],
    }
    mapping: Dict[str, str] = {}
    for canonical, names in candidates.items():
        for n in names:
            key = _norm_name(n)
            if key in actual:
                mapping[canonical] = actual[key]
                break
    return mapping


def parse_sequence(cell: Any) -> List[str]:
    """Parse a modalities sequence from a cell into a list of strings."""
    if cell is None:
        return []
    if isinstance(cell, float) and np.isnan(cell):
        return []
    s = str(cell).strip()
    if not s:
        return []

    # JSON-like array (accept single quotes too)
    if s.startswith('[') and s.endswith(']'):
        try:
            data = json.loads(s.replace("'", '"'))
            return [str(x).strip() for x in data if str(x).strip()]
        except Exception:
            pass

    # Try common delimiters first
    parts = re.split(r"[;,>\|\-\,]+", s.strip("[]"))
    parts = [p.strip() for p in parts if p.strip()]
    # If still a single token with spaces, split on whitespace
    if len(parts) <= 1 and " " in s:
        parts = [p for p in s.split() if p.strip()]

    # Clean quotes
    parts = [p.strip().strip("'").strip('"') for p in parts if p.strip()]
    return parts


def save_bar(series: pd.Series, title: str, ylabel: str, out_png: Path) -> None:
    if series is None:
        return
    # Accept Series or single-column DataFrame
    if isinstance(series, pd.DataFrame) and series.shape[1] == 1:
        series = series.iloc[:, 0]
    series = series.dropna()
    if series.shape[0] == 0:
        return
    plt.figure()
    series.plot(kind="bar")
    plt.title(title)
    plt.ylabel(ylabel)
    plt.tight_layout()
    plt.savefig(out_png, dpi=150)
    plt.close()


# ---------- Main pipeline ----------

def run_evaluation() -> None:
    ensure_dir(OUTDIR)

    # Load CSV
    df = pd.read_csv(INPUT_PATH, low_memory=False)

    # Map flexible column names to canonical ones, then soft-rename
    colmap = map_columns(df)
    df = df.rename(columns={colmap[k]: k for k in colmap.keys()})

    # Parse sequences (for fallback and top sequences)
    if "routemodalities" in df.columns:
        df["seq_list"] = df["routemodalities"].apply(parse_sequence)
    else:
        df["seq_list"] = [[] for _ in range(len(df))]

    # Main-mode share
    if "routemainmodality" in df.columns:
        df["routemainmodality"] = pd.Categorical(df["routemainmodality"],
                                                 categories=MODALITIES, ordered=True)
        main_counts = df["routemainmodality"].value_counts().reindex(MODALITIES).fillna(0).astype(int)
        main_share = (df["routemainmodality"].value_counts(normalize=True) * 100).reindex(MODALITIES)
        used_fallback = False
    else:
        # Fallback: majority in seq_list (simple heuristic)
        def majority(s: List[str]) -> Optional[str]:
            if not s:
                return np.nan
            vc = pd.Series(s).value_counts()
            return vc.index[0]
        df["main_mode_fallback"] = pd.Categorical(
            df["seq_list"].apply(majority),
            categories=MODALITIES, ordered=True
        )
        main_counts = df["main_mode_fallback"].value_counts().reindex(MODALITIES).fillna(0).astype(int)
        main_share = (df["main_mode_fallback"].value_counts(normalize=True) * 100).reindex(MODALITIES)
        used_fallback = True

    # Top sequences (ignore empties)
    seq_strings = df["seq_list"].apply(lambda s: ">".join(s))
    top_sequences = seq_strings[seq_strings != ""].value_counts().head(TOPN_SEQUENCES)

    # Save tables
    main_counts.to_csv(OUTDIR / "main_mode_counts.csv", header=["count"])
    main_share.to_csv(OUTDIR / "main_mode_share_pct.csv", header=["pct"])
    top_sequences.to_csv(OUTDIR / "top_sequences.csv", header=["count"])

    # Augmented dataset (only keep seq_list and, if used, main_mode_fallback)
    df_out = df.copy()
    keep_cols = [c for c in df_out.columns if c not in {"creation_id","agent_type","stableid","activecapability",
                                                        "routemainmodality","routemodalities","seq_list",
                                                        "main_mode_fallback"}]  # start with all, then ensure seq/fallback appended
    base_cols = [c for c in df.columns if c not in {"seq_list", "main_mode_fallback"}]
    export_cols = base_cols + (["seq_list"] if "seq_list" in df.columns else []) + (["main_mode_fallback"] if used_fallback else [])
    df_out[export_cols].to_csv(OUTDIR / "augmented_trips.csv", index=False)

    # Chart: main-mode share
    save_bar(main_share, "Main mode share", "% of trips", OUTDIR / "main_mode_share_pct.png")

    # Summary text
    with open(OUTDIR / "summary.txt", "w", encoding="utf-8") as f:
        f.write(f"Input: {INPUT_PATH}\n")
        f.write(f"Trips: {len(df)}\n")
        f.write("Modalities (ordered): " + ", ".join(MODALITIES) + "\n")
        f.write("\nMain mode share (%):\n")
        f.write(main_share.round(2).to_string() + "\n")
        if used_fallback:
            f.write("\nNote: `routemainmodality` missing; used majority of `routemodalities` per trip.\n")
        if not top_sequences.empty:
            f.write("\nTop sequences:\n")
            f.write(top_sequences.to_string() + "\n")

    print(f"Done. Outputs written to: {OUTDIR}")


if __name__ == "__main__":
    run_evaluation()
