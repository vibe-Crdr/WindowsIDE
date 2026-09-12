# Tool-specific testing hints

Always read the **live** schema first. Verification **must not** use the same tool under test.

## DisplayInventory

- No args. Cross-check bounds/DPI with Screenshot Original Size and `Get-CimInstance Win32_VideoController`.
- Read-only; no side-effect step.

## App

- Modes: `launch`, `launch_executable`, `resize`, `switch`.
- Launch known apps (`notepad`, `calc`) and an unknown name.
- `launch_executable` with a real path vs missing file; `args`/`cwd` only in that mode.
- Verify with `Get-Process` and Screenshot/Snapshot. Kill extra PIDs only.

## PowerShell

- `echo "hello"`, `Get-Date`, `Get-Process | Select-Object -First 3`.
- Short timeout vs long command. Unicode. stderr. failing `Get-Item`.
- Timing is self-referential — use status codes, not elapsed_ms.

## Screenshot / Snapshot

- Screenshot: default, annotation, reference lines, `display`, `region`.
- Snapshot: `use_vision`, `use_dom`, `use_annotation`, `use_ui_tree` on/off.
- Confirm image data on Screenshot / vision Snapshot.

## Click / Type / Scroll / Move / Shortcut

- Click: `loc` vs `label`; left/right/middle; clicks 0/1/2; invalid coords/labels.
- Type: Unicode, emoji BMP-outside, `clear`, `press_enter`, caret start/idle/end, empty string, IME on.
- Scroll: vertical/horizontal; wheel_times 1/5/10.
- Move: hover vs `drag=true` with `from_loc` / `duration`.
- Shortcut: `ctrl+c`/`ctrl+v`/`ctrl+a`/`alt+tab`/`win+r`; invalid key names.
- Verify Move/Click/Shortcut with Screenshot or Snapshot. Type: Ctrl+A, Ctrl+C, then `Get-Clipboard`.

## Wait / WaitFor

- Wait: 1s and 0s; elapsed should roughly match.
- WaitFor: each condition; missing `text`; timeout that should fail; `use_dom`.

## MultiSelect / MultiEdit

- Coords vs labels; `press_ctrl` true/false; empty list; mixed valid/invalid.

## Clipboard

- get with text, get empty/non-text, set Unicode, set-then-get roundtrip.
- Verify set with PowerShell `Get-Clipboard`, not Clipboard get.

## Process

- list: default sort, `memory`/`cpu`/`name`, name filter, limits.
- kill: sacrificial PID only. Verify with `Get-Process -Id`.

## Notification

- Valid title/message/`app_id`. Empty strings. Special characters. User-visible — keep to one toast per case.

## FileSystem

- All modes. Verify with `Test-Path` / `Get-Content` / `Get-ChildItem`, never FileSystem itself.
- Relative paths land on the user Desktop — use `%TEMP%\wmcp-test-*` absolute paths.

## Registry

- get/set/delete/list. Roundtrip. String/DWord/QWord if the schema allows.
- Test key only under `HKCU:\Software\WMCP-Test-*`. Verify with `Get-ItemProperty` / `reg query`.

## Scrape

- Lightweight public URL; with/without `query`; `use_dom` (browser open); invalid URL.
- Do not aim at private/loopback URLs (SSRF guard should reject).
