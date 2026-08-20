"""
Summary:
    Static guard for the WindowsIDE source tree that runs on any platform,
    including the Linux Cloud Agent VM where the Windows-only product build
    (Framework csc.exe / PowerShell 5.1) cannot run.

    It enforces the subset of the project's frozen rules that can be verified
    without Windows tooling, so an agent editing docs or C# 5 source gets fast
    feedback before the authoritative build on a Windows machine:

      * every product/test .cs file carries a UTF-8 BOM (docs/build.md, P7);
      * the three bundled SIL OFL font files are present (docs/fonts.md);
      * no Cascadia Code font leaks into assets/fonts (docs/decisions.md D9);
      * every /r: reference in build/*.rsp stays inside the frozen Framework
        folder, i.e. no NuGet / third-party DLL references (docs/constraints.md);
      * every source path listed in a .rsp exists on disk, and every product
        .cs under src/ is wired into build/windows-ide.rsp.

    Heuristic PowerShell 5.1 checks on build/product .ps1 files are reported as
    warnings (never fatal) because they can false-positive inside strings.

    This is Cursor-side tooling only. It is never shipped in the product EXE and
    it never replaces the mandated Framework csc.exe build.

Arguments:
    None. All paths are derived from this script's location.

Returns:
    Exit code 0 when no errors are found, 1 otherwise. Warnings never fail.
"""

from __future__ import annotations

import re
import sys
from pathlib import Path

PROJECT_ROOT = Path(__file__).resolve().parents[1]

UTF8_BOM = b"\xef\xbb\xbf"
FRAMEWORK_DIR = r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319"
SMA_DLL = (
    r"C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Management.Automation"
    r"\v4.0_3.0.0.0__31bf3856ad364e35\System.Management.Automation.dll"
)

BUNDLED_FONTS = (
    "assets/fonts/cascadia/CascadiaMono-Regular.ttf",
    "assets/fonts/cascadia/CascadiaMono-Bold.ttf",
    "assets/fonts/source-han-sans/SourceHanSansJP-Regular.otf",
)

# PowerShell 7 tokens that must not appear in product/build scripts.
PS7_PATTERNS = (
    (re.compile(r"\bpwsh\b"), "uses pwsh (PowerShell 7); build scripts run under powershell.exe 5.1"),
    (re.compile(r"&&"), "uses && chain (PowerShell 7 syntax)"),
    (re.compile(r"\|\|"), "uses || chain (PowerShell 7 syntax)"),
    (re.compile(r"\?\?"), "uses ?? null-coalescing (PowerShell 7 syntax)"),
)


class Report:
    """収集した違反（致命）と警告（非致命）をまとめる。"""

    def __init__(self):
        self.errors = []
        self.warnings = []

    def error(self, message):
        self.errors.append(message)

    def warn(self, message):
        self.warnings.append(message)


def _rel(path):
    """リポジトリ相対のパス文字列を返す。ルート外なら絶対のまま。"""
    try:
        return str(path.relative_to(PROJECT_ROOT))
    except ValueError:
        return str(path)


def _cs_files(*subdirs):
    """指定サブディレクトリ配下の .cs をソートして列挙する。"""
    result = []
    for subdir in subdirs:
        base = PROJECT_ROOT / subdir
        if base.is_dir():
            result.extend(sorted(base.rglob("*.cs")))
    return result


def check_bom(report):
    """src/ と tests/ の .cs が UTF-8 BOM 付きかを検査する。"""
    files = _cs_files("src", "tests")
    if not files:
        report.warn("No .cs files found under src/ or tests/.")
        return
    for path in files:
        head = path.read_bytes()[:3]
        if head != UTF8_BOM:
            report.error("Missing UTF-8 BOM: " + _rel(path))


def check_fonts(report):
    """同梱フォント 3 種の存在と Cascadia Code の不在を検査する。"""
    for rel in BUNDLED_FONTS:
        if not (PROJECT_ROOT / rel).is_file():
            report.error("Bundled font missing: " + rel)

    fonts_dir = PROJECT_ROOT / "assets" / "fonts"
    if fonts_dir.is_dir():
        for path in fonts_dir.rglob("*"):
            if path.is_file() and path.name.startswith("CascadiaCode"):
                report.error("Cascadia Code must not be in the repo: " + _rel(path))


