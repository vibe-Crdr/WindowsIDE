"""
Summary:
    Launch the official filesystem MCP server scoped to the WindowsIDE
    project root. Resolves allowed directories from this script's location so
    MCP config stays portable on paths that contain non-ASCII segments.

Arguments:
    None. The allowed directory is derived from this script's location.

Returns:
    Nothing. Starts the MCP stdio server as a side effect.
"""

from __future__ import annotations

import shutil
import subprocess
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
NPX_CANDIDATES = (
    Path(r"C:\Program Files\nodejs\npx.cmd"),
    Path(r"C:\Program Files (x86)\nodejs\npx.cmd"),
)


def _resolve_npx() -> Path:
    """
    Summary:
        Locate the npx executable on Windows or PATH.

    Args:
        None.

    Returns:
        Absolute path to npx.

    Raises:
        SystemExit: When npx cannot be found.
    """
    candidates: list[Path] = []
    npx_on_path = shutil.which("npx")
    if npx_on_path:
        candidates.append(Path(npx_on_path))
    candidates.extend(NPX_CANDIDATES)

    for candidate in candidates:
        resolved = candidate
        if sys.platform == "win32" and resolved.suffix.lower() not in {".cmd", ".exe"}:
            cmd_path = resolved.with_suffix(".cmd")
            if cmd_path.exists():
                resolved = cmd_path
        if resolved.exists():
            return resolved.resolve()

    raise SystemExit(
        "npx was not found. Install Node.js LTS and restart Cursor before using "
        "the filesystem MCP server."
    )


def main() -> None:
    """
    Summary:
        Start @modelcontextprotocol/server-filesystem for the project root only.

    Args:
        None.

    Returns:
        Nothing. Blocks until the MCP client closes the stdio session.
    """
    allowed_directory = str(PROJECT_ROOT.resolve())
    npx = _resolve_npx()
    command = [
        str(npx),
        "-y",
        "@modelcontextprotocol/server-filesystem",
        allowed_directory,
    ]
    return_code = subprocess.call(
        command,
        stdin=sys.stdin,
        stdout=sys.stdout,
        stderr=sys.stderr,
    )
    raise SystemExit(return_code)


if __name__ == "__main__":
    main()
