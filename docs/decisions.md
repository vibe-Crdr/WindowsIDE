# 決定事項

状態: **確定** はユーザー指定またはこのチャットで確認済み。**提案** は実装前提。覆すときはこの表と関連文書を同時に直す。

## 確定

| ID | 内容 |
| --- | --- |
| D1 | 実行環境は Windows 11 64-bit、PowerShell 5.1、Microsoft 365 64-bit、Framework `csc.exe` 4.8.9221.0（C# 5）、FW 4.8.1 |
| D2 | BCL の基準はコンパイラと同じフォルダの `mscorlib.dll` / `System.dll`（4.8.9337 / 4.8.9340） |
| D3 | 外部ライブラリのインストール・同梱は禁止 |
| D4 | ホスト言語は C# 5、VBA、PowerShell 5.1、cmd のみ。Markdown（`.md`）は編集言語でありホストではない（実行・デバッグ・csc しない） |
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
| D21 | フェーズ P7 で提案 P14 / P16 / P17 をこのチャットで採用する（覆すまで）。P16 / P17 は P7-A で実装する。P14（常時コンパイル単位）は波 C までコードを書かない |
| D22 | このチャットで提案 P19 / P21 / P22 を採用する（覆すまで）。P19 の実装はフェーズ P7-B、P21 / P22 の実装はフェーズ P8。今はコードを書かない |
| D23 | ユーザー `.cmd` / `.bat` は新規・0 バイト Open・無題 SaveAs が CP932 BOM なし CRLF。保存時は BOM を書かない。非空 UTF-8 BOM の Open は検出どおり UTF-8 のまま、Save で BOM だけ落とす（本文は再エンコードしない）。TryDecode は変えない。 |

## P0 で採用した提案

| ID | 内容 |
| --- | --- |
| P4 | 設定・マップは **XML**（`System.Xml` / `XmlDocument`）。P0 の `{workspace}/.windows-ide/workspace.xml` は `editor/@fontSize` と `editor/@tabSize` のみ。`vba-map` は P2 の同期操作で作る。workspace.xml に namingMode は書かない。フォルダを開いただけでは `.windows-ide` を作らない |
| P6 | 製品は **単一 WinExe**。`build/out/WindowsIDE.exe` のみ（隣の PDB と `WindowsIDE.exe.config` は可）。クラスライブラリ分割はしない |
| P7 | 製品 C# ソースと新規ユーザー `.cs` は **UTF-8 BOM**。新規ファイルの改行は CRLF。VBA / CP932 既定は P2。`.cmd` / `.bat` は D23 |

## P1 で採用した提案

| ID | 内容 |
| --- | --- |
| P11 | 言語は拡張子のみ。無題・不明は Plain。字句は行開始状態 + 行スキャナ。Regex / Roslyn は使わない。キーワードはソース内静的表。 |
| P12 | C# の識別子色は自前束縛（行レキサの上にオーバーレイ）。TokenKind Local / Instance / Method / Type。ファイル内シンボル表 + ワークスペース `.cs` 型名 + Framework Reflection（既存 `/r` と同じ DLL を LoadFrom）。Roslyn・Regex ホットパスは使わない。VBA / PowerShell は行内ヒューリスティックを土台にし、型名は言語ごとの走査で Type にする。cmd は型なし（`%VAR%` / ラベルのみ）。Theme 色は tokyonight。Languages は Theme を参照しない。 |
| P13 | PowerShell ハイライトだけ `System.Management.Automation.dll`（GAC `v4.0_3.0.0.0__31bf3856ad364e35`）を `/r` する。`Parser.ParseInput` の AST を使う。実行ホスト（Runspace）はこの参照ではまだ作らない。F-PS-RUN はこの SMA で Runspace を開かない。 |

## フェーズ P7 で採用した提案

採用済み提案 P7（UTF-8 BOM）とは別。P7-A 着手。P16 / P17 は P7-A で実装する。P14 は波 C までコードを書かない。P19 の枠コメント実装は P7-B（今はコードを書かない）。

| ID | 内容 |
| --- | --- |
| P14 | 常時コンパイルの単位はワークスペース内すべての `.cs` を 1 単位。複数 Main は csc エラー。無題 C# は一時ファイルで含める。csproj はフェーズ P7 では作らない。実装は波 C |
| P16 | フェーズ P7 の着手は P2 完了の直後（デバッガ P3 より先）。波 C（F-LIVE / F-SQU / F-VBA-BLD）だけ残 P1 の F-CS-BLD / F-PROB を待つ |
| P17 | `{` 入力直後に `}` を入れ、キャレットを間へ。既に対がある／文字列・コメントでは入れない。`()` `[]` も同様でよい。間で Enter したら F-SIND で整形 |
| P19 | F-DOC のラベル綴りは `summary`（原文の `summery` は誤綴りとして直す）。文書の枠例は `summary` を使う。実装は P7-B |

