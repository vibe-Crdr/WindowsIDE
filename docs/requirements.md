# WindowsIDE 要件定義

## 1. 目的

Windows 11 上で、指定の .NET Framework 4.8.1 と Framework `csc.exe`（C# 5）だけを使い、外部ライブラリ無しで動くローカル IDE を作る。

編集・実行・デバッグの対象は **C# 5、VB.NET（指定 Framework `vbc.exe`）、VBA、Windows PowerShell 5.1、cmd**。VB.NET の実装はフェーズ P9（今は実装しない）。VBA はディスク上でディレクトリ管理し、Excel と同期する（`.vb` は VBA ではない）。見た目はダーク、neovim 程度に薄い枠、フォントは半角 Cascadia Mono / 全角 源ノ角ゴシック。編集器本文には全ファイル形式のインデント線（F-IG。今は実装しない）。

VS Code 級の作業面（エクスプローラ、タブ、検索、問題一覧、統合ターミナル、実行とデバッグ、コマンドパレット）を、自前実装で満たす。Electron や VS Code 本体は使わない。

## 2. 想定利用者と利用形態

- 利用者は開発者本人（単一ユーザー、ローカル）。
- ネットワーク必須機能は作らない（拡張マーケット、テレメトリ、アカウントなし）。
- 対象 PC は 16 GB RAM クラス。常駐は軽く保つ。

## 3. 機能要件（要約）

詳細と受け入れ条件は [ide-features.md](ide-features.md)。VBA 同期は [vba-workspace.md](vba-workspace.md)。

| ID | 要件 |
| --- | --- |
| R1 | フォルダをワークスペースとして開き、ツリー表示・開く・保存できる |
| R2 | 複数タブのテキスト編集。行番号、Undo/Redo、検索置換、インデント |
| R3 | C# / VBA / PowerShell / cmd の字句ベース構文色分け（P1）。P9 で VB.NET（`.vb`）を足す。Markdown は R14 であり R3 に入れない |
| R4 | C# は Framework `csc.exe` でビルドし、生成 EXE/コードを実行できる |
| R5 | PowerShell 5.1 と cmd を実行し、標準出力・標準エラーをパネルに出す |
| R6 | 各ホスト言語をデバッグできる（段階はフェーズ表）。ブレーク、ステップ、出力、失敗箇所 |
| R7 | VBA ソースをディレクトリで管理し、Excel とプッシュ / プルできる。Excel 側にフォルダは作らない |
| R8 | 統合ターミナル（既定は PowerShell 5.1、切替で cmd） |
| R9 | 診断を問題一覧に出し、クリックで行へ飛ぶ。C# は Framework `csc.exe`、PowerShell は `ParseInput`（実行しない）。VBA は Excel Compile（診断のみ、Run しない。フェーズ P7。今は実装しない）。VB.NET は指定 `vbc.exe`（F-VB-BLD / F-VB-LIVE。フェーズ P9。今は実装しない）。手動ビルドも常時コンパイルも同じ診断モデル。常時コンパイルは実行しない |
| R10 | 外観・フォント要件を満たす（[ui.md](ui.md)、[fonts.md](fonts.md)） |
| R11 | コマンドパレットとファイル名クイックオープン（VS Code の Ctrl+P / Ctrl+Shift+P 相当） |
| R12 | 入力停止後、Framework `csc.exe` で診断のみ再コンパイルし、問題一覧と波線に出す。生成物は起動しない（フェーズ P7。今は実装しない） |
| R13 | ユーザーソース上の定義へ移動と、ホバー（F-DOC 枠 / `///` / 直前コメント / Framework XML）。枠コメント挿入（F-DOC）。P7 の対象は C# / VBA / PowerShell / cmd。VB.NET の F-GD / F-HOV / F-DOC はフェーズ P9（今は実装しない） |
| R14 | Markdown（`.md`）の字句色分けとプレビュー。ホスト言語ではない。実行・デバッグしない（フェーズ P8。今は実装しない） |
| R15 | VB.NET をホストとして扱う（`.vb`、指定 Framework `vbc.exe` でビルド、実行。デバッグは段階）。VBA / VBScript ではない。フェーズ P9。今は実装しない |
| R16 | 編集器本文のインデント線（F-IG）。すべてのファイル形式（Plain / 無題を含む）。言語非依存。今は実装しない |

