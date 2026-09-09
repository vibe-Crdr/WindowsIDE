# IDE 機能

VS Code 相当を「全部一度に」ではなく、受け入れ条件付きで並べる。優先度は [requirements.md](requirements.md) のフェーズに合わせる。

凡例: M = 必須（P2 まで）。S = べき（P3–P9。デバッグは P3–P6、編集器インテリジェンスはフェーズ P7、Markdown／参照／マクロはフェーズ P8、VB.NET ホストはフェーズ P9。F-IG は P7-A に混ぜず P9 より先でよい）。C = できるとよい（フェーズ P7–P9 内の後回し可）。W = 初期はやらない。F-BR / F-SIND / F-AC / F-LIVE / F-SQU / F-GD / F-HOV / F-DOC / F-VBA-CASE / F-VBA-BLD / F-SIG / F-MD / F-VBA-REF / F-MACRO / F-IG / F-VB-BLD / F-VB-RUN / F-VB-LIVE / F-DBG-VB から新しい M は作らない。P1 F-IND スライスで Enter の前行先頭空白コピーと複数行 Tab/Shift+Tab を入れる。P1 F-FIND スライスでファイル内検索・置換を入れる。P1 F-CS-BLD / F-PROB スライスで手動 `csc` と問題一覧を入れる。P1 F-CS-RUN スライスで手動ビルド成功後の EXE 起動と出力パネルを入れる。P1 F-PS-RUN スライスでディスク上の `.ps1` を PowerShell 5.1 子プロセスで実行する。P1 F-CMD-RUN スライスでディスク上の `.cmd` / `.bat` ファイルと選択行を実行する。F-TERM と混ぜない。P1 F-TERM スライスで下パネルの統合ターミナル（ConPTY）を入れる。P2 F-VBA-SYNC スライスで R7 のプル/プッシュを入れる。

## P0 スライス

P0 は単一 WinExe のダークシェル。自前 `TextView`、左ツリー（開く・作成・リネーム・削除）、タブ、UTF-8 保存、EXE 同梱デュアルフォント。下パネル・ハイライト・実行・VBA は入れない。

| ID | P0 でやる | P0 でやらない |
| --- | --- | --- |
| F-EXP | フォルダを開く、ツリー表示、クリックで開く、起動引数で既存ファイルを開く、他プロセスからフォアグラウンドに戻ったとき再読込（`WM_ACTIVATEAPP`。展開・選択・スクロールは保持。自前ホバー窓では再読込しない）。OwnerDraw ツリーは MouseDown+GetNodeAt で開く。選択中ファイルは Enter でも開く。AfterSelect では開かない。ツリーは 12 DIP 双フォント。スプリッタで幅変更（セッション内）。バーは内容が収まるとき非表示。見切れたファイル名は横スクロールで末尾まで見える。タッチパッド縦横で中身とバーが動く。スクロールでファイル名が二重描画しない。ツリー上端の作成バー（線画アイコン＋ツールチップ（文字ラベルなし））からワークスペース内へファイル／フォルダを新規作成（ツリー内インライン。Enter 確定、Esc／空 Enter／他コントロールへフォーカスでキャンセル。同名上書きなし、作成後ファイルは開く／フォルダは展開して選択）。ファイル作成 Ctrl+Alt+N、フォルダ作成 Ctrl+Shift+N（ProcessCmdKey、ShortcutKeys なし、IME 中は奪わない。編集器／ターミナルからも可）。ワークスペース無しは作成バーと同じ MessageBox。Ctrl+N 無題は維持。ツリーへ Ctrl+Shift+E。リネーム（F2、選択済みラベルの遅延クリック、行内 DualFontField。Enter 確定、Esc／空／同一名／フォーカス離脱でキャンセル。根不可。同名上書きなし）。削除（ツリーフォーカス時 Delete、確認のうえごみ箱。根不可）。開いているタブはリネームでパス追従、削除で閉じる（dirty は既存保存確認。Cancel ならディスクも触らない）。選択行は FileTreeControl.ContainsFocus なら Theme.Selection、さもなくば Theme.CurrentLine。HideSelection=false。インライン DualFontField の枠は Theme.Selection。新しい Theme 色は足さない。ワークスペース未 Bind では左ペイン非表示。起動フォーカスは編集器。Ctrl+Shift+E はワークスペース無しで no-op | ウォッチ、ツリーからワークスペース外を開く、Shift+Delete 完全削除、コンテキストメニュー |
| F-TAB | 複数タブ、未保存印、閉じる（印はタブ矩形内）、切替でキャレット/スクロール復帰。溢れ時は横オフセットでスクロール。ホイール（縦も横も）でオフセット。選択変更で選択タブを可視へ寄せる | タブ並べ替え、ピン留め、シェブロン、タブバー上の ThemedScrollBar、タブ幅縮小、タブ一覧、Ctrl+Tab オーバーレイ |
| F-SAVE | 保存 / すべて保存 / 名前を付けて保存。保存ダイアログは Common Item Dialog | 自動保存 |
| F-ED | 挿入、選択、コピー/切取/貼付、Undo/Redo、無題ファイル。未確定は自前描画（システム変換窓なし、候補はシステム、変換中キャレット追従）。編集器へ Ctrl+1（ProcessCmdKey、ShortcutKeys なし、IME 中は奪わない。左ペイン・下パネルは畳まない。FindBar は閉じない。ワークスペース無しでも可） | マルチカーソル |
| F-LN | 行番号（ガター内右寄せ、左右 8 DIP）、現在行ハイライト。内容が収まるときは編集器バー非表示。全角の長い行でも横バーが出て末尾までスクロールできる。横スクロール時も本文・選択はガターへ描かない | ミニマップ、折り返し |
| F-IND | Tab でスペース挿入（既定幅 4）。Shift+Tab は行頭スペース削り | Enter 後の自動インデント（前行コピー）と複数行の Tab/Shift+Tab は残 P1 |
| F-SET | fontSize 既定 14 DIP を XML で変更可（UI 無し）。P0 受け入れ: 125% DPI で本文が物理 18px、240 DPI で 14 DIP → 35 物理 px、起動窓が DIP 換算で作業領域に収まる、About の閉じると注記が見切れない。既存 workspace.xml は書き換えない | 設定画面、`namingMode`（P2） |

P0 の非対象: 字句ハイライト、検索、問題一覧、csc/PS/cmd 実行、統合ターミナル、VBA 同期、デバッグ、コマンドパレット、クイックオープン、補完、Git、Vim、WPF、RichTextBox 着色、NuGet、下パネル、Excel COM。

## P1 F-HL スライス

P1 最初のスライスは字句ハイライト 4 言語（R3 / F-HL）。`WindowsIDE.Languages` を新設する。検索・実行・下パネルは入れない。

| ID | P1 F-HL でやる | P1 F-HL でやらない |
| --- | --- | --- |
| F-HL | 拡張子だけで C# / VBA / PowerShell / cmd / Plain を判定（無題・不明は Plain）。行開始状態 + 行スキャナで Keyword / String / Comment / Number / Text / Local / Instance / Method / Type を区別。C# は自前束縛オーバーレイ（ファイル内シンボル + ワークスペース型名 + BCL Reflection）。VBA / PowerShell は IdentifierClassifier に加え型名走査。cmd は IdentifierClassifier のみ（型なし）。Theme に Local `#7dcfff` / Instance `#2ac3de` / Method `#e0af68` / Type `#73daca` を足す。ステータス先頭に言語名。`HighlightSession` は Document に持ち、タブ切替で捨てない。キーワードはソース内静的表 | 検索、Ctrl+F/P、下パネル、問題一覧、csc/PS/cmd 実行、ターミナル、Enter 自動インデント、複数行一括インデント、補完、Roslyn、折りたたみ、括弧強調、RichTextBox 着色、Regex ホットパス、言語手動切替、workspace.xml 言語キー、FileKind 削除/言語化、メニュー「ファイルを開く」、D&D、VBA 同期、Excel COM |

C# 束縛の残り外れ（文書化）: 打ち途中の構文エラー区間、ユーザーコードの C# 6 以降、読み込んでいないアセンブリ、Excel 未起動の COM 型。cmd にクラスはない。VBA は文頭の識別子で次が文字列／識別子なら Method。（D30 実装までの現行）

括弧強調（F-BR）は本スライスでも残 P1 でもやらず、フェーズ P7-A へ送る。Enter 後の自動インデントは **P1 F-IND スライスで入れる**（直前行の先頭空白コピー、言語非依存）。言語対応スマートインデントは **F-SIND**（P7-A）であり F-IND と同居しない。

## P1 F-IND スライス

言語非依存のインデント残り。F-SIND と混ぜない。

