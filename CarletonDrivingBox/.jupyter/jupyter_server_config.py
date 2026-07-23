"""Auto-repair incomplete notebook outputs on save (stream missing 'name', etc.).

Launch Jupyter from this project with:
  set JUPYTER_CONFIG_DIR=%CD%\\.jupyter
  jupyter lab

Or on WSL/Linux:
  JUPYTER_CONFIG_DIR="$(pwd)/.jupyter" jupyter lab
"""

from __future__ import annotations


def _fix_output(out: dict) -> None:
    ot = out.get("output_type")
    if ot == "stream":
        if "name" not in out:
            text = out.get("text", "")
            joined = "".join(text).lower() if isinstance(text, list) else str(text).lower()
            out["name"] = "stderr" if ("traceback" in joined or "error" in joined) else "stdout"
        out.setdefault("text", "")
    elif ot in ("display_data", "execute_result"):
        out.setdefault("metadata", {})
        out.setdefault("data", {"text/plain": ""})
        if ot == "execute_result":
            out.setdefault("execution_count", None)
    elif ot == "error":
        out.setdefault("ename", "Error")
        out.setdefault("evalue", "")
        out.setdefault("traceback", [])


def scrub_notebook_outputs(model, **kwargs):
    """ContentsManager pre_save_hook: keep notebooks nbformat-valid."""
    if model.get("type") != "notebook":
        return
    nb = model.get("content")
    if not isinstance(nb, dict):
        return
    for cell in nb.get("cells", []):
        cell.setdefault("metadata", {})
        if cell.get("cell_type") == "code":
            cell.setdefault("execution_count", None)
            cell.setdefault("outputs", [])
        for out in cell.get("outputs", []):
            if isinstance(out, dict):
                _fix_output(out)


c = get_config()  # noqa: F821 — provided by Jupyter traitlets
c.FileContentsManager.pre_save_hook = scrub_notebook_outputs
