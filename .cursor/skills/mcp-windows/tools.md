# mcp-windows tools

Always `GetMcpTools` then `CallMcpTool`. Names below match CursorTouch **Windows-MCP** as launched by `scripts/run_windows_mcp.py` (all 20 tools). Confirm the live schema; versions differ.

## Discovery

Find a server whose tools include `Screenshot` and `Snapshot`. Common id: `windows-mcp`.

`label` / `labels` need a prior `Snapshot` so `desktop_state` exists. `Screenshot` does not fill that tree.

## Capture

### `Screenshot`

Fast screenshot-first capture (skips UI tree). Default first look.

| Arg | Notes |
|-----|--------|
| `use_annotation` | Default false |
| `width_reference_line` / `height_reference_line` | Optional grid |
| `display` | Zero-based monitor indices, e.g. `[0]` or `[0,1]` |
| `region` | `[left, top, right, bottom]` virtual-desktop pixels; wins over `display` |

If the image is downscaled, map click coords back to original size.

### `Snapshot`

Full desktop state: windows, interactive ids, scrollables. Use before `label`.

| Arg | Notes |
|-----|--------|
| `use_vision` | Include screenshot (default false) |
| `use_dom` | Browser page DOM (Chrome / Edge / Firefox) |
| `use_annotation` | Default true (boxes on elements) |
| `use_ui_tree` | Default true; false ≈ screenshot-only |
| `width_reference_line` / `height_reference_line` | Needs vision |
| `display` / `region` | Same rules as Screenshot |

### `DisplayInventory`

No arguments. Monitor index, device, bounds, work area, resolution, orientation, effective DPI, scale.

### `Scrape`

| Arg | Notes |
|-----|--------|
| `url` | Required. Private/loopback/credential URLs are blocked |
| `query` | Optional extraction focus |
| `use_dom` | True = active browser tab DOM (open the URL first) |
| `use_sampling` | Default true; client LLM summary when supported |

## Input

Provide **either** `loc` `[x,y]` **or** `label` (int from Snapshot), unless noted.

### `Click`

`loc` or `label`. `button`: `left` / `right` / `middle` (default `left`). `clicks`: `0` hover, `1` single, `2` double.

### `Type`

Required `text`. `loc` or `label`. `clear`, `press_enter` (bool or `"true"`/`"false"`). `caret_position`: `start` / `idle` / `end`.

### `Scroll`

`loc`, `label`, or current pointer if both omitted. `type`: `vertical` / `horizontal`. `direction`: `up` / `down` / `left` / `right`. `wheel_times` default 1.

### `Move`

`loc` or `label`. `drag` false = hover-move. `drag` true = drag; `from_loc` and `duration` require `drag=true`.

### `Shortcut`

Required `shortcut` such as `ctrl+c`, `alt+tab`, `win+r`, `ctrl+shift+esc`.

### `Wait`

Required `duration` (seconds).

### `WaitFor`

Polls UIA inside one call.

| Arg | Notes |
|-----|--------|
| `condition` | `text_exists`, `active_window`, `element_exists`, `element_enabled`, `focused_element` (aliases: `text`, `window`, `element`, `enabled`, `focused`) |
| `text` / `window_name` | Required depending on condition |
| `timeout` | Default 10; `(0, 120]` |
| `interval` | Default 0.25; `(0, 5]` |
| `use_dom` | Browser DOM text |

### `MultiSelect`

`locs` or `labels` (lists). `press_ctrl` default true.

### `MultiEdit`

`locs=[[x,y,text], ...]` or `labels=[[label,text], ...]`.

## System

### `App`

`mode`: `launch` (Start Menu `name`), `launch_executable` (`executable` + optional `args` / `cwd`), `resize` (`window_loc` / `window_size`), `switch` (`name`). `executable`/`args`/`cwd` are invalid outside `launch_executable`.

### `PowerShell`

Required `command`. Optional `timeout` seconds (default 30). Returns response + status code.

### `FileSystem`

`mode`: `read` / `write` / `copy` / `move` / `delete` / `list` / `search` / `info`.

Relative `path` / `destination` resolve from the user **Desktop** folder.

| Mode | Extra args |
|------|------------|
| `read` | `offset`, `limit`, `encoding` (default utf-8) |
| `write` | `content` required; `append` |
| `copy` / `move` | `destination` required; `overwrite` |
| `delete` | `recursive` for non-empty dirs |
| `list` | `pattern`, `recursive`, `show_hidden` |
| `search` | `pattern` required; `recursive` |
| `info` | metadata |

### `Registry`

PowerShell paths (`HKCU:\Software\...`). `mode`: `get` / `set` / `delete` / `list`.

| Mode | Args |
|------|------|
| `get` | `path` + `name` |
| `set` | `path` + `name` + `value`; `type` default `String` (confirm live enum) |
| `delete` | `path`; `name` optional (value vs key) |
| `list` | `path` |

### `Process`

`mode` `list`: optional `name`, `sort_by` (`memory` / `cpu` / `name`), `limit` (default 20).

`mode` `kill`: `pid` and/or `name`; `force`. Prefer PID of a process you spawned. Do not kill by name if the user may have the same app open.

### `Clipboard`

`mode` `get`, or `mode` `set` with `text`.

### `Notification`

Required `title`, `message`, `app_id` (Application User Model ID).