| ID | P1 F-IND でやる | P1 F-IND でやらない |
| --- | --- | --- |
| F-IND | Enter で直前行の先頭空白（連続する `' '` と `'\t'`）をコピー。複数行選択の Tab/Shift+Tab。規則は `IndentRules`（`WindowsIDE.Editor`、WinForms 非依存） | F-SIND、F-FIND、下パネル、新色、新 XML |

## P1 F-FIND スライス

開いている文書のファイル内検索・置換。F-GSRCH と混ぜない。

| ID | P1 F-FIND でやる | P1 F-FIND でやらない |
| --- | --- | --- |
| F-FIND | リテラル検索・置換、大小無視、タブ直下の薄い FindBar（検索・置換欄は DualFont オーナー描画、本文と同じ IME）、Ctrl+F / Ctrl+H / F3 / Shift+F3 / Esc。規則は `FindRules`（`WindowsIDE.Editor`、WinForms 非依存）。走査は `IndexOf` / `LastIndexOf` + Ordinal / OrdinalIgnoreCase。現在ヒットは既存 Selection。クエリ・置換・ignoreCase・バー表示はセッション内 | Regex、ワイルドカード、単語単位、選択範囲内検索、複数行クエリ、全ヒット背景、下パネル、F-GSRCH、F-PAL、Ctrl+P、F-SIND、実行、VBA、新 Theme 色、新 XML、新 `/r`、FindDialog、RichTextBox |

## P1 F-CS-BLD / F-PROB スライス

指定 Framework `csc.exe` の手動ビルドと、下パネルの問題一覧。F-CS-RUN（ユーザー EXE 起動）と混ぜない。生成物は TEMP に残す。ビルドコマンドからは起動しない。

| ID | P1 F-CS-BLD / F-PROB でやる | P1 F-CS-BLD / F-PROB でやらない |
| --- | --- | --- |
| F-CS-BLD | メニュー「ビルド」と Ctrl+Shift+B。指定パスの Framework `csc.exe`（`/noconfig` はコマンドライン）。ワークスペースがあれば根の全 `.cs`（`bin` / `obj` / `.git` 除外）、無ければフォーカス中のディスク上 `.cs` 1 本。パス付き dirty は `Document.Save()` してから csc。診断を問題一覧へ。`/target:exe` の出力は `%TEMP%\WindowsIDE\build\manual\<key>\out.exe`。PDB 可 | ビルドコマンドからのユーザー EXE 起動／出力パネル、F-LIVE、F-TERM、F-PS-RUN、F-CMD-RUN、csproj、無題の一時ファイル、SMA / Microsoft.CSharp をユーザー csc へ、新製品 `/r` |
| F-PROB | 左右 split の外側に問題一覧。起動時は畳み、初回ビルド（合成診断を含む）で 180 DIP。error / warning / ファイル無し fatal と IDE 合成 1 件。クリックで `TryOpenFile` + `SelectRange`（csc 1 始まり → BufferPoint 0 始まり）。12 DIP DualFont、ThemedScrollBar 10 DIP、既存 Theme 色のみ | デバッグ / ターミナルの空タブ、ListView / DataGrid / RichTextBox、波線、F-LIVE / F-SQU / VBA Compile、新 Theme 色、新 XML、Esc で畳むこと（Esc は FindBar） |

## P1 F-CS-RUN スライス

手動ビルド成功後に TEMP の `out.exe` を別プロセス起動し、stdout/stderr を下パネルの出力へ出す。常時コンパイル生成物は起動しない（F-LIVE は作らない）。

| ID | P1 F-CS-RUN でやる | P1 F-CS-RUN でやらない |
| --- | --- | --- |
| F-CS-RUN | トップ「実行」の「デバッグなしで実行」（表示 Ctrl+F5。ShortcutKeys は付けず ProcessCmdKey。IME 変換中は奪わない）。毎回手動 csc と同じ経路。成功（ExitCode==0 かつ StartError 空）のときだけその回の `OutputExe` を `Host.Csharp` が起動。警告付き exit 0 は起動する。下パネルは問題と出力のみ。ビルド完了と csc 失敗は問題タブ、csc 成功して起動するときは出力を空にして出力タブ。stdout/stderr は出力パネル（4000 行キャップ） | F-PS-RUN / F-CMD-RUN / F-TERM、P7/P8、F5 デバッグ、ビルド成功時の自動起動、引数 UI、対話 stdin、新しい `/r`・Theme 色・XML、ListView / DataGrid / RichTextBox、空のデバッグ/ターミナルタブ、`.windows-ide/`、Excel COM、Host.PowerShell/Cmd、F-LIVE、`lastManualOutputExe` だけの起動近道、Build への起動、CscRunner 契約変更 |

## P1 F-PS-RUN スライス

ディスク上の `.ps1` を Windows PowerShell 5.1 の子プロセスで実行し、stdout/stderr/起動終了を既存の出力パネルへ出す。F-CMD-RUN / F-TERM / P7 / P8 と混ぜない。

| ID | P1 F-PS-RUN でやる | P1 F-PS-RUN でやらない |
| --- | --- | --- |
| F-PS-RUN | フォーカス中タブの拡張子で Ctrl+F5 を分岐。ディスク上 `.ps1` は `Environment.SpecialFolder.System` + `WindowsPowerShell\v1.0\powershell.exe` を `-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File` で起動（PATH / 対話 stdin は使わない）。cwd はその `.ps1` のディレクトリ。パス付き dirty はそのタブだけ `Document.Save()`。無題は一時ファイルを作らず拒否。開始前拒否は問題タブ、開始後は出力。C# ホストと同時に走らせない（保持 Process 参照だけ Kill。プロセス名検索はしない）。Ctrl+Shift+B はどのタブでも現行どおり C# csc | F-CMD-RUN / F-TERM、P7/P8、F5、引数 UI、対話 stdin、新しい `/r`・Theme・XML、ListView / DataGrid / RichTextBox、空ターミナルタブ、Excel COM、Host.Cmd、Runspace、Languages `ParseInput` 変更、CscRunner 契約変更、共通 ProcessHost 基底、`.psm1` / `.psd1` 実行、Set-ExecutionPolicy / Unblock-File |

## P1 F-CMD-RUN スライス

ディスク上の `.cmd` / `.bat` ファイルを OS 同梱 `cmd.exe` の子プロセスで実行し、stdout/stderr/起動終了を既存の出力パネルへ出す。選択行・F-TERM / P9 / 共通基底と混ぜない。

| ID | P1 F-CMD-RUN でやる | P1 F-CMD-RUN でやらない |
| --- | --- | --- |
| F-CMD-RUN | フォーカス中タブのディスク上 `.cmd` / `.bat` は `Environment.SpecialFolder.System` + `cmd.exe` を `/d /s /c` で起動（PATH / 対話 stdin は使わない）。cwd はそのファイルのディレクトリ。パス付き dirty はそのタブだけ `Document.Save()`。無題は一時ファイルを作らず拒否。開始前拒否は問題タブ、開始後は出力。C# / PowerShell ホストと同時に走らせない（保持 Process 参照だけ Kill。プロセス名検索はしない）。Ctrl+Shift+B はどのタブでも現行どおり C# csc | 選択行・F-TERM、`/k`、P9、F-DBG-CMD、共通 ProcessHost 基底、新 `/r`、F5、引数 UI、対話 stdin、新しい Theme・XML、ListView / DataGrid / RichTextBox、空ターミナルタブ、Excel COM、Runspace、CscRunner 契約変更、P7/P8 |

## P1 F-CMD-RUN 選択行スライス

ディスク上の `.cmd` / `.bat` の選択テキスト（無ければ現在行）を OS 同梱 `cmd.exe` で実行し、stdout/stderr/起動終了を既存出力パネルへ出す。Ctrl+F5 のファイル全体実行は変えない。F-TERM と混ぜない。

| ID | P1 F-CMD-RUN 選択行でやる | P1 F-CMD-RUN 選択行でやらない |
| --- | --- | --- |
| F-CMD-RUN | 実行メニュー「選択行を実行(&L)」（表示 F8。ShortcutKeys は付けず ProcessCmdKey。IME 変換中は奪わない）。ディスク上 `.cmd` / `.bat` のみ（FilePath 拡張子。LanguageKind では判定しない）。選択なしは現在行、同一行は文字どおり、複数行は `IndentRules.BlockLastLine`（終端列 0 は最終行除外）で CRLF 結合。空・空白のみと無題は拒否。所有 TEMP `%TEMP%\WindowsIDE\run\cmd\<guid>.cmd` を ACP・CRLF・BOM なしで書き既存 Start（`/d /s /c`）へ。cwd はソースのディレクトリ。本文に `@echo off` 等を足さない。`"%&^` はサニタイズしない。F8 では `Save` しない。起動行はソースフルパス（TEMP は出さない）。開始前拒否は問題タブ、開始後は出力。Ctrl+F5 はファイル全体のまま | F-TERM、`/k`、P9、F-DBG-CMD、F5、引数 UI、対話 stdin、本文を `/c` に連結、PATH の別 cmd、共通 ProcessHost 基底、C# / PS 選択を cmd として走らせること、無題の一時ファイル化、Excel COM、Runspace、新しい `/r` / Theme / XML、ListView / DataGrid / RichTextBox、P7/P8、Ctrl+F5 を選択時に奪うこと、F8 に ShortcutKeys を付けること |

