# WindowsIDE

Windows 11 上で、.NET Framework 4.8.1 の Framework `csc.exe`（C# 5）だけを使って動くローカル IDE。外部ライブラリは入れない。

編集・実行・デバッグの対象は C# 5、VB.NET（指定 Framework `vbc.exe`、フェーズ P9。今は実装しない）、VBA、Windows PowerShell 5.1、cmd。VBA はフォルダで管理し、Excel（Microsoft 365 64-bit）と同期する。Excel 側の VBA は平坦なまま。`.vb` は VBA ではない。

## 仕様

文書の正は [docs/](docs/) 。入口は [docs/README.md](docs/README.md) と [AGENTS.md](AGENTS.md)。

| 項目 | 値 |
| --- | --- |
| OS | Windows 11 64-bit |
| シェル | Windows PowerShell 5.1 |
| Office | Microsoft 365 MSO 64-bit |
| コンパイラ | 製品とユーザー C# は `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`（C# 5）。ユーザー VB.NET は同フォルダの `vbc.exe`（Visual Basic 2012。フェーズ P9。今は実装しない） |
| ランタイム | .NET Framework 4.8.1 |
| UI | ダーク、薄い枠。半角 Cascadia Mono、全角 源ノ角ゴシック（EXE 同梱。OS へのフォントインストール不要） |

## リポジトリ構成

| パス | 用途 |
| --- | --- |
| `docs/` | 要件と設計 |
| `.cursor/rules/` | エージェント用ルール |
| `scripts/` | Cursor MCP 起動（製品に含めない） |
| `src/WindowsIDE/` | 製品ソース（実装時） |
| `build/` | `csc` 用スクリプト（実装時） |

## ビルド（実装後）

製品は `dotnet` / NuGet / MSBuild を使わない。手順は [docs/build.md](docs/build.md)。
