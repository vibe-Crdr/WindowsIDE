---
name: mcp-windows
description: >-
  Uses CursorTouch Windows-MCP for live Windows desktop automation: Screenshot,
  Snapshot, DisplayInventory, Click, Type, Scroll, Move, Shortcut, Wait,
  WaitFor, MultiSelect, MultiEdit, App, PowerShell, FileSystem, Registry,
  Process, Clipboard, Notification, Scrape. Use when the user mentions Windows
  MCP, windows-mcp, desktop automation, UI click/type, screenshot the screen,
  Snapshot, Registry, toast notification, or driving Windows apps. Distinct from
  windows-ide-filesystem, Excel MCP, GitHub MCP, and the WindowsIDE product.
---

# mcp-windows

Live Windows OS automation via MCP (`windows-mcp`, CursorTouch). Not a repo sandbox, not Excel COM, not the WindowsIDE editor.

Call `GetMcpTools` for the target tool, then `CallMcpTool`. Discover the server by tools named `Screenshot`, `Snapshot`, `Click`, or `DisplayInventory` (IDs often contain `windows-mcp`). If the server is missing, say so and do not invent desktop state.

Argument names follow the **live schema** from `GetMcpTools`. Upstream READMEs drift — trust the live catalog. This install enables **all 20 tools** (no `--tools` / `--exclude-tools` whitelist).

## Do not confuse layers

| Need | Use |
|------|------|
| Click / type / screenshot / launch apps on the live desktop | This skill (Windows-MCP) |
| Workspace source (`.cs`, `.md`, XML) | Built-in Read / Write / StrReplace |
| Sandboxed file trees under the repo | `windows-ide-filesystem` |
| Workbook cells under the repo | `mcp-excel` |
| GitHub issues / PRs | `mcp-github` |
| Decisions across chats | `mcp-memory-service` |
| In-chat Cursor browser tab | `cursor-ide-browser` (not Snapshot `use_dom` unless the user wants a real Chrome/Edge/Firefox window) |

Do **not** use `Type` to write code in WindowsIDE or Cursor. Do **not** use `FileSystem` to edit this repo when built-in file tools work.

## Decision matrix

| Intent | Tool |
|--------|------|
| Fast visual context (default first look) | `Screenshot` |
| Interactive element ids, scroll regions, browser DOM | `Snapshot` |
| Monitor bounds / DPI / scale | `DisplayInventory` |
| Click / hover / double-click | `Click` (`loc` or `label`) |
| Type into a field | `Type` |
| Scroll | `Scroll` |
| Move pointer or drag | `Move` (`drag=true`, optional `from_loc`) |
| Hotkey (`ctrl+c`, `alt+tab`, `win+r`) | `Shortcut` |
| Sleep N seconds | `Wait` |
| Poll until text/window/element | `WaitFor` |
| Multi-item select | `MultiSelect` |
| Fill many fields | `MultiEdit` |
| Launch / resize / switch windows | `App` |
| Run a PowerShell command | `PowerShell` |
| Read/write/copy/move/delete/list/search files | `FileSystem` |
| Registry get/set/delete/list | `Registry` |
| List or kill processes | `Process` |
| Clipboard get/set | `Clipboard` |
| Toast | `Notification` |
| Fetch a URL or active-tab DOM | `Scrape` |

Full catalog: [tools.md](tools.md). QA of one tool: skill `windows-mcp-tool-tester`.

## Workflow

1. Confirm Windows-MCP is in the live catalog. If not: say it is disconnected (reload MCP after `mcp.json` changes). Do not guess pixels or window titles.
2. **Look first.** `Screenshot` for visual context. `Snapshot` when you need `label` ids. `DisplayInventory` before multi-monitor `display=` / `region=`.
3. Act with `Click` / `Type` / `Shortcut` / `App`. After UI changes, Snapshot again — labels go stale.
4. `WaitFor` instead of a loop of Snapshot+Wait when waiting for text, a window, or an element.
5. Mutating OS tools (`FileSystem` delete, `Registry` set/delete, `Process` kill, destructive `PowerShell`) only when the user asked. Verify with a **different** tool (usually `PowerShell`).
6. Japanese IME: switch to English before `Type` / `Shortcut` character tests, then restore.

### Coordinates and labels

- `label` needs a prior `Snapshot` (not Screenshot). Screenshot skips the UI tree.
- `loc` is `[x, y]` in virtual-desktop pixels. If Screenshot downscales, scale image coords back to original size before clicking.
- `region=[left, top, right, bottom]` beats `display=` when both are set.

### Safety

Windows-MCP has **no sandbox**. `PowerShell`, `FileSystem`, `Registry`, `Process`, and `App` can be irreversible. Prefer read-only tools until the user asks to change the machine. `Scrape` blocks private/loopback URLs (SSRF guard).

## This machine

Windows 11. Package: PyPI `windows-mcp` via `uvx` (Python 3.14). Upstream: [CursorTouch/Windows-MCP](https://github.com/CursorTouch/Windows-MCP).

**WindowsIDE** (this workspace):

- Server id typically `windows-mcp`
- Config: `.cursor/mcp.json` → `scripts/run_windows_mcp.py`
- Allowlist: `windows-mcp:*`
- All 20 tools enabled. Telemetry off (`ANONYMIZED_TELEMETRY=false`)

If the server is not connected: do not claim desktop ops succeeded.

## Gotchas

- **Live schema over README.** Tool PascalCase names (`PowerShell`, not `Shell`).
- **Screenshot vs Snapshot.** Screenshot is the fast default. Snapshot is required for `label`, scrollables, and `use_dom=True`.
- **FileSystem relative paths** resolve from the user **Desktop** folder, not the repo root.
- **App `launch`** uses Start Menu names (English preferred). `launch_executable` needs a real file path plus `args` / `cwd`.
- **Type** is for UI fields, not IDE programming. Modern Notepad's editor may have no Snapshot label — use `loc`.
- **Notification** requires `app_id` (AUMID), not only title/message.
- **Games / custom-drawn UIs** often have no useful UIA tree.
- First launch of `uvx windows-mcp` can take a minute while dependencies install.