## P1 F-TERM スライス

下パネルに実体のある統合ターミナル。既定は `SpecialFolder.System` の powershell.exe 5.1、切替で同梱 cmd.exe。ConPTY で対話する。1 ショット実行（Host.*）とは共存する。

| ID | P1 F-TERM でやる | P1 F-TERM でやらない |
| --- | --- | --- |
| F-TERM | 下パネル順は問題 / 出力 / ターミナル。CreatePseudoConsole + CreateProcessW（EXTENDED_STARTUPINFO_PRESENT。CREATE_NO_WINDOW は付けない。STARTF_USESTDHANDLES も付けない（付けると PowerShell がリダイレクト扱いになり PSReadLine 警告が出る）。レジストリ Blind Access Off のときだけ張り付き SPI_GETSCREENREADER をライブ解除。PTY 子へ TERM を渡さない）+ 匿名パイプ。既定 `SpecialFolder.System` + `WindowsPowerShell\v1.0\powershell.exe`（`-NoLogo -NoProfile -ExecutionPolicy Bypass`）。切替は同梱 `cmd.exe`（`/d` のみ）。cwd はワークスペース根、無ければユーザープロファイル。パイプは UTF-8。ReadFile は専用スレッド、UI へ BeginInvoke、世代で stale を捨てる。Kill は保持ハンドルと HPCON / パイプだけ。× はパネルを畳むだけで PTY は殺さない。Ctrl+` は ProcessCmdKey（ShortcutKeys なし。変換中は奪わない）。Ctrl+1 は編集器へフォーカスし下パネルは畳まない（ProcessCmdKey、ShortcutKeys なし、IME 中は奪わない）。ターミナルフォーカス中は編集器へ Ctrl+C/V/Z/Y/X/A を送らない。シェル自己終了はバッファを残し Enter で同種を再起動。VT は CR/LF/BS/HT/BEL 無視と CUP/CUU/CUD/CUF/CUB/CHA/EL/ED/SGR、ESC 7/8、CSI ?25。未知 CSI は最終バイトまで読んで捨てる。折り返しは PTY 列（最小 20×4）。IME は TerminalControl が IImeClient（未確定は自前、候補はシステム）。失敗はターミナル本文 1 行（Error 色）。1 本のみ。フォントは 12 DIP DualFont | 複数セッション、分割、検索、リンク、マウス追跡、alt screen、OSC、256/truecolor、DECSTBM、Sixel、vim 品質 VT、PSReadLine UI、引数 UI、Host.* 変更、共通 ProcessHost、Host.Cmd への `/k`、P9、F-DBG-*、空デバッグタブ、F5、P2 VBA、P7/P8、本文 fontSize 連動、workspace.xml 属性、新しい Theme 色、新しい `/r`、AllocConsole、黒いコンソール窓、SMA Runspace、pwsh、PATH / ComSpec でシェル exe を解決すること、GetProcessesByName、UI スレッドの同期 ReadFile / WaitForExit、1 ショット Kill 集合に PTY を足すこと |

## 残 P1

残作業なし。P1 完了。スマートインデント・括弧強調・自動閉じ・定義へ移動・ホバー・枠コメント・VBA キャピタライズ・常時コンパイル・波線・VBA Compile 診断は残 P1 に入れない。

## P2 F-VBA-SYNC スライス

R7 の VBA ディレクトリ ↔ Excel 明示プル／プッシュ。Compile / Run / References / UserForm は入れない。

| ID | P2 F-VBA-SYNC でやる | P2 F-VBA-SYNC でやらない |
| --- | --- | --- |
| F-VBA-SYNC | ディスク `vba/`（マップ root）の `.bas` / `.cls` とマクロ有効ブック（`.xlsm` / `.xlsb`）の明示プル／プッシュ。Excel 側にフォルダは作らない。両方の `namingMode`（正は vba-map）。VBA 既定 CP932。マップ `{workspace}/.windows-ide/vba-map.xml`。トップ `VBA(&A)`（実行と表示の間）。遅延バインディング（`Type.InvokeMember`）。IDE が起動した Excel を Quit しない | F-VBA-BLD / Compile、F-VBA-REF、F-DBG-VBA、`Application.Run`、UserForm（`.frm` / `.frx`）、Excel のみモジュールの削除コマンド、複数ブック、一般メニュー「ファイルを開く」、D&D、新 Theme 色、Office PIA `/r`、`Microsoft.CSharp.dll`、`dynamic`、Host.* / Terminal / Languages レキサ変更、`workspace.xml` に namingMode、プッシュ成功後の Workbook.Save、Excel の Quit/Close、GetProcessesByName、バックグラウンド COM、DoEvents、進捗ダイアログ、P7/P8 |

## 残 P2

残作業なし。Compile 等は残 P2 に入れない。

## F-IG スライス（インデント線）— 今は実装しない

言語非依存の編集器本文ガイド。P7-A に混ぜない。P9 より先でよい。F-IND / F-SIND と混ぜない。新しい M は作らない。

| ID | F-IG でやる | F-IG でやらない |
| --- | --- | --- |
| F-IG | 全ファイル形式（Plain / cmd / Markdown / 無題 / 全ホスト）で、先頭 `' '` と `'\t'` から `tabSize` 列ごとに 1 物理 px の縦線を本文へ描く。色は既存 LineNumber。常時オン。新 XML なし。規則は `WindowsIDE.Editor`（WinForms 非依存の計測 + `TextView` 描画）。`DpiUtil.TextBodyClip` 内。ガターへはみ出さない | F-SIND、F-IND の Enter/Tab 契約変更、Languages、新 Theme 色、アクティブガイド（P29）、XML トグル（P30）、空行継続の確定（P31）、U+3000 を数えること、列 0 の線、P7-A への混入 |

## フェーズ P7（編集器インテリジェンス）— P7-B 完了。P7-C 完了（F-LIVE / F-SQU / F-VBA-BLD 手動。VBA ライブは今は実装しない）。F-CMP 拡張と F-SIG は今は実装しない。

P3–P6（デバッガ / パレット）に押し込まない。P0–P2 の「最初の利用可能 IDE」を壊さない。着手時期は採用済み P16（P2 完了の直後。波 C の F-LIVE / F-SQU / F-VBA-BLD は残 P1 の F-CS-BLD / F-PROB 待ち）。**P7-A（波 A）は完了。P7-B は完了。P7-C は完了（F-LIVE / F-SQU / F-VBA-BLD 手動。VBA ライブは今は実装しない）。F-CMP の P7 拡張と F-SIG は今は実装しない。** 設定 XML の新規属性は今増やさない。F-CMP の P5 基本はフェーズ順では P7 の後（P5）に実装される。

| 波 | ID | 内容 | 依存 | 状態 |
| --- | --- | --- | --- | --- |
| A 構造 | F-BR、F-AC、F-SIND、F-VBA-CASE | 括弧強調、自動閉じ、スマートインデント、VBA キーワード大文字小文字 | Editor + Languages。Build 不要 | 完了 |
| B ナビ | F-GD、F-HOV、F-DOC、F-CMP の P7 拡張、F-SIG（C） | 定義へ移動、ホバー、枠コメント、ワークスペース／メンバー補完、パラメータヒント。対象は C# / VBA / PowerShell / cmd。VB.NET はフェーズ P9 | 位置付きシンボル。csc 不要 | F-GD / F-HOV / F-DOC 着手。F-CMP 拡張と F-SIG は今は実装しない |
| C 診断 | F-LIVE、F-SQU、F-VBA-BLD | 常時 csc、波線、VBA Compile 診断 | 残 P1 の F-CS-BLD / F-PROB の後。VBA はプッシュ後 | 完了（手動 Compile。ライブ VBA とブロック不一致波線は今は実装しない） |

| ID | P7 で対象 | 今の P1 および P7 でやらない |
| --- | --- | --- |
| F-BR | キャレット隣接の対括弧強調（文字列・コメント外）。直前と直後の両方を見る。両方括弧なら直前（左）優先。C#/PS は `()[]{}`。VBA は `()`。cmd は無し。対は Theme.Selection、不一致は既存 Error | 虹色ネスト。cmd。P1 スライスへの混入。新 Theme 色 |
| F-SIND | 前行 LeadingWhitespace をコピーしたうえで、言語規則のときだけ 1 段増減。+1 は末尾に tabSize 個のスペース、-1 は IndentRules.UnindentLine（タブをスペースへ変換しない）。`{|}` 間 Enter は `{` 行の prefix を W とし、中行 W+tabSize スペース、`}` は次行の W。C# `{}`、VBA ブロック、PS は C# に準じる。順は C# → VBA → PS | cmd。F-IND への同居（置き換え）。F-VBA-CASE と混ぜない。IndentRules へのスマート規則追加 |
| F-AC | C#/PS の対括弧（閉じ方は採用済み P17: `{` 直後に `}`、既対／文字列・コメントでは入れない、直後が既に対応閉じなら入れない。閉じ入力で直後が同じ閉じなら挿入せずキャレットだけ進める）。VBA はブロック開始の Enter で対応終端を 1 回。For / For Each は Next。1 行 If（Then より後ろにコメント以外のトークン）と Then 直後が継続 `_` だけのときは End If を入れない。Then 直後が空白とコメントだけ／行末ならブロック | VBA に `End For` を書く。既存終端があるときの二重挿入。1 行 If に End If。cmd |
| F-VBA-CASE | トークン境界のみ（空白・演算子・改行。IME 中はしない）。文字列・コメント外のキーワードを表の大文字小文字にする。表外識別子は触らない。Rem 行はコメントのまま（行頭 rem→Rem は可）。インデントは変えない | 識別子の宣言合わせを P7 必須にすること。VBE 全整形。F-SIND と混ぜる。キー 1 文字ごとの書き換え |
| F-GD | F12 でユーザーソース上の定義へ | Peek、Find All References、BCL / cmd へ F12 |
| F-HOV | 現行ホスト（C# / VBA / PowerShell / cmd。cmd 含む）。解決できたユーザー定義はシグネチャ常時。説明は定義直前の F-DOC 枠または従来コメント。VB.NET はフェーズ P9 | Object Browser、COM HelpString、無い XML をエラーにする。ホバー内クリックで定義へ |
| F-DOC | ショートカット 1 つで言語の枠コメントを定義直前に 1 回入れる | 既存枠の二重挿入。C# の `///` `<summary>` を生成すること。キーを確定すること（提案 P20 は確認待ち） |
| F-CMP | P7 でワークスペースのユーザーシンボルと `.` 後のメンバー候補（P12 束縛／ヒューリスティック）。VBA は D30 実装後にその構造を使う。今はヒューリスティックのまま。 | Roslyn。オーバーロード解決・変換・definite assignment。Excel 未起動の COM 型。P5 必須の拡大（提案 P8 は未確定） |
| F-SIG | （C）呼び出し中の粗い引数リスト | P5 必須にすること。完全な型システム |
| F-LIVE | デバウンスした Framework `csc.exe` の診断のみ。生成物は起動しない。C# タブの入力停止後だけ live csc。PS は ParseInput。VBA ライブは今は実装しない | EXE 自動起動、キー入力ごとの同期 csc、C# 以外への csc、ホスト Kill、UI スレッドでの WaitForExit |
| F-SQU | C# は csc、PS は `ParseInput`、VBA は手動 F-VBA-BLD の位置。error のみ。既存 Error 色。新色なし | 自前パーサをコンパイラ診断と偽る。cmd 波線。warning 波線。ブロック不一致波線（今は実装しない） |
| F-VBA-BLD | メニュー「コンパイル」からの手動。プッシュ後の Excel Compile 失敗を問題一覧と波線へ。Run しない。ライブ VBA は今は実装しない | `Application.Run`、MakeCompiledFile、ダミー Run、キーごとの Compile、Excel 未起動のライブ自動起動、バックグラウンドスレッドの COM |
| F-IND | （P7 の対象外）P1 F-IND スライス | スマートインデントを F-IND に足すこと |
| F-LSP | 変えない（W） | 外部 LSP / Roslyn を上げること |
| F-CS-BLD | 手動ビルドのまま | 常時コンパイルと同一コマンドにすること |