def _parse_rsp(path):
    """.rsp を (references, sources) に分けて返す。コメントと空行は無視。"""
    references = []
    sources = []
    for raw in path.read_text(encoding="utf-8").splitlines():
        line = raw.strip()
        if not line or line.startswith("#"):
            continue
        if line.startswith("/r:"):
            references.append(line[len("/r:"):])
        elif line.startswith("/"):
            continue
        else:
            sources.append(line)
    return references, sources


def check_rsp(report):
    """build/*.rsp の参照範囲と列挙ソースの実在を検査する。"""
    build_dir = PROJECT_ROOT / "build"
    rsp_files = sorted(build_dir.glob("*.rsp")) if build_dir.is_dir() else []
    if not rsp_files:
        report.warn("No .rsp files found under build/.")
        return

    for rsp in rsp_files:
        references, sources = _parse_rsp(rsp)
        for ref in references:
            if not ref.startswith(FRAMEWORK_DIR) and ref != SMA_DLL:
                report.error(
                    "Out-of-frame reference in " + _rel(rsp) + ": " + ref
                    + " (only the frozen Framework folder or SMA GAC may be referenced)"
                )
        for src in sources:
            src_path = PROJECT_ROOT / src.replace("\\", "/")
            if not src_path.is_file():
                report.error(
                    "Source listed in " + _rel(rsp) + " does not exist: " + src
                )

    # すべての製品 .cs が windows-ide.rsp に載っているか。
    product_rsp = build_dir / "windows-ide.rsp"
    if product_rsp.is_file():
        _, listed = _parse_rsp(product_rsp)
        listed_set = set(s.replace("\\", "/") for s in listed)
        for path in _cs_files("src"):
            rel = _rel(path).replace("\\", "/")
            if rel not in listed_set:
                report.error(
                    "Product source not wired into build/windows-ide.rsp: " + rel
                )


def check_no_nuget(report):
    """NuGet の痕跡（packages.config / PackageReference）が無いことを検査する。"""
    for name in ("packages.config",):
        for path in PROJECT_ROOT.rglob(name):
            if ".git" in path.parts:
                continue
            report.error("NuGet artifact present: " + _rel(path))
    for path in list(PROJECT_ROOT.rglob("*.csproj")) + list(PROJECT_ROOT.rglob("*.props")):
        text = path.read_text(encoding="utf-8", errors="ignore")
        if "PackageReference" in text:
            report.error("PackageReference found in " + _rel(path))


def _strip_ps_comments(text):
    """PowerShell の行コメント（# 以降）を除いた本文を返す。文字列内は考慮しない簡易版。"""
    lines = []
    for line in text.splitlines():
        hash_index = line.find("#")
        if hash_index != -1:
            line = line[:hash_index]
        lines.append(line)
    return "\n".join(lines)


def check_powershell(report):
    """build/ と scripts/ の .ps1 に PowerShell 7 構文が無いかを警告する。"""
    ps1_files = []
    for subdir in ("build", "src", "."):
        base = PROJECT_ROOT / subdir
        if base.is_dir():
            ps1_files.extend(base.rglob("*.ps1"))
    seen = set()
    for path in sorted(ps1_files):
        if path in seen or ".git" in path.parts:
            continue
        seen.add(path)
        text = _strip_ps_comments(path.read_text(encoding="utf-8", errors="ignore"))
        for pattern, why in PS7_PATTERNS:
            if pattern.search(text):
                report.warn(_rel(path) + ": " + why)


def main():
    """全チェックを実行し、結果を表示して終了コードを返す。"""
    report = Report()
    check_bom(report)
    check_fonts(report)
    check_rsp(report)
    check_no_nuget(report)
    check_powershell(report)

    for message in report.warnings:
        print("WARN : " + message)
    for message in report.errors:
        print("ERROR: " + message)

    print("")
    print(
        "check_sources: {0} error(s), {1} warning(s).".format(
            len(report.errors), len(report.warnings)
        )
    )
    print(
        "Note: the authoritative build is Windows-only "
        "(Framework csc.exe C# 5, PowerShell 5.1); this guard only covers "
        "platform-independent invariants."
    )
    return 1 if report.errors else 0


if __name__ == "__main__":
    sys.exit(main())
