"""
Summary:
    Launch the Excel MCP server scoped to the WindowsIDE project root.
    Workbook paths are resolved relative to the project root.

Arguments:
    None. The allowed directory is derived from this script's location.

Returns:
    Nothing. Starts the MCP stdio server as a side effect.
"""

from __future__ import annotations

from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    """
    Summary:
        Start excel-mcp-server with paths restricted to the project root.

    Args:
        None.

    Returns:
        Nothing. Blocks until the MCP client closes the stdio session.
    """
    from excel_mcp import server

    server.EXCEL_FILES_PATH = str(PROJECT_ROOT.resolve())
    server.run_stdio()


if __name__ == "__main__":
    main()