層: 構造は行レキサの TokenKind（Regex ホットパスと Roslyn 禁止。描画/挿入は Editor、規則は Languages。Languages をコンパイラと呼ばない）。ナビは P12 束縛（`CSharpSemantic.Collect`）に定義位置とシグネチャを足す（完全型システムは作らない。BCL は F12 しない）。診断の正は言語別（C# は指定 `csc.exe`＝Build。VBA は Excel Compile＝`WindowsIDE.Vba`。PS は `ParseInput`＝Languages。EXE 起動は Host.Csharp）。VBA の知能の正は D30（今は実装しない）。現行は行レキサ + `VbaSemantic` / `CollectVba`。診断の正は Excel Compile のまま。構造ヒント（D31）は F-VBA-BLD と並べて偽らない。

### 常時コンパイル（F-LIVE）

入力停止後にデバウンス（MainForm の定数。XML に書かない）。前回の **csc プロセスだけ** を Kill（csharpHost / powershellHost / cmdHost / PTY は Kill しない）。UI スレッドで csc を待たない。一時出力へ書き、診断後に削除する。`Process.Start` しない。C# 以外に csc しない。手動ビルドと常時が競合したら **新しい方** で問題一覧を置き換える。コンパイル単位は採用済み P14（ワークスペース内すべての `.cs`。無題 C# は一時ファイル）。ユーザー `/r` は提案 P15（確定しない）。live rsp は `/target:library`（debug スイッチ無し）。起動しない。

PS の波線は `Parser.ParseInput` の構文エラー位置で可（Runspace は開始しない）。VBA のコンパイラ診断の波線は F-VBA-BLD（Excel Compile）の位置。ディスク上の粗いブロック不一致を波線にするのは C で、UI 上は「構造ヒント」としコンパイラと並べて偽らない（D31。問題一覧に出すなら別バケット／別文言「構造」。プッシュはパーサ失敗で拒否しない。今は実装しない）。cmd は波線なし（W）。

### VBA Compile 診断（F-VBA-BLD）

診断の正は Excel VBA コンパイラ。`WindowsIDE.Vba`。`Build`（csc）にも `Host.Csharp` にも置かない。ディスクを先にプッシュしてから Compile。`Application.Run` しない。`MakeCompiledFile` やダミー Run で代替しない。自前レキサで埋めない。

| 経路 | 動き |
| --- | --- |
| 手動（S） | コマンド「VBA をコンパイル」。Excel 未起動なら起動してよい（プル／プッシュと同じ）。保存確認 → プッシュ → Compile |
| ライブ（C、提案 P25 は確認待ち。今は実装しない） | マップ済みブックが既に開いているときだけ。Excel を自動起動しない。デバウンス後にプッシュ（既存の Excel 未保存確認）→ Compile。キー入力ごと禁止。今は実装しない |

COM は STA / UI。VBIDE に診断リストを返す `Compile()` は無い、と書いてよい。ベストエフォート:

1. プッシュ後、対象 `VBProject` を取る（マップのブック）。
2. `Application.VBE.CommandBars` から Compile（通例 Control Id **578**、キャプション依存にしない）を `FindControl`。
3. `Enabled = False` なら直近は成功扱い（問題一覧の VBA 診断をクリア）。
4. `Enabled = True` なら `Execute`。成功後に再び Enabled を見る。
5. 失敗時: `ActiveCodePane.GetSelection` と `CodeModule.Parent.Name` を `vba-map` でディスクパスへ。GetSelection は CodeModule 行。マップ後、ディスクに先頭ヘッダがあればその行数を足してディスク行にする。本文のみなら GetSelection の行をそのまま。列はそのまま。読めなければ未変換。メッセージは COM から取れなければ「VBA のコンパイルに失敗した」でよい。VBE のモーダルに IDE の MessageBox を重ねない。
6. FindControl 失敗・VBE 未初期化: 問題一覧に **取得失敗** を 1 件。VbaLexer の推測エラーで埋めない。VBE をちら見せしてリトライは C。

### 定義へ移動（F-GD）とホバー（F-HOV）

F-HOV は S。P7-B の対象は C# / VBA / PowerShell / cmd。cmd を対象外にしない。VB.NET はフェーズ P9。F12（F-GD）の cmd 対象外は維持。コメントの有無は F12 に影響しない。ホバー表示順: シグネチャ（ユーザー定義が解決できたとき常時）→ 定義直前の F-DOC 枠 → 従来の連続コメント → C# BCL は Framework XML（無ければ出さない。エラーにしない）。C# 宣言は `CSharpSemantic.Collect`（D26。コンストラクタとクラスは別。`.` の左が既知の型名ならそのメンバー。開いているバッファをディスクより優先。新規フルパーサは作らない）。VBA の構造の正は D30（今は実装しない。現行はディスク木の行スキャン）。

