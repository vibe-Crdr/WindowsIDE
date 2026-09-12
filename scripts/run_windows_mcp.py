"""
Summary:
    Launch CursorTouch Windows-MCP with every tool enabled.

    Uses uvx and managed Python 3.14. cwd is ASCII because this repo path
    contains デスクトップ. Does not pass --tools or --exclude-tools.

Arguments:
    None. Extra CLI arguments are forwarded to `windows-mcp serve`.

Returns:
    Exits with the windows-mcp process exit code.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

PACKAGE_SPEC = "windows-mcp"


def _resolve_uvx() -> Path:
    """
    Summary:
        Locate the uvx executable for launching Windows-MCP.

    Args:
        None.

    Returns:
        Absolute path to uvx.exe.
    """
    which = shutil.which("uvx")
    if which:
        return Path(which).resolve()

    candidates = [
        Path(os.environ["USERPROFILE"]) / ".local" / "bin" / "uvx.exe",
        Path(os.environ.get("LOCALAPPDATA", "")) / "Programs" / "uv" / "uvx.exe",
    ]
    win_get_root = (
        Path(os.environ.get("LOCALAPPDATA", ""))
        / "Microsoft"
        / "WinGet"
        / "Packages"
    )
    if win_get_root.is_dir():
        candidates.extend(sorted(win_get_root.glob("astral-sh.uv*/uvx.exe")))

    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()

    raise SystemExit(
        "uvx was not found. Install uv (winget install astral-sh.uv) and retry."
    )


def _prepare_env() -> dict[str, str]:
    """
    Summary:
        Build the process environment with all Windows-MCP tools enabled.

    Args:
        None.

    Returns:
        Environment mapping for the child windows-mcp process.
    """
    env = os.environ.copy()
    env.setdefault("UV_PYTHON", "3.14")
    env.setdefault("UV_PYTHON_PREFERENCE", "only-managed")
    env.setdefault("ANONYMIZED_TELEMETRY", "false")
    env.pop("WINDOWS_MCP_TOOLS", None)
    env.pop("WINDOWS_MCP_EXCLUDE_TOOLS", None)
    return env


def main() -> None:
    """
    Summary:
        Start windows-mcp stdio MCP via uvx from an ASCII-safe cwd.

    Args:
        None.

    Returns:
        Nothing. Exits with the child process status code.
    """
    uvx = _resolve_uvx()
    env = _prepare_env()
    ascii_cwd = str(Path(os.environ["USERPROFILE"]).resolve())
    command = [
        str(uvx),
        "--from",
        PACKAGE_SPEC,
        "windows-mcp",
        "serve",
        *sys.argv[1:],
    ]
    completed = subprocess.run(
        command,
        cwd=ascii_cwd,
        env=env,
        check=False,
    )
    raise SystemExit(completed.returncode)


if __name__ == "__main__":
    main()
