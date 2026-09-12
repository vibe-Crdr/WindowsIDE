"""
Summary:
    Launch doobidoo mcp-memory-service as the WindowsIDE decision-memory MCP.

    Uses uvx with managed Python 3.14 so sqlite_vec loads reliably on Windows.
    Storage stays under .cursor/memory/.

Arguments:
    None. Extra CLI arguments are forwarded to `memory server`.

Returns:
    Exits with the mcp-memory-service process exit code.
"""

from __future__ import annotations

import os
import shutil
import subprocess
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]
MEMORY_DIR = PROJECT_ROOT / ".cursor" / "memory"
# [sqlite] pulls onnxruntime + tokenizers without heavy torch/[ml] extras.
PACKAGE_SPEC = "mcp-memory-service[sqlite]==11.8.0"


def _resolve_uvx() -> Path:
    """
    Summary:
        Locate the uvx executable for launching mcp-memory-service.

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
        Build the process environment for local sqlite_vec memory storage.

    Args:
        None.

    Returns:
        Environment mapping for the child memory server process.
    """
    MEMORY_DIR.mkdir(parents=True, exist_ok=True)
    (MEMORY_DIR / "backups").mkdir(parents=True, exist_ok=True)

    env = os.environ.copy()
    env.setdefault("UV_PYTHON", "3.14")
    env.setdefault("UV_PYTHON_PREFERENCE", "only-managed")
    env.setdefault("MCP_MEMORY_STORAGE_BACKEND", "sqlite_vec")
    env.setdefault(
        "MCP_MEMORY_SQLITE_PATH",
        str((MEMORY_DIR / "sqlite_vec.db").resolve()),
    )
    env.setdefault(
        "MCP_MEMORY_BACKUPS_PATH",
        str((MEMORY_DIR / "backups").resolve()),
    )
    env.setdefault(
        "MCP_MEMORY_SQLITE_PRAGMAS",
        "journal_mode=WAL,busy_timeout=15000,cache_size=20000",
    )
    env.setdefault("MCP_ENTITY_LINKING_ENABLED", "1")
    env.setdefault("MCP_MEMORY_USE_ONNX", "1")
    env.setdefault("MCP_CUSTOM_MEMORY_TYPES", '{"ops": []}')
    return env


def main() -> None:
    """
    Summary:
        Start mcp-memory-service stdio MCP via uvx from an ASCII-safe cwd.

    Args:
        None.

    Returns:
        Nothing. Exits with the child process status code.
    """
    uvx = _resolve_uvx()
    env = _prepare_env()
    # Avoid non-ASCII cwd issues similar to codebase-memory-mcp on デスクトップ.
    ascii_cwd = str(Path(os.environ["USERPROFILE"]).resolve())
    command = [
        str(uvx),
        "--from",
        PACKAGE_SPEC,
        "memory",
        "server",
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