| 言語 | F12（F-GD） | ホバー（F-HOV） |
| --- | --- | --- |
| C# | ファイル内 → ワークスペース `.cs`。BCL へは入らない | シグネチャ常時。F-DOC 枠。連続 `///` の `<summary>` をプレーンテキスト（生成はしないが認識する）。BCL は DLL 隣の Framework XML（無ければ出さない、エラーにしない） |
| VB.NET | ワークスペース `.vb`。BCL へは入らない。フェーズ P9 | シグネチャ常時。F-DOC 枠。直前 `'` / `'''` 認識（生成しない）。フェーズ P9 |
| VBA | ディスク木の Sub / Function / Property（Excel 平坦名ではない） | シグネチャ常時。F-DOC 枠。直前の連続 `'` / `Rem`。COM HelpString は W |
| PS | ファイル内 `function` とファイル内変数（`param` / 代入左辺 / `foreach` イテレータ）。照合は `$` なし名前・OrdinalIgnoreCase。`$script:x` 等は名前 `x`。複数は先頭。ピッカーなし。ドットソース先・自動変数・`$env:` 等ドライブ・splat・`Set-Variable` は初期対象外。スコープに忠実な解決はしない。 | シグネチャ常時。F-DOC 枠。直前 `#`（S） |
| cmd | 対象外 | F-DOC 枠。直前の連続 `rem` / `::`（cmd にユーザー関数宣言は無いのでシグネチャは出さない） |

キーは D12 どおり F12。Peek / 参照検索は W。Object Browser は W。Markdown の見出し直前ホバーはフェーズ P8。

### 枠コメント（F-DOC）

P7-B の対象は C# / VBA / PowerShell / cmd。VB.NET の枠はフェーズ P9（VBA 例をコピーして流用しない。今は実装しない）。ショートカットは 1 つ（提案 P20 Ctrl+Alt+D は確認待ち。P7-B ではメニューと ProcessCmdKey。F-PAL は今は実装しない）＋将来は F-PAL から同コマンド。既存の F-DOC 枠が定義の直前にあれば二重挿入しない。シグネチャが取れれば `args` / `returns` を埋める（取れなければ空）。cmd に関数が無ければ空。C# の `///` `<summary>` は生成しない（ホバー認識はする）。変換もしない。

ラベル綴りは採用済み P19（`summary`）。文書の枠例も `summary`。

```text
VBA     '------------------------
        'summary :
        'args    :
        'returns :
        '------------------------

C#      //------------------------
        //summary :
        //args    :
        //returns :
        //------------------------

PS      #------------------------
        #summary :
        #args    :
        #returns :
        #------------------------

cmd     rem ------------------------
        rem summary :
        rem args    :
        rem returns :
        rem ------------------------
```

Markdown への同じコマンドはフェーズ P8。VB.NET の `'` 枠はフェーズ P9（VBA 例をコピーして流用しない。今は実装しない）。

### VBA キャピタライズ（F-VBA-CASE）

`VbaKeywords` の表記を正とするトークン単位。**トークン境界のみ**（空白・演算子・改行。IME 変換中はしない。キー 1 文字ごとには書き換えない）。表に無い識別子は触らない。文字列・コメントは触らない。`Rem` 行はコメントのまま（行頭 `rem`→`Rem` は可。本文は触らない）。F-SIND（インデント）と混ぜない。宣言に合わせた識別子ケース合わせは S で後でも可（P7 必須にしない）。VBE 全整形は W。

### 括弧強調（F-BR）と自動閉じ（F-AC）

C# / PS は `()[]{}`。VBA は `()`（`[]` は C）。cmd は対象外。文字列・コメント外のみ。新色は足さない。対は Selection、不一致は既存エラー `#f7768e`。虹色は W。

**隣接:** キャレット直前の文字、無ければ直後。両方括弧なら直前（左）優先。その側が文字列・コメントなら無視して反対側を見る。cmd は常に無し。

**C#/PS 自動閉じ（P17）:** 開き `{` `(` `[` 入力時。String/Comment では入れない。直後がすでに対応閉じなら closer を入れない。opener だけを挿入したと仮定して既存の閉じと組めるなら入れない。それ以外は opener+closer を入れてキャレットを間へ。選択置換は 1 つの compound。閉じ `)` `]` `}` 入力で直後が同じ閉じなら挿入せずキャレットだけ進める（String/Comment・選択中を除く）。

VBA の終端は実際の語を使う（**`End For` は禁止**）。既存の対応終端があるときは二重挿入しない。1 行 If（Then より後ろにコメント以外のトークンがある）には End If を入れない。Then 直後が空白とコメントだけ、または行末ならブロック If。Then のあとが継続 `_` だけのときは End If を入れない。

| 開始 | 挿入する終端 |
| --- | --- |
| Sub | End Sub |
| Function | End Function |
| Property | End Property |
| If … Then（ブロック） | End If |
| For / For Each | **Next** |
| Do | **Loop** |
| While | **Wend** |
| With | End With |
| Select | End Select |
| Enum | End Enum |
| Type | End Type |

### フェーズ P7 の非対象

外部 LSP、Roslyn、NuGet、別 csc、C# 6+、pwsh、常時コンパイルによる EXE 自動起動、Excel の自動 `Application.Run`、Compile と Run の混同、キー入力ごとの同期 Excel Compile、Excel 未起動時のライブ Compile による自動起動、バックグラウンドスレッドの Excel COM、`MakeCompiledFile` / ダミー Run による Compile 代替、自前レキサを VBA コンパイラと呼ぶこと、COM HelpString、Object Browser、虹色括弧、Peek、Find All References、cmd の F12 / 波線 / 自動閉じ / スマートインデント、BCL へ F12、自前パーサを csc 診断と偽ること、D30 の VBA パーサ本体を今書くこと、D31 のパーサ本体を今書くこと、今の P1 への混入、設定 XML の新規属性、製品への新しい `/r`、Markdown／VBAProject 参照／マクロ（それらはフェーズ P8）、VB.NET ホスト（フェーズ P9。今は実装しない）、インデント線（F-IG。P7-A に混ぜない）。STA の診断 Compile は対象外にしない。

## フェーズ P3（PowerShell デバッグ）

ディスク上の `.ps1` を IDE 同一プロセスの Runspace + SMA Debugger でデバッグする。F-PS-RUN（Ctrl+F5 の子プロセス）は変えない。Host.PowerShell と Languages は Runspace を開かない。PTY はデバッグ対象にも Kill 集合にも入れない。新しい M は作らない。

| ID | P3 でやる | P3 でやらない |
| --- | --- | --- |
| F-DBG-PS | 行ブレーク（ストアは OrdinalIgnoreCase パス + 1 始まり行。二重なし。XML に書かない）。F5 は Idle+ディスク `.ps1` で開始（dirty ならそのタブだけ Save）、Stopped で続行（言語不問）、Running は no-op。Idle の F5 を 1 ショットにしない。Shift+F5 停止、F10 StepOver、F11 StepInto。Invoke はディスクパス（本文 AddScript は使わない）。cwd はスクリプトディレクトリ。`InitialSessionState.CreateDefault`、`ExecutionPolicy.Bypass` は ISS プロパティのみ。Apartment は MTA 既定。Excel COM は触らない。停止中 `ProcessCommand` の `Get-Variable`（name + ToString。null は `$null`。先頭 256 文字。ネスト展開しない）。ストリームはデバッグコンソールだけ（出力タブに混ぜない）。開始時にストアを Debugger へ載せる | Set-PSBreakpoint フォールバック、本文 AddScript、Languages の ParseInput 変更、Languages / Host.PowerShell への Runspace、Host.PowerShell 編集、Ctrl+F5 の子プロセス経路、PTY の Kill、pwsh、条件 BP、ステップアウト、ウォッチ、オブジェクト展開、F-DBG-CMD / F-DBG-VBA / F-DBG-VB、ヘルパー EXE 隔離（提案 P35。今は実装しない） |
| F-DBG-UI | 下パネル 4 タブ（問題 / 出力 / ターミナル / デバッグ）。デバッグは上ローカル・下コンソール（縦 SplitContainer はセッション内。XML に書かない）。12 DIP DualFont、ThemedScrollBar 10 DIP。コンソールは OutputPanelControl 再利用（4000 行、先頭 4096 文字。stdout=Foreground、stderr=Error、起動終了警告=Comment）。ShowDebug はフォーカスを奪わない。F5 開始でパネル展開＋デバッグチップ。× は畳むだけ（セッションも PTY も殺さない）。表示メニュー「デバッグ」（ShortcutKeys なし、表示文字列なし）。実行メニュー先頭: 開始/続行(F5)、停止(Shift+F5)、ステップ オーバー(F10)、ステップ イン(F11)、セパレータ、既存 2 項。CreateDisplayCommand。**F10 に ShortcutKeys を付けない。** ProcessCmdKey は F5 / Shift+F5 / F9 / F10 / F11（IME composing では奪わない。ターミナルフォーカス中もグローバル）。ガター左に 12 DIP のブレーク列（既存 min 36 DIP の左）。印は行中央の楕円、Theme.Error（新色なし。OnPaint 中だけ AntiAlias）。クリックは列内かつ `.ps1` / `.cs` ならその行をトグル。F9 は `.ps1` / `.cs` ならキャレット行トグル、それ以外は黙って消費。停止行は TryOpenFile + キャレット（CurrentLine）。TextView は Debug/SMA を参照しない（BP 行は 0 始まり int[]、トグルはイベント） | 空のデバッグタブ、ListView / DataGrid / RichTextBox、新 Theme 色、workspace.xml 属性、専用色（提案 P36。今は Error）、P4-B–P9、F-IG、F-PAL、F-CMP |

