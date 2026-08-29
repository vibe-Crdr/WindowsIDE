# 実行環境と言語制約（凍結）

このファイルの数値・パスは実行環境の定義である。推測で緩めない。

## OS とシェル

| 項目 | 値 |
| --- | --- |
| OS | Windows 11 64-bit |
| シェル（製品・ビルド・実行） | Windows PowerShell **5.1**（`powershell.exe`） |
| コマンドプロンプト | 同梱の `cmd.exe` |
| PowerShell 7 (`pwsh`) | 使わない |

PowerShell 7 構文は書かない（`&&` / `||` チェーン、`??`、`?:`、null 条件など）。

## Office

| 項目 | 値 |
| --- | --- |
| 製品 | Microsoft 365 Apps（MSO）64-bit |
| 用途 | Excel の VBA プロジェクト同期、診断 Compile（STA。Run とは別）、マクロ実行 |
| ビットネス | IDE（x64）と Excel（x64）を一致させる。32-bit Excel は対象外 |

VBA プロジェクト オブジェクト モデルへのアクセス信頼は、同期と診断 Compile の前提である。

## コンパイラ

製品のビルドとユーザー C# は指定 `csc.exe` だけを使う。ユーザー VB.NET（フェーズ P9。今は実装しない）は、同じフォルダの指定 `vbc.exe` だけを使う。csc に `.vb` を渡さない。vbc に `.cs` を渡さない。

### C#（製品およびユーザー）

| 項目 | 値 |
| --- | --- |
| パス | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe` |
| バナー | Microsoft (R) Visual C# Compiler version 4.8.9221.0 / for C# 5 |
| ファイルバージョン | 4.8.9221.0（ビルドタグ `NET481REL1LAST_25H2`） |

Visual Studio / Build Tools / Roslyn の `csc.exe` は、PATH にあっても使わない。

### VB.NET（ユーザーコードのみ。製品は使わない）

| 項目 | 値 |
| --- | --- |
| パス | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\vbc.exe` |
| バナー | Microsoft (R) Visual Basic Compiler version 14.8.9221 / for Visual Basic 2012 |
| ファイルバージョン | 14.8.9221.0 |

Visual Studio / Build Tools / Roslyn の `vbc.exe` は、PATH にあっても使わない。製品コードと製品ビルドは指定 `csc.exe` のままである。

## ランタイム

| 項目 | 値 |
| --- | --- |
| インストール済み FW | レジストリ `Version=4.8.09221`、`Release=533509` → **.NET Framework 4.8.1** |
| 対象 | `v4.0.30319` の 64-bit。.NET 5+ / .NET Core / .NET Standard は対象外 |

## 参照する BCL（識別）

コンパイラと同じフォルダの次を、同じフォルダの識別にする。

| ファイル | 識別 |
| --- | --- |
| `mscorlib.dll` | 指定 csc と同じ `v4.0.30319`。ゲートは 4.8.x（FileMajorPart=4 かつ FileMinorPart=8）。サービス更新可 |
| `System.dll` | 同上 |

観測例: 当時 4.8.9337 / 4.8.9340。この PC（2026-08-29）は mscorlib 4.8.9345.0 / System 4.8.9340。パッチ FileVersion はピンにしない。

ビルドは `/noconfig /nostdlib` とし、上記フォルダの DLL を明示参照する。詳細は [build.md](build.md)。

## 追加参照の許可範囲

IDE には WinForms 等が必要なので、**同じ Framework フォルダ**、**Windows が既に入れている GAC アセンブリ**、**Office の COM / PIA** は許可する。

許可の例:

- `System.Core.dll`, `System.Drawing.dll`, `System.Windows.Forms.dll`, `System.Xml.dll`, `Microsoft.CSharp.dll`
- `System.Management.Automation.dll`（Windows PowerShell 5.1）
- Excel / VBIDE の COM 相互運用（遅延バインディング推奨）

禁止:

- NuGet、`packages.config`、`PackageReference`
- 第三者 DLL のリポジトリ投入、手動コピー、サブモジュール
- AvalonEdit、Scintilla、DockPanelSuite、Roslyn、Newtonsoft.Json など
- `dotnet` SDK、MSBuild 必須のビルド（補助としても製品ビルドの正にはしない）

許可リストに無い Framework DLL を足すときは、[decisions.md](decisions.md) を更新してから `/r` に足す。

## 製品が使ってよい言語

| 用途 | 言語 |
| --- | --- |
| IDE 本体 | C# **5** のみ |
| ビルド / 起動補助 | Windows PowerShell 5.1 と cmd |
| IDE が扱うユーザーコード | C# 5、VB.NET（指定 `vbc.exe`、Visual Basic 2012。フェーズ P9。今は実装しない）、VBA、PowerShell 5.1、cmd / `.bat`（ホスト言語。実行・デバッグ対象。VB.NET のデバッグは段階） |
| IDE が扱う編集専用 | Markdown（`.md`）。ホストではない。実行・デバッグ・csc / vbc しない（フェーズ P8。今は実装しない） |

C# 6 以降の構文は、コンパイラが落とす。書かない。代表例は `.cursor/rules/csharp5.mdc`。

Python / Node / .NET SDK は **Cursor 用 `scripts/` だけ**。製品コード、製品ビルド、ユーザー向けランタイムに出さない。

## フォント

実行環境に Cascadia Mono / 源ノ角ゴシックは入っていない。OS インストールは要件にしない。半角は Cascadia Mono のみ（Cascadia Code は使わない）。SIL OFL の TTF/OTF を `assets/fonts/` から EXE へ埋め込む。これは外部ライブラリではない。詳細は [fonts.md](fonts.md)。