## フェーズ P8 で採用した提案

[requirements.md](requirements.md) の **フェーズ P8**。下の **提案 P8**（F-CMP の P5 必須範囲）とは別。実装はフェーズ P8。今はコードを書かない。

| ID | 内容 |
| --- | --- |
| P21 | Markdown プレビューはオーナー描画のサブセット（同梱フォント）。インボックス WebBrowser は使わない |
| P22 | F-MACRO はキー記録／再生と、パレットと同じコマンド名を PowerShell 5.1 から呼ぶ薄い面。サクラの PPA / JScript / マクロファイル互換は W |

## 提案（確認待ち）

採用済み提案 P7（UTF-8 BOM）と、[requirements.md](requirements.md) の **フェーズ P7**（編集器インテリジェンス）は別物である。

[requirements.md](requirements.md) の **フェーズ P8**（Markdown／VBAProject 参照／マクロ）と、下表の **提案 P8**（F-CMP の P5 必須範囲）も別物である。混同しない。

P15 / P18 はフェーズ P7 向けの確認待ち。**提案 P8 は確定しない。** P20 / P23 / P24 / P25 は確認待ちであり、上の確定欄に入れない。P15 / P18 も確定しない。P19 / P21 / P22 は採用済み（D22）。P1 F-CS-BLD は実装既定として P15 の 6 DLL を使う。確定にはしない。

| ID | 提案 | 理由 | 代替 |
| --- | --- | --- | --- |
| P5 | Excel は **遅延バインディング**（`Excel.Application` / `VBIDE`） | M365 の PIA バージョン差を避ける | インストール済み PIA を `/r` |
| P8 | P5 の F-CMP はキーワードとオープン中ファイル（ファイル内）の識別子に限る。ワークスペースのシンボル名まで広げることは P5 実装時の必須にはしない（提案として広げてよい）。完全な型システム（オーバーロード解決、変換、definite assignment）は作らない。P12 の自前束縛に **定義位置** と **XML / 直前コメント** を足すことは、ハイライト・F-GD・F-HOV のための拡張であり、Roslyn 相当ではない。F-LSP は W のまま。 | Roslyn は外部かつ C# 6 世界。P12 はハイライト用束縛でありコンパイラではない | 外部 LSP（F-LSP は W のため不採用） |
| P9 | cmd の「デバッグ」は **エコー付き実行と失敗行の表示** | cmd に CLR デバッガが無い | 自前 `.bat` インタープリタ |
| P10 | `namingMode` の既定は `filename`。document モジュール（ThisWorkbook / シート）は常に Excel の名前を使う | 既存ブックとの衝突を減らす | 既定を `folder_prefix` |
| P15 | ユーザー csc の `/r` は当面、製品と同じ Framework セット（mscorlib, System, System.Core, System.Drawing, System.Windows.Forms, System.Xml）。`Microsoft.CSharp` は足さない。追加 `/r` は後で XML。P1 F-CS-BLD は実装既定としてこの 6 DLL を使う。確定にはしない | 製品 `/r` をフェーズ P7 で増やさない | ユーザー XML で任意 `/r`（今は作らない） |
| P18 | 常時 csc の `/target` は library。`/target:exe` でも `Process.Start` しない | 診断専用であり起動しないことを明示する | `/target:exe` でも起動しない（保安は同じ） |
| P20 | F-DOC のショートカットは Ctrl+Alt+D（パレットからも同コマンド）。Ctrl+K Ctrl+I はホバー、Ctrl+K Ctrl+D は将来の Format と衝突しやすい | D12（VS Code 風）の近傍で 1 キー | 別ショートカット（未決） |
| P23 | P8 実装時、開いている VBProject 参照の GUID を `vba-map.xml` に残す。**今は XML 要素を足さない** | ディスク `.bas` には参照を書けない | マップに残さず Excel 側だけ |
| P24 | パラメータヒントは別 ID F-SIG（C、フェーズ P7-B）。F-CMP の P5 受け入れには含めない。P5 必須ではない | F-CMP に含めると P5 受け入れが膨らむ | F-CMP に含める |
| P25 | VBA のライブ Compile は、マップ済みブックが **既に開いている** ときだけ。Excel を自動起動しない。未保存 VBA は既存同期規則でプッシュしてから Compile | 未起動 Excel の自動起動は COM 寿命と意図しないブック起動 | ライブでも Excel を起動する |

## まだ聞かないが後で決める

- UserForm（`.frm` / `.frx`）の扱い
- 複数ブック / 複数 VBA プロジェクト
- ログのローテーション
- メニュー「ファイルを開く」
- ドラッグ＆ドロップでファイルを開く
- 単一インスタンス（二重起動の集約）
- ユーザー csc の追加 `/r`（製品 `/r` とは別。当面は提案 P15）
- 常時コンパイルのデバウンス数値（実装時の定数でよい）