デバッグ中（Idle 以外）: Ctrl+F5 / F8 / 1 ショット開始は ShowSynthetic で拒否しセッションは殺さない。PS デバッグ中の Ctrl+Shift+B は可（C# デバッグ中の拒否は P4-A）。live タイマーはセッション中 Stop、Idle 復帰後に再開可。開始時の排他は StartPs に合わせる（`buildGeneration++`、`cscRunner.Kill`、`InvalidateLiveCsc`、csharp/ps/cmd ホスト Kill。**PTY は殺さない。** 別 `debugGeneration`。`activeRunKind` に流用しない）。停止/Dispose は Runspace を閉じ、UI で Wait しない。PTY は閉じない。Idle の F5 拒否文言は P4-A（無題／`.ps1`／`.cs`／その他）。ユーザースクリプトは同一プロセスのため IDE を落とせる（隠さない。提案 P35 は確認待ち）。

## フェーズ P4（C# デバッグ）

P4-A は F-DBG-CS。ディスク上 `.cs` を手動 csc と同じ単位（`/target:exe /debug+`）でビルドし、成功時だけ TEMP の `out.exe` を ICorDebug.CreateProcess で別プロセスデバッグする。Ctrl+F5 は従来どおり出力タブ + `Host.Csharp`。P4-B は F-DBG-CMD（今は実装しない）。Host.Csharp の起動ロジックは変えない。live csc 成果物は起動しない。ユーザー EXE を IDE に Load しない。TextView は `WindowsIDE.Debug` を参照しない。新しい Theme 色・XML・製品 `/r` は足さない。F10 に ShortcutKeys を付けない。F10 はソース行 StepRange（同一行の nop / プロログはデバッガが続けて踏む）。ステップ準備に失敗したら Continue しない。F11 はユーザーメソッドへ入る。JMC は使わないので Framework メソッドへ入ることがある。

| ID | P4-A でやる | P4-A でやらない |
| --- | --- | --- |
| F-DBG-CS | 手動と同じ csc（`CscArgumentBuilder` / TEMP `build\manual\<key>\out.exe` と隣 PDB。第3の debug ディレクトリは作らない）。成功かつ exe+pdb なら `CorDebugSession.Start`（exe パス + cwd + generation）。失敗は問題タブ・セッション無し。pdb 無しは合成診断。行 BP は CreateProcess 前に PDB を開き token+IL を計算。LoadModule では ISym を開かず `GetFunctionFromToken` + IL `CreateBreakpoint`。native `GetILToNativeMapping` はフォールバック。ストアの `.cs` だけ ICorDebug に載せる（`.ps1` は載せない）。ユーザーモジュールは exe フルパス OrdinalIgnoreCase（`out.exe`）。フルパス不一致でもファイル名 `out.exe` 同士なら載せる。BCL には BP を載せない。専用 MTA スレッド。UI で Initialize/CreateProcess/Wait しない。コールバックから COM を UI に渡さない。`dwCreationFlags` は CREATE_NO_WINDOW のみ（DEBUG_ONLY_THIS_PROCESS / DEBUG_PROCESS は付けない。managed-only。Win32 フラグは unmanaged callback が必要）。アタッチ禁止。パイプ。stdout/stderr はデバッグコンソール（kind 0/1、ACP）。stdin 閉じる。エントリで Stopped にしない。Breakpoint / StepComplete / 未処理 second-chance だけ Stopped。Locals はプリミティブ値、string 先頭 256、null は `"null"`、その他は型名。FuncEval 禁止。スタック上限 32（メソッド名 + パス + 1 始まり行）。Shift+F5 は保持プロセスだけ Terminate（GetProcessesByName 禁止） | ISymWrapper / ClrMD / Roslyn / NuGet / 第三者 DLL、新しい製品 `/r`、Host.Csharp 編集、live 成果物の起動、ユーザー EXE の IDE Load、F-DBG-CMD / F-IG / P5 / P8 / P9 / F-DBG-VB / Excel / D30 パーサ、条件 BP、ウォッチ、FuncEval、例外 UI、アタッチ |
| F-DBG-CMD | （P4-B。今は実装しない） | P4-A に混ぜない |
| F-DBG-UI | ガター / F9 は `.ps1` と `.cs`。ストアはパス別に共存。停止時にフレームがあればローカル上ペイン先頭へ Comment「コールスタック」とフレーム行。PS は Frames 空なので見た目維持。ListView 禁止。新色なし | 新 Theme、F10 の ShortcutKeys |

PS と CS は同時セッション禁止。Stopped の F5 は止まっているエンジンへ Continue。Idle F5: 無題「無題はデバッグできない。」／ディスク `.ps1` は P3／ディスク `.cs` は pendingDebugAfterBuild + `StartManualBuild`／その他「C# のデバッグはディスク上の .cs、PowerShell はディスク上の .ps1 だけです。」CS デバッグ中（Idle 以外、または pendingDebugAfterBuild）の Ctrl+Shift+B は拒否。PS デバッグ中のビルドは現行どおり可。デバッグ中 Ctrl+F5/F8 は現行「デバッグ中は実行できない。」

## フェーズ P8（Markdown／参照／マクロ）— 今は実装しない

P5（F-PAL）と P7 の後。P3–P6 デバッガには押し込まない。**フェーズ番号の P8 は、提案 P8（F-CMP の P5 必須範囲）とは別である。** 新しい M は作らない。設定 XML の新規属性は今増やさない（参照 GUID の `vba-map` は提案 P23。P8 実装時。今は要素を足さない）。

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-MD | S | `.md` の字句色分けとプレビュー | `.md` が字句色分けされ、サブセットがプレビューできる。実行されない |
| F-VBA-REF | S | 開いている VBProject の参照 | 開いている VBProject の参照を一覧・追加・削除できる。`.bas` には書かない |
| F-MACRO | S | キー記録／再生と PS 5.1 の薄いコマンド面 | キー記録を再生でき、同じコマンド名を PS 5.1 から呼べる。サクラファイルは動かない。F-PAL の後 |

### Markdown（F-MD）

ホスト言語にしない（D4）。ホスト字句は P9 で VB.NET を足す。Markdown は R14 で R3 に入れない。拡張子 `.md` のみ（P11）。`LanguageKind.Markdown`。行スキャナ（Regex 禁止）。実行・デバッグ・csc / vbc・F-LIVE しない。見出しは既存 Keyword 色、コードフェンスは String。新しいテーマ色は足さない。プレビューはオーナー描画サブセット（採用済み P21。同梱フォント。WebBrowser は使わない）。見出し・段落・リスト・インライン強調・フェンス。CommonMark 完全は W。生 HTML の実行はしない。F-CMP の Markdown はファイル内見出し・リンク先程度。F-HOV は見出し直前でも可。F-DOC の同じコマンドを足してよい。

### VBAProject 参照（F-VBA-REF）

Excel が開きマップ済みブックがあるとき、`VBProject.References` の一覧・追加・削除。明示操作だけ。ディスク `.bas` に参照は書けない。ビルトイン参照（VBA / Excel）は削除しない。任意の COM DLL / タイプライブラリを足すと、そのコードが Excel プロセスに載る。GUID の vba-map 保存は提案 P23（確認待ち。今は要素を足さない）。

### マクロ（F-MACRO）

