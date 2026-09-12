# Windows-MCP Tool Test Report: {ToolName}

**Date:** {timestamp}
**Tool:** {ToolName}
**Total Test Cases:** {N}
**PASS:** {P} | **SOFT PASS:** {SP} | **FAIL:** {F} | **ERROR:** {E} | **SKIP:** {S}
**Overall Pass Rate:** {(P+SP)/N * 100}%

---

## 1. Test Environment

| Item | Value |
|------|-------|
| OS Version | {e.g., Windows 11 Pro 10.0.26200} |
| Display Resolution | {e.g., 2560x1440} |
| Screenshot Original Size | {resolution x scale} |
| Display Count | {n} |
| Active Virtual Desktop | {e.g., Desktop 1} |
| MCP Transport | {stdio via windows-mcp} |
| Scale Factor | {e.g., 150% (AppliedDPI=144)} |

## 2. Executive Summary

2–3 sentences. Note error-message quality, input validation, consistency, graceful degradation.

## 3. Failed & Error Test Cases

For each non-pass:

### TC-{ID}: {Description}

- **Category:**
- **Parameters:**
- **Expected:**
- **Actual:**
- **Side-Effect Verification:**
- **Root Cause Analysis:**
- **Suggested Fix:**

If all passed: "All test cases passed. No issues to report."

## 4. Performance Analysis

Times are end-to-end (MCP overhead included), not pure tool time. For server-side stages set `WINDOWS_MCP_PROFILE_SNAPSHOT=1`.

### Response Time

| Test Case | Time (ms) | Assessment |
|-----------|-----------|------------|
| TC-XXX-01 | | |

**Average / Median / P95 / Max**

Bands: Fast &lt; 5000ms; Normal 5000–10000; Slow 10000–20000; Very Slow &gt; 20000.

### Response Size

| Test Case | Response Size (chars) |
|-----------|-----------------------|
| TC-XXX-01 | |

## 5. Environmental Interference & Notes

| # | Factor | Impact | Mitigation |
|---|--------|--------|------------|
| 1 | | | |

Common: IME, notification popups, focus steals, screen lock, clipboard managers.

If none: "No environmental interference observed."

## 6. Documentation & Schema Gaps

| # | Gap Type | Description | Recommendation |
|---|----------|-------------|----------------|
| 1 | schema / description / behavior | | |

If none: "No documentation or schema gaps identified."

## 7. All Test Cases

| ID | Category | Description | Result | Time (ms) | Response Size |
|----|----------|-------------|--------|-----------|---------------|
| TC-XXX-01 | A - Basic | | PASS | | |
