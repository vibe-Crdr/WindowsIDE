# 決定事項

状態: **確定** はユーザー指定またはこのチャットで確認済み。**提案** は実装前提。覆すときはこの表と関連文書を同時に直す。

## 確定

| ID | 内容 |
| --- | --- |
| D1 | 実行環境は Windows 11 64-bit、PowerShell 5.1、Microsoft 365 64-bit、Framework `csc.exe` 4.8.9221.0（C# 5）、FW 4.8.1 |
| D2 | BCL の基準はコンパイラと同じフォルダの `mscorlib.dll` / `System.dll`（4.8.9337 / 4.8.9340） |
| D3 | 外部ライブラリのインストール・同梱は禁止 |
| D4 | ホスト言語は C# 5、VBA、PowerShell 5.1、cmd のみ |
| D5 | それらをデバッグおよび実行できる |
| D6 | VBA モジュールはディレクトリ管理し Excel と同期する。Excel VBA にディレクトリは反映しない |
| D7 | VS Code 等の IDE として必要な作業面は必須（自前実装） |
| D8 | 外観はダーク、neovim のような薄い UI |
| D9 | 半角英数記号フォントは **Cascadia Mono のみ**（同梱）。Cascadia Code は使わない（GDI+ でリガチャが出ないため） |
| D10 | 全角は 源ノ角ゴシック |
| D11 | UI は WinForms + 自前オーナードロー編集器。WPF は使わない |
| D12 | キーバインドは VS Code 風。Vim モーダルは初期対象外 |
| D13 | VBA の Excel 側名は設定 `namingMode`: `filename` または `folder_prefix`。両方実装する |
| D14 | 指定フォントは製品に同梱してプロセス内読み込み。実行 PC への事前インストールは要求しない |
| D15 | P0 で提案 P4 / P6 / P7 を採用する（覆すまで）。設定は XML、製品は単一 WinExe、C# は UTF-8 BOM |
| D16 | editor/@fontSize は 96dpi DIP。本文は GraphicsUnit.Pixel に換算。既定は **14 DIP**（VS Code `editor.fontSize` 14 相当）。13 から 18 は `Control.DeviceDpi` が 96 のままになる環境での見た目対策だった。`GetDpiForWindow` 修正後は 18 DIP が VS Code 14 より大きい。既存 `workspace.xml` はマイグレーションしない。DPI の正は **GetDpiForWindow**（無ハンドルは GetDpiForSystem）。`Control.DeviceDpi` は使わない。 |
| D17 | P0 スクロールバーは About 含め ThemedScrollBar、オートハイド |
| D18 | フォルダ選択と名前を付けて保存は Windows Common Item Dialog（IFileOpenDialog / IFileSaveDialog、ole32）。フォルダは FOS_PICKFOLDERS\|FOS_FORCEFILESYSTEM\|FOS_PATHMUSTEXIST。保存は FOS_OVERWRITEPROMPT\|FOS_FORCEFILESYSTEM\|FOS_PATHMUSTEXIST（PICKFOLDERS なし）。キャンセル HRESULT 0x800704C7 はエラーにしない。追加 /r なし。app.manifest に Common-Controls 6.0 amd64。新しいフォルダボタンは Explorer 既定 |
| D19 | About を開いたときライセンス TextBox は非選択。初期フォーカスは閉じる。手動選択してコピーは可 |
| D20 | 起動引数の既存ファイルは開く（ワークスペース外可）。親フォルダはワークスペースにしない。ディレクトリ引数は OpenFolder と同じ BindWorkspace（複数なら最後が勝つ）。失敗は MessageBox 1 枚で続行。全部失敗または無引数は無題。単一インスタンスは作らない。メニュー「ファイルを開く」とドラッグ＆ドロップは未決 |

## P0 で採用した提案

| ID | 内容 |
| --- | --- |
| P4 | 設定・マップは **XML**（`System.Xml` / `XmlDocument`）。P0 の `{workspace}/.windows-ide/workspace.xml` は `editor/@fontSize` と `editor/@tabSize` のみ。フォルダを開いただけでは作らない。`namingMode` / `vba-map` は P2 まで作らない |
| P6 | 製品は **単一 WinExe**。`build/out/WindowsIDE.exe` のみ（隣の PDB と `WindowsIDE.exe.config` は可）。クラスライブラリ分割はしない |
| P7 | 製品 C# ソースと新規ユーザー `.cs` は **UTF-8 BOM**。新規ファイルの改行は CRLF。VBA / CP932 既定は P2 |

## 提案（確認待ち）

| ID | 提案 | 理由 | 代替 |
| --- | --- | --- | --- |
| P5 | Excel は **遅延バインディング**（`Excel.Application` / `VBIDE`） | M365 の PIA バージョン差を避ける | インストール済み PIA を `/r` |
| P8 | 補完はキーワード + ファイル内シンボル。Roslyn 相当は作らない | Roslyn は外部かつ C# 6 世界 | 将来自前バインディング |
| P9 | cmd の「デバッグ」は **エコー付き実行と失敗行の表示** | cmd に CLR デバッガが無い | 自前 `.bat` インタープリタ |
| P10 | `namingMode` の既定は `filename`。document モジュール（ThisWorkbook / シート）は常に Excel の名前を使う | 既存ブックとの衝突を減らす | 既定を `folder_prefix` |

## まだ聞かないが後で決める

- UserForm（`.frm` / `.frx`）の扱い
- 複数ブック / 複数 VBA プロジェクト
- ログのローテーション
- メニュー「ファイルを開く」
- ドラッグ＆ドロップでファイルを開く
- 単一インスタンス（二重起動の集約）