キー操作の記録と再生（コマンド ID＋文字）。F-PAL と同じコマンド名を PowerShell 5.1 から呼ぶ固定の薄い面（例: `Invoke-WindowsIdeCommand 'Name'`）。採用済み P22。サクラのマクロファイル互換・PPA・JScript・Python・任意 IDE オブジェクトモデルは W。F-EXT ではない。再生は UI/STA。名前空間は `WindowsIDE.Macro`（拡張ホストではない）。

### フェーズ P8 の非対象

CommonMark 完全、NuGet Markdown パーサ、新しいテーマ色、Markdown の実行・デバッグ・csc、Markdown を D4 ホストに足すこと、サクラ PPA / JScript / マクロファイル互換、拡張ホスト（F-EXT）、今の `workspace.xml` 新規属性、製品への新しい `/r`。

## フェーズ P9（VB.NET ホスト）— 今は実装しない

P7-A / P8 に押し込まない。新しい M は作らない。設定 XML の新規属性は今増やさない。製品 `/r` は増やさない。`.vb` ≠ `.bas` / `.cls`。`.vbs` は Plain のまま。表示名は `"VB.NET"`。enum は将来 `LanguageKind.VbNet`（`Vb` / `VisualBasic` は使わない）。

着手順の目安（コードを書くとき）: F-HL → F-VB-BLD / F-PROB 再利用 → F-VB-RUN → F-BR / F-SIND / F-AC → F-VB-LIVE / F-SQU。デバッグ（F-DBG-VB）は P9 受け入れに含めない（提案 P32）。

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-HL | S | `.vb` の字句色分け | 拡張子 `.vb` が `LanguageKind.VbNet` になり、ステータスが `VB.NET`。行スキャナ。`.vbs` は Plain |
| F-VB-BLD | S | 指定 `vbc.exe` の手動ビルド | ディスク上 `.vb` フォーカスで Ctrl+Shift+B が vbc。診断が問題一覧。csc に `.vb` を渡さない |
| F-VB-RUN | S | vbc 成功後の EXE 起動 | TEMP の EXE が別プロセスで走り、stdout/stderr が出力パネル。Excel しない。1 ショット排他に含める |
| F-BR | S | `()[]{}` | C#/PS に合わせる。VBA の `()` のみに寄せない。`<>` XML は C |
| F-SIND | S | VB.NET 専用スマートインデント | Languages の専用規則。`VbaBlockRules` を流用しない |
| F-AC | S | ブロック終端 | VB.NET 語の終端を 1 回。`End For` 禁止。F-VBA-CASE は使わない。VB.NET 用 CASE は今作らない（W） |
| F-DOC | S | `'` 枠 | VBA に似ても VB.NET 専用例。`'''` XML 生成はしない |
| F-GD / F-HOV / F-CMP | S | ナビと補完 | ワークスペース `.vb`。BCL へ F12 しない。P5 実装は今触らない |
| F-VB-LIVE / F-SQU | S | 常時 vbc と波線 | F-LIVE（csc）に足さない。自前レキサを vbc と偽らない |

### フェーズ P9 の非対象

製品を VB.NET で書き直すこと、製品ビルドを vbc にすること、指定外 vbc、`.vbproj`、`.vbs` を VB.NET にすること、`.bas` / `.cls` を vbc に渡すこと、Excel へ `.vb` を載せること、F-VBA-CASE / Excel Compile の流用、F-LIVE を vbc 対応に拡張すること、`WindowsIDE.Vba` に vbc を置くこと、`Host.VisualBasic` という名前、製品 `/r` への `Microsoft.VisualBasic.dll`、P7-A / P8 への混入、今の `workspace.xml` 新規属性、P9 受け入れへのデバッグ必須化。

## ワークベンチ

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-EXP | M | フォルダを開く、ツリー、開く/新規作成（線画アイコン＋ツールチップ（文字ラベルなし））/リネーム/削除 | ワークスペース配下が表示され、外部変更は他プロセスからフォアグラウンドに戻ったとき更新（`WM_ACTIVATEAPP`。自前ホバーでは再読込しない）。再読込後も展開・選択・スクロールが残る。OwnerDraw ツリーは MouseDown+GetNodeAt で開く。選択中ファイルは Enter でも開く。AfterSelect では開かない。バーは内容が収まるとき非表示。見切れた名前は横スクロールで末尾まで見える。タッチパッド縦横で中身とバーが動く。スクロールでファイル名が二重描画しない。新規作成は選択フォルダまたはファイルの親または根へ。名前はツリー内インライン（Enter 確定、Esc／空 Enter／他コントロールへフォーカスでキャンセル）。同名は上書きしない。作成したファイルは開く。作成したフォルダは展開して選択。ファイル作成 Ctrl+Alt+N、フォルダ作成 Ctrl+Shift+N（ProcessCmdKey、ShortcutKeys なし、IME 中は奪わない。編集器／ターミナルからも可）。ワークスペース無しは作成バーと同じ MessageBox。Ctrl+N 無題は維持。ツリーへ Ctrl+Shift+E。編集器へ Ctrl+1（左ペイン・下パネルは畳まない。IME 中は奪わない。ShortcutKeys なし。FindBar は閉じない。ワークスペース無しでも可）。リネームは F2 と選択済みラベルの遅延クリック（行内 DualFontField。Enter 確定、Esc／空／同一名／フォーカス離脱でキャンセル。根不可。同名上書きなし）。削除はツリーフォーカス時 Delete（確認のうえごみ箱。根不可。Shift+Delete 完全削除はしない）。開いているタブはリネームでパス追従、削除で閉じる（dirty は既存保存確認。Cancel ならディスクも触らない）。選択行は FileTreeControl.ContainsFocus なら Theme.Selection、さもなくば Theme.CurrentLine。HideSelection=false。インライン DualFontField の枠は Theme.Selection。新しい Theme 色は足さない。ワークスペース未 Bind では左ペイン非表示。起動フォーカスは編集器。Ctrl+Shift+E はワークスペース無しで no-op |
| F-TAB | M | 複数タブ、未保存印、閉じる | タブ切替でキャレットとスクロールが戻る。閉じる印はタブ矩形内に収まる。溢れ時は横オフセットでスクロール。ホイール（縦も横も）でオフセット。選択変更で選択タブを可視へ寄せる |
| F-SAVE | M | 保存、すべて保存、名前を付けて保存 | エンコーディング規則どおり書き込む。保存ダイアログは Common Item Dialog |
| F-PAL | S | コマンドパレット | コマンド名で絞り込み実行 |
| F-QO | S | クイックオープン | ワークスペース内ファイル名で開く |
| F-SET | M | 設定 UI または XML 編集 | フォントサイズと VBA `namingMode` を切替できる。半角フォントは Cascadia Mono 固定。P2 の namingMode は vba-map + VBA メニュー。設定画面なし |

## 編集器

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-ED | M | 挿入、選択、コピー、切取、貼付、Undo/Redo | 1 万行クラスの C# ファイルで入力が実用。未確定は自前、システム変換窓なし、候補はシステム、変換中キャレット追従。編集器へ Ctrl+1（ProcessCmdKey、ShortcutKeys なし、IME 中は奪わない。左ペイン・下パネルは畳まない。FindBar は閉じない。ワークスペース無しでも可） |
| F-LN | M | 行番号、現在行 | 行番号はガター内右寄せ、左右 8 DIP。内容が収まるときは編集器バー非表示。全角の長い行でも横バーが出て末尾までスクロールできる。横スクロール時も本文・選択はガターへ描かない |
| F-FIND | M | ファイル内検索・置換 | 大小無視オプション |
| F-IND | M | Tab/Shift+Tab、Enter で直前行の先頭空白をコピー | Enter で新行が直前行の先頭空白をコピーする（変換しない。空行の上は見ない）。言語非依存。タブ幅は既存 `tabSize`。複数行選択の Tab は対象行頭へ `tabSize` 個のスペース、Shift+Tab は行頭のタブ 1 個または最大 `tabSize` 個のスペースを削る。スマートインデントは F-SIND |
| F-IG | S | インデント線（本文の縦ガイド） | すべてのファイル形式で、`tabSize` 列ごとの縦線が本文に見える。色は LineNumber。常時オン。ガターへはみ出さない。今は実装しない。P7-A に混ぜない |
| F-HL | M | 字句ハイライト（P1 は 4 言語。P9 で VB.NET） | キーワード・文字列・コメントに加え、ローカル / メンバー / メソッド / 型が区別できる。名前空間色はしない。C# は自前束縛。Roslyn は使わない。VB.NET はフェーズ P9（今は実装しない） |
| F-BR | S | 対応括弧の強調 | キャレット隣接の括弧と対が、文字列・コメント外で強調される。フェーズ P7-A。VB.NET は P9 で `()[]{}`（VBA の `()` のみに寄せない） |
| F-SIND | S | 言語対応スマートインデント | 言語規則で 1 段増減する（C# `{}`、VBA ブロック、PS は C# に準じる）。順は C# → VBA → PS。cmd 対象外。フェーズ P7-A。VB.NET は P9 の専用規則（`VbaBlockRules` 流用禁止） |
| F-AC | S | 自動閉じ | C#/PS は対括弧が条件付きで入り、閉じ入力で直後が同じ閉じなら挿入せずキャレットだけ進む。VBA はブロック開始の Enter で対応終端が 1 回だけ入る。フェーズ P7-A。閉じ方は採用済み P17（`{` 入力直後に `}`。閉じスキップを含む） |
| F-VBA-CASE | S | VBA キーワードの大文字小文字 | 文字列・コメント外のキーワードが表の大文字小文字になり、インデントは変わらない。フェーズ P7-A |
| F-DOC | S | 枠コメント挿入 | ショートカット 1 つで、言語の枠コメントが定義直前に 1 回入り、既存枠のときは増えない。フェーズ P7-B は C# / VBA / PowerShell / cmd。VB.NET はフェーズ P9 |
| F-MC | C | マルチカーソル | |
| F-MM | W | ミニマップ | |
| F-VIM | W | Vim モーダル | D12 により初期対象外 |

