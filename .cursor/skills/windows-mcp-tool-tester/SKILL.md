---
name: windows-mcp-tool-tester
description: >-
  Automated testing skill for Windows-MCP tools. Use this skill whenever the user
  wants to test, validate, benchmark, or evaluate any Windows-MCP tool (App,
  PowerShell, Screenshot, Snapshot, DisplayInventory, Click, Type, Scroll, Move,
  Shortcut, Wait, WaitFor, MultiSelect, MultiEdit, Clipboard, Process,
  Notification, FileSystem, Registry, Scrape). Triggers on phrases like "test
  the Click tool", "benchmark Screenshot", "validate FileSystem", "run QA on
  Registry", "check if PowerShell works", "evaluate tool performance", or any
  mention of testing/validating a Windows-MCP tool. Each invocation tests
  exactly ONE tool.
---

# Windows-MCP Tool Tester

Official CursorTouch tester, adapted for this machine: all **20** tools are enabled, including `WaitFor` and `DisplayInventory`.

Generates test cases for a single Windows-MCP tool, executes them, and writes a structured report. Usage skill for everyday automation: `mcp-windows`.

## Core Principles

- **One tool per invocation.** If the user does not specify a tool, ask first.
- **Black-box testing only.** Derive cases from the live MCP description and parameter schema (`GetMcpTools`). Never read Windows-MCP source to excuse a failure.
- **Auto-generate** common paths, edges, parameter combinations, and errors.
- **Measure** correctness and end-to-end response time.
- **Mandatory side-effect verification** for every mutating call, using a **different** tool (usually `PowerShell`).
- **Safe cleanup:** record PIDs you spawn; kill by PID only, never by process name.
- **Safety first:** Windows-MCP has full system access. Destructive tests (`FileSystem` delete, `Registry` set/delete, `Process` kill, `PowerShell`) need an explicit user OK. VM / Windows Sandbox is recommended.
- Report template: [report.md](report.md). Per-tool hints: [tool-hints.md](tool-hints.md).

## Step 0: Identify the Target Tool

If unspecified, ask the user to pick one:

> App, PowerShell, Screenshot, Snapshot, DisplayInventory, Click, Type, Scroll, Move, Shortcut, Wait, WaitFor, MultiSelect, MultiEdit, Clipboard, Process, Notification, FileSystem, Registry, Scrape

Do not test multiple tools in one session.

## Step 1: Analyze the Tool

From the live schema, list parameters (required/optional/defaults/enums), modes, success vs error shape, side effects, and dependencies (open window, existing file). Silence in the schema is a documentation gap — probe it.

## Step 2: Generate Test Cases

Cover what applies. Aim for **10–20** cases. Estimate ~30–45s each (App launch +5–10s). Present the plan and get confirmation before running.

### Category A: Basic Functionality (Required)

One case per mode with realistic inputs.

### Category B: Parameter Variations (Required)

Each optional param; every enum; booleans true and false. For `anyOf` bool|string (`drag`, `use_vision`): test `true`/`false` and `"true"`/`"false"`. Also try `"yes"` / `"1"` — if those fail while `"true"` passes, the transport likely coerced strings. For nullable: valid value and explicit `null`.

### Category C: Edge Cases (Required)

Empty strings, zero, negatives, Unicode, very large/small numbers, long `Type` text, `timeout=0` where relevant.

### Category D: Error Handling (Required)

Missing required args, bad types, missing files/windows/PIDs/keys, operations that should fail gracefully.

### Category E: Parameter Interaction (When Applicable)

e.g. `Click` with both `loc` and `label`; mode-specific args on the wrong mode.

### Category F: Idempotency & State (When Applicable)

`idempotentHint: true` → call twice. Destructive tools → cleanup must work.

### Test Case Format

```
ID:          TC-{ToolName}-{Number}
Category:    A/B/C/D/E/F
Description: What this test verifies
Parameters:  The exact parameters to pass
Expected:    Success/failure and key response content
Setup:       Prerequisites
Teardown:    Cleanup
```

## Step 3: Execute Tests

### Pre-Test: Environment

```powershell
(Get-CimInstance Win32_OperatingSystem).Caption + " " + (Get-CimInstance Win32_OperatingSystem).Version
Get-CimInstance Win32_VideoController | Select-Object CurrentHorizontalResolution, CurrentVerticalResolution
(Get-CimInstance Win32_PnPEntity | Where-Object { $_.PNPClass -eq 'Monitor' -and $_.Status -eq 'OK' }).Count
Get-ItemProperty 'HKCU:\Control Panel\Desktop\WindowMetrics' -Name AppliedDPI -ErrorAction SilentlyContinue | Select-Object -ExpandProperty AppliedDPI
```

Call `Screenshot` once (Original Size vs DPI; Active/All Desktops). Prefer `DisplayInventory` for monitor metadata.

### Pre-Test: Input tools

For Type, Click, Scroll, Move, Shortcut, MultiSelect, MultiEdit:

1. **IME:** If Snapshot tray shows a non-English IME, `Shortcut` to English (often `shift`). Restore after tests. Critical for `Type`.
2. **Labels:** Snapshot the target. Win11 Notepad editor often has **no** interactive label — use `loc`. If a planned label is missing, adapt or SKIP.
3. **Warm-up:** 1–2 throwaway calls (not scored).

### Per test

> Snapshot labels go stale after UI changes. Reset shared windows between cases (Type: Ctrl+A then Delete; Scroll: Ctrl+Home).

1. Setup; record spawned PIDs.
2. Start time via PowerShell:
   ```powershell
   [long](([System.DateTime]::UtcNow - [System.DateTime]::UnixEpoch).TotalMilliseconds)
   ```
3. Call the MCP tool.
4. End time with the same command. `elapsed_ms = t_end - t_start` (includes MCP overhead ~3–5s per timestamp). When testing `PowerShell` itself, record time as `N/A (self-referential)`.
5. Store the response. Size = character count of the **text** part (`+image` for Screenshot/Snapshot).
6. Score correctness. Independently verify mutating calls (see [tool-hints.md](tool-hints.md)). If the tool said success but verification failed → **FAIL**.
7. Teardown.

Never estimate times. If measurement is impossible, `N/A` plus why.

### Results

| Result | Meaning |
|--------|---------|
| PASS | Matches expected |
| SOFT PASS | Acceptable but messy (whitespace, order) |
| FAIL | Wrong behavior, including rejecting schema-valid input |
| ERROR | Exception or timeout |
| SKIP | Missing prerequisite (document why) |

Prefer **Adapt** (e.g. `loc` instead of `label`) over SKIP when the intent is still testable.

**Timing bands (end-to-end, including MCP):** Fast &lt; 5s; Normal 5–10s; Slow 10–20s; Very Slow &gt; 20s. Exclude warm-up from aggregates.

## Isolation

- **FileSystem:** `%TEMP%\wmcp-test-{timestamp}\` then delete.
- **Registry:** `HKCU:\Software\WMCP-Test-{timestamp}` then delete the key.
- **Process:** list by default; for kill, spawn notepad and kill **that PID**.
- **App:** Notepad/Calculator. Diff PIDs before/after (Win11 Notepad may reuse a process). Never `Stop-Process -Name`.
- **Clipboard:** save and restore.
- **Input:** dedicated test window, not the user's work.
- **Notification:** one clearly labeled toast per case.
- **PowerShell:** prefer read-only commands.
- **DisplayInventory / Screenshot / Snapshot / Scrape:** read-only; Scrape only public lightweight URLs.

## Step 4: Report

Write the report in the user's language if they asked. Keep IDs like `TC-Move-01` in English. Follow [report.md](report.md).