## 4. 非機能要件

| ID | 要件 |
| --- | --- |
| N1 | 凍結環境以外を要求しない（[constraints.md](constraints.md)） |
| N2 | 外部ライブラリ禁止。編集器・ドッキング・JSON・ハイライトも自前またはインボックスのみ |
| N3 | 製品コードは C# 5。C# 6+ 構文禁止 |
| N4 | ビルドは PowerShell 5.1 から `csc.exe` を直接呼ぶ |
| N5 | 64-bit 専用（`/platform:x64`）。Excel COM も x64 |
| N6 | ソースは C# / VB.NET / PS を UTF-8（csc / vbc 向け BOM あり）、VBA は日本語 Excel に合わせ CP932 を既定、cmd（`.cmd` / `.bat`）は CP932・BOM なし（cmd.exe が UTF-8 BOM を先頭コマンドの一部として読むため） |
| N7 | 未処理例外は黙って消さない。ログと UI に出す |
| N8 | 設定はワークスペースの XML（`System.Xml`）。第三者シリアライザ禁止 |
| N9 | DPI 認識（Windows 11 の拡大表示で極端にボケない） |
| N10 | Cascadia Mono と源ノ角ゴシックは EXE に同梱。対象 PC へのフォントインストールは要求しない（[fonts.md](fonts.md)） |

## 5. 対象外（初期）

- Git クライアント、リモート SSH、コンテナ、WSL 統合
- C# 6+、.NET Core / 5+、PowerShell 7、32-bit Office
- 拡張機能ホスト、LSP サーバ導入、NuGet UI
- 共同編集、クラウド同期
- Vim モーダル編集（見た目は neovim、キーは VS Code 風。D12）
- UserForm の完全ビジュアル編集（`.frm` / `.frx` は後相）
- 常時コンパイルによる EXE 自動起動
- バックグラウンドスレッドの Compile / 自動 Run
- Object Browser
- BCL ソースへの F12（定義へ移動）

## 6. フェーズ

| フェーズ | 目標 |
| --- | --- |
| P0 | ウィンドウ、ダーク UI、デュアルフォント描画、ファイルツリー、タブ、保存、UTF-8 編集 |
| P1 | 字句ハイライト、検索、問題一覧、`csc` 実行、PS/cmd 実行、統合ターミナル |
| P2 | VBA ディレクトリ ↔ Excel プッシュ / プル（`.bas` / `.cls`） |
| P3 | PowerShell デバッグ（ブレークポイント、ステップ、ローカル） |
| P4 | C# デバッグ（PDB + CLR デバッグ API）。cmd は行単位実行または出力トレース |
| P5 | コマンドパレット、クイックオープン、簡易補完（C# / VBA / PowerShell / cmd。キーワード + ファイル内シンボル。VB.NET の F-CMP はフェーズ P9） |
| P6 | VBA デバッグ（Excel VBE 連携 / `Application.Run` と実行時エラー） |
| P7 | 編集器インテリジェンス（対応括弧、スマートインデント、自動閉じ、定義へ移動、ホバー、枠コメント、VBA キーワード大文字小文字、常時 csc、波線、VBA Compile 診断）。対象ホストは C# / VBA / PowerShell / cmd。VB.NET の同系統はフェーズ P9。P7-A 着手。B/C は今は実装しない。着手時期は採用済み P16（P2 完了の直後） |
| P8 | Markdown 字句とプレビュー、VBAProject 参照、キー記録マクロ。今は実装しない。P5（F-PAL）と P7 の後 |
| P9 | VB.NET ホスト（`.vb`、指定 Framework `vbc.exe` でビルドと実行。デバッグは段階）。今は実装しない。P7-A / P8 に押し込まない |

F-IG（インデント線）はフェーズ番号を持たない独立スライスである。P7-A に混ぜない。P9 より先でよい。今は実装しない。

P0→P2 を最初の利用可能な IDE とする。P3–P6 はデバッガ品質を上げる。フェーズ P7 は編集器インテリジェンスであり、P0–P2 の必須には含めない。フェーズ番号の P7 は、採用済み提案 P7（UTF-8 BOM）とは別である。フェーズ番号の P8 は、提案 P8（F-CMP の P5 必須範囲）とは別である。フェーズ番号の P9 は VB.NET ホストであり、提案番号とは別である。
