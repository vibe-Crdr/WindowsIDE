"""
Summary:
    Launch codebase-memory-mcp for Cursor with an ASCII session cwd.

    CBM 0.10 derives session_root from process cwd and then canonicalizes it.
    A CJK path such as OneDrive/デスクトップ is rejected ("daemon session
    context was rejected"). A junction to that repo is also rejected because
    CBM follows it. Starting from %USERPROFILE% is accepted but autoindex
    then skips home as unsafe. This wrapper uses the real repo as cwd when
    the path is ASCII; otherwise it uses a normal ASCII directory under
    LOCALAPPDATA and points CBM_ALLOWED_ROOT at that same directory.

Arguments:
    None. Extra CLI arguments are forwarded to codebase-memory-mcp.exe.

Returns:
    Exits with the MCP server process exit code.
"""

from __future__ import annotations

import os
import subprocess
import sys
from pathlib import Path

# Windows FILE_ATTRIBUTE_REPARSE_POINT. Used to detect leftover junctions.
_FILE_ATTRIBUTE_REPARSE_POINT = 0x400


def _resolve_exe() -> Path:
    """
    Summary:
        Locate the installed codebase-memory-mcp executable.

    Args:
        None.

    Returns:
        Absolute path to codebase-memory-mcp.exe.
    """
    candidates = [
        Path(os.environ["USERPROFILE"]) / ".local/bin/codebase-memory-mcp.exe",
        Path(os.environ.get("LOCALAPPDATA", ""))
        / "Programs/codebase-memory-mcp/codebase-memory-mcp.exe",
    ]
    for candidate in candidates:
        if candidate.is_file():
            return candidate.resolve()
    raise SystemExit(
        "codebase-memory-mcp.exe was not found. Re-install with install.ps1."
    )


def _resolve_project_root() -> Path:
    """
    Summary:
        Determine the WindowsIDE repository root.

    Args:
        None.

    Returns:
        Absolute project path.
    """
    explicit = os.environ.get("CBM_ALLOWED_ROOT", "").strip()
    if explicit:
        return Path(explicit).resolve()
    return Path(__file__).resolve().parents[1]


def _path_is_ascii(path: Path) -> bool:
    """
    Summary:
        Return whether the path string is ASCII-only.

    Args:
        path: Filesystem path to inspect.

    Returns:
        True when str(path) encodes as ASCII.
    """
    try:
        str(path).encode("ascii")
    except UnicodeEncodeError:
        return False
    return True


def _is_home_directory(path: Path) -> bool:
    """
    Summary:
        Return whether path is the current Windows user profile.

    Args:
        path: Candidate directory.

    Returns:
        True when path resolves to %USERPROFILE%.
    """
    profile = os.environ.get("USERPROFILE", "").strip()
    if not profile:
        return False
    try:
        return path.resolve() == Path(profile).resolve()
    except OSError:
        return False


def _is_reparse_point(path: Path) -> bool:
    """
    Summary:
        Detect a Windows junction or symlink without following it.

    Args:
        path: Path that may be a reparse point.

    Returns:
        True when the path itself is a reparse point.
    """
    try:
        attributes = path.lstat().st_file_attributes  # type: ignore[attr-defined]
    except (AttributeError, OSError):
        return False
    return bool(attributes & _FILE_ATTRIBUTE_REPARSE_POINT)


def _ascii_session_dir() -> Path:
    """
    Summary:
        Return the ASCII directory used as CBM process cwd on CJK hosts.

    Args:
        None.

    Returns:
        Path under %LOCALAPPDATA%/WindowsIDE/cbm-session.
    """
    local_app_data = os.environ.get("LOCALAPPDATA", "").strip()
    if not local_app_data:
        raise SystemExit(
            "LOCALAPPDATA is not set; cannot create an ASCII CBM session cwd."
        )
    return Path(local_app_data) / "WindowsIDE" / "cbm-session"


def _ensure_ascii_session_dir() -> Path:
    """
    Summary:
        Create a normal ASCII directory for CBM cwd. Replace a leftover
        junction, because CBM 0.10 follows junctions to the CJK repo and
        then rejects the session.

    Args:
        None.

    Returns:
        Existing ASCII session directory that is not a reparse point.
    """
    session_dir = _ascii_session_dir()
    session_dir.parent.mkdir(parents=True, exist_ok=True)
    try:
        session_dir.lstat()
        present = True
    except OSError:
        present = False
    if present and _is_reparse_point(session_dir):
        try:
            session_dir.rmdir()
        except OSError as exc:
            raise SystemExit(
                f"Failed to replace leftover CBM session junction {session_dir}: {exc}"
            ) from exc
        present = False
    if not present:
        session_dir.mkdir(parents=True, exist_ok=True)
    marker = session_dir / "README.txt"
    if not marker.exists():
        marker.write_text(
            "ASCII session root for codebase-memory-mcp on a CJK repository path.\n",
            encoding="ascii",
        )
    return session_dir


def _resolve_session_cwd(project_root: Path) -> Path:
    """
    Summary:
        Choose a CBM process cwd that CBM 0.10 will accept.

    Args:
        project_root: Repository root from CBM_ALLOWED_ROOT or this script.

    Returns:
        Directory to pass as subprocess cwd.
    """
    resolved = project_root.resolve()
    if _is_home_directory(resolved):
        raise SystemExit(
            "CBM session cwd cannot be the user profile; set CBM_ALLOWED_ROOT "
            "to the WindowsIDE repository."
        )
    if _path_is_ascii(resolved):
        return resolved
    return _ensure_ascii_session_dir()


def main() -> None:
    """
    Summary:
        Start CBM MCP from an ASCII cwd that is not the user profile.

    Args:
        None.

    Returns:
        Nothing. Exits with the child process status code.
    """
    exe = _resolve_exe()
    env = os.environ.copy()
    project_root = _resolve_project_root()
    session_cwd = _resolve_session_cwd(project_root)
    # CBM 0.10 rejects a CJK session_root. The child allowed-root must contain
    # the ASCII cwd. The real repo stays reachable via CBM allow-root grants.
    env["CBM_ALLOWED_ROOT"] = str(session_cwd)

    completed = subprocess.run(
        [str(exe), *sys.argv[1:]],
        cwd=str(session_cwd),
        env=env,
        check=False,
    )
    raise SystemExit(completed.returncode)


if __name__ == "__main__":
    main()