## 言語と実行

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-CS-BLD | M | `csc.exe` で手動ビルド | エラー行が問題一覧に出る。常時コンパイルは F-LIVE（フェーズ P7） |
| F-CS-RUN | M | ビルド成功後に EXE 起動 | stdout/stderr が出力パネル。常時コンパイルの生成物は起動しない |
| F-VB-BLD | S | 指定 `vbc.exe` で手動ビルド | ディスク上 `.vb` のとき Ctrl+Shift+B が vbc になり、エラー行が問題一覧に出る。csc に `.vb` を渡さない。フェーズ P9。今は実装しない |
| F-VB-RUN | S | vbc 成功後に EXE 起動 | stdout/stderr が出力パネル。Excel しない。フェーズ P9。今は実装しない |
| F-VB-LIVE | S | VB.NET の常時診断（vbc） | 入力停止後にデバウンスした指定 `vbc.exe` が走り、失敗が問題一覧と波線に出る。生成物は起動されない。F-LIVE（csc）に足さない。フェーズ P9。今は実装しない |
| F-PS-RUN | M | `.ps1` を PowerShell 5.1 で実行 | `$PSVersionTable.PSVersion.Major -eq 5` |
| F-CMD-RUN | M | `.cmd` / `.bat` / 選択行を cmd で実行 | ディスク上の `.cmd` / `.bat` を OS 同梱 `cmd.exe` で実行し、stdout/stderr/起動終了が出力パネルに出る。選択テキスト（無ければ現在行）も同じ `cmd.exe` で実行し、stdout/stderr/起動終了が出力パネルに出る。Ctrl+F5 はファイル全体。F8 は選択行。 |
| F-TERM | M | 統合ターミナル | 下パネルにターミナルがあり、既定は System32 の powershell.exe 5.1（`-NoLogo -NoProfile -ExecutionPolicy Bypass`）、メニューで cmd.exe（`/d`）に切替できる。ConPTY でプロンプト・Read-Host・`dir /p` が対話できる。Ctrl+` で表示／フォーカス（ターミナルフォーカス中なら畳む）。Ctrl+1 は編集器へ戻し下パネルは畳まない（ProcessCmdKey、ShortcutKeys なし、IME 中は奪わない）。1 ショット実行と共存する |
| F-VBA-SYNC | M | VBA ディレクトリ ↔ Excel プッシュ / プル | ディスクの `vba/` で `.bas` / `.cls` を管理し、マクロ有効ブックと明示のプル／プッシュができる。Excel 側にフォルダは作らない。両方の namingMode。VBA 既定 CP932 |
| F-PROB | M | 問題一覧 | クリックでファイル+行。残 P1 の受け入れは手動 `csc` の診断を問題一覧に出すこと。将来の常時 csc（F-LIVE、フェーズ P7）と VBA Compile（F-VBA-BLD、フェーズ P7）も同じ診断モデル。VBA Compile の受け入れは F-VBA-BLD。競合したら新しい方で置き換える |
| F-LIVE | S | 常時コンパイル（診断のみ） | 入力停止後にデバウンスした Framework `csc.exe` が走り、失敗が問題一覧と波線に出る。生成物は起動されない。フェーズ P7-C。csc 専用（VBA Compile は F-VBA-BLD。VB.NET は F-VB-LIVE） |
| F-SQU | S | 構文エラーの波線 | C# は csc 診断位置、PS は `ParseInput` エラー位置、VBA は F-VBA-BLD の位置に、エラー色の波線が付く。cmd は波線なし。フェーズ P7-C |
| F-VBA-BLD | S | VBA Compile 診断 | プッシュ後の Excel Compile 失敗が問題一覧と波線に出る。Run されない。Excel 未起動のライブは走らない。フェーズ P7-C |

## デバッグ

| ID | 優先 | 言語 | 内容 | 受け入れ |
| --- | --- | --- | --- | --- |
| F-DBG-PS | S | PowerShell | 行ブレーク、ステップ、ローカル変数 | P3。ディスク上 `.ps1` を同一プロセス Runspace + SMA Debugger で、行 BP・F5 開始/続行・Shift+F5 停止・F10 StepOver・F11 StepInto・停止時ローカル（Get-Variable）ができる |
| F-DBG-CS | S | C# | PDB + ステップ、コールスタック、ローカル | P4-A。ディスク `.cs` を手動 csc と同じ単位でビルドし、成功時だけ TEMP `out.exe` を ICorDebug.CreateProcess。ICorDebug 等インボックス API のみ。製品 `/r` なし |
| F-DBG-VB | S | VB.NET | PDB + ステップ、コールスタック、ローカル | 独立 ID。F-DBG-CS に相乗りしない。P9 の受け入れに含めない。時期は P4 の後（提案 P32） |
| F-DBG-CMD | S | cmd | エコー実行、失敗行 | P4-B。今は実装しない。本格ステップは必須にしない |
| F-DBG-VBA | S | VBA | マクロ指定実行、COM エラー表示。可能なら VBE 連携 | P6 |
| F-DBG-UI | S | 共通 | ブレークガター、続行/停止、デバッグコンソール | P3 最小 + P4-A ガター `.ps1` / `.cs`。ガター左 12 DIP の Error 楕円、F9 トグル、下パネル実体デバッグタブ（ローカル + コンソール）。空タブは置かない。新色なし |

実行（デバッグなし）は M。ステップ実行は S。

## 補完とナビ

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-CMP | S | キーワードと開いているファイルの識別子 | P5。全ホスト（P9 以降は VB.NET を含む。今の P5 実装は触らない）。キーワード静的表＋開いているファイルの識別子。cmd はキーワード、`%VAR%` / `!VAR!`、ファイル内ラベル。メンバーリストなし。ワークスペースのシンボル名は P5 必須ではない（提案 P8）。P7 でワークスペース／メンバー（S）。Roslyn ではない |
| F-GD | S | 定義へ移動 | F12 でユーザーソース上の定義へジャンプする。BCL / cmd へは入らない。Peek / 参照検索は W。フェーズ P7-B は C# / VBA / PowerShell。VB.NET はフェーズ P9 |
| F-HOV | S | ホバー / クイックインフォ | C# / VBA / PowerShell / cmd（cmd 含む）で、解決できた定義のシグネチャが常時出る。説明は F-DOC 枠または従来コメント。フェーズ P7-B。VB.NET はフェーズ P9 |
| F-SIG | C | パラメータヒント | 呼び出し中に粗の引数リストが出る。P5 必須ではない。フェーズ P7-B |
| F-LSP | W | 外部 LSP / Roslyn | 変えない。初期はやらない |

## 検索（ワークスペース）

| ID | 優先 | 内容 |
| --- | --- | --- |
| F-GSRCH | S | フォルダ内テキスト検索 |

## 明示的にやらない（初期）

| ID | 内容 | 理由 |
| --- | --- | --- |
| F-GIT | SCM ビュー | 要件に無い。OS の git は別 |
| F-EXT | 拡張マーケット | 外部コード導入になる |
| F-AI | 製品内チャット | ホスト言語外・ネット前提になりやすい |

フェーズ P7 の非対象は「フェーズ P7（編集器インテリジェンス）」節に列挙する。フェーズ P8 の非対象は「フェーズ P8」節に列挙する。F-LSP は W のまま。
