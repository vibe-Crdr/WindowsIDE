# WindowsIDE 要件定義

## 1. 目的

Windows 11 上で、指定の .NET Framework 4.8.1 と Framework `csc.exe`（C# 5）だけを使い、外部ライブラリ無しで動くローカル IDE を作る。

編集・実行・デバッグの対象は **C# 5、VBA、Windows PowerShell 5.1、cmd** に限る。VBA はディスク上でディレクトリ管理し、Excel と同期する。見た目はダーク、neovim 程度に薄い枠、フォントは半角 Cascadia Mono / 全角 源ノ角ゴシック。

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
| R3 | C# / VBA / PowerShell / cmd の字句ベース構文色分け |
| R4 | C# は Framework `csc.exe` でビルドし、生成 EXE/コードを実行できる |
| R5 | PowerShell 5.1 と cmd を実行し、標準出力・標準エラーをパネルに出す |
| R6 | 各ホスト言語をデバッグできる（段階はフェーズ表）。ブレーク、ステップ、出力、失敗箇所 |
| R7 | VBA ソースをディレクトリで管理し、Excel とプッシュ / プルできる。Excel 側にフォルダは作らない |
| R8 | 統合ターミナル（既定は PowerShell 5.1、切替で cmd） |
| R9 | コンパイラ診断を問題一覧に出し、クリックで行へ飛ぶ |
| R10 | 外観・フォント要件を満たす（[ui.md](ui.md)、[fonts.md](fonts.md)） |
| R11 | コマンドパレットとファイル名クイックオープン（VS Code の Ctrl+P / Ctrl+Shift+P 相当） |

## 4. 非機能要件

| ID | 要件 |
| --- | --- |
| N1 | 凍結環境以外を要求しない（[constraints.md](constraints.md)） |
| N2 | 外部ライブラリ禁止。編集器・ドッキング・JSON・ハイライトも自前またはインボックスのみ |
| N3 | 製品コードは C# 5。C# 6+ 構文禁止 |
| N4 | ビルドは PowerShell 5.1 から `csc.exe` を直接呼ぶ |
| N5 | 64-bit 専用（`/platform:x64`）。Excel COM も x64 |
| N6 | ソースは C# / PS を UTF-8（csc 向け BOM あり）、VBA は日本語 Excel に合わせ CP932 を既定 |
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

## 6. フェーズ

| フェーズ | 目標 |
| --- | --- |
| P0 | ウィンドウ、ダーク UI、デュアルフォント描画、ファイルツリー、タブ、保存、UTF-8 編集 |
| P1 | 字句ハイライト、検索、問題一覧、`csc` 実行、PS/cmd 実行、統合ターミナル |
| P2 | VBA ディレクトリ ↔ Excel プッシュ / プル（`.bas` / `.cls`） |
| P3 | PowerShell デバッグ（ブレークポイント、ステップ、ローカル） |
| P4 | C# デバッグ（PDB + CLR デバッグ API）。cmd は行単位実行または出力トレース |
| P5 | コマンドパレット、クイックオープン、簡易補完（キーワード + ファイル内シンボル） |
| P6 | VBA デバッグ（Excel VBE 連携 / `Application.Run` と実行時エラー） |

P0→P2 を最初の利用可能な IDE とする。P3 以降はデバッガ品質を上げる。
