# IDE 機能

VS Code 相当を「全部一度に」ではなく、受け入れ条件付きで並べる。優先度は [requirements.md](requirements.md) のフェーズに合わせる。

凡例: M = 必須（P2 まで）。S = べき（P3–P8。デバッグは P3–P6、編集器インテリジェンスはフェーズ P7、Markdown／参照／マクロはフェーズ P8）。C = できるとよい（フェーズ P7–P8 内の後回し可）。W = 初期はやらない。F-BR / F-SIND / F-AC / F-LIVE / F-SQU / F-GD / F-HOV / F-DOC / F-VBA-CASE / F-VBA-BLD / F-SIG / F-MD / F-VBA-REF / F-MACRO から新しい M は作らない。残 P1 に残す M のインデントは F-IND の前行先頭空白コピーだけ。

## P0 スライス

P0 は単一 WinExe のダークシェル。自前 `TextView`、左ツリー（開くだけ）、タブ、UTF-8 保存、EXE 同梱デュアルフォント。下パネル・ハイライト・実行・VBA は入れない。

| ID | P0 でやる | P0 でやらない |
| --- | --- | --- |
| F-EXP | フォルダを開く、ツリー表示、クリックで開く、起動引数で既存ファイルを開く、フォーカス復帰で再読込（展開・選択・スクロールは保持）。OwnerDraw ツリーは MouseDown+GetNodeAt で開く。ツリーは 12 DIP 双フォント。スプリッタで幅変更（セッション内）。バーは内容が収まるとき非表示。見切れたファイル名は横スクロールで末尾まで見える。タッチパッド縦横で中身とバーが動く。スクロールでファイル名が二重描画しない | リネーム、削除、ウォッチ、ツリーからワークスペース外を開く |
| F-TAB | 複数タブ、未保存印、閉じる（印はタブ矩形内）、切替でキャレット/スクロール復帰 | タブ並べ替え、ピン留め |
| F-SAVE | 保存 / すべて保存 / 名前を付けて保存。保存ダイアログは Common Item Dialog | 自動保存 |
| F-ED | 挿入、選択、コピー/切取/貼付、Undo/Redo、無題ファイル。未確定は自前描画（システム変換窓なし、候補はシステム、変換中キャレット追従） | マルチカーソル |
| F-LN | 行番号（ガター内右寄せ、左右 8 DIP）、現在行ハイライト。内容が収まるときは編集器バー非表示。全角の長い行でも横バーが出て末尾までスクロールできる。横スクロール時も本文・選択はガターへ描かない | ミニマップ、折り返し |
| F-IND | Tab でスペース挿入（既定幅 4）。Shift+Tab は行頭スペース削り | Enter 後の自動インデント（前行コピー）と複数行の Tab/Shift+Tab は残 P1 |
| F-SET | fontSize 既定 14 DIP を XML で変更可（UI 無し）。P0 受け入れ: 125% DPI で本文が物理 18px、240 DPI で 14 DIP → 35 物理 px、起動窓が DIP 換算で作業領域に収まる、About の閉じると注記が見切れない。既存 workspace.xml は書き換えない | 設定画面、`namingMode`（P2） |

P0 の非対象: 字句ハイライト、検索、問題一覧、csc/PS/cmd 実行、統合ターミナル、VBA 同期、デバッグ、コマンドパレット、クイックオープン、補完、Git、Vim、WPF、RichTextBox 着色、NuGet、下パネル、Excel COM。

## P1 F-HL スライス

P1 最初のスライスは字句ハイライト 4 言語（R3 / F-HL）。`WindowsIDE.Languages` を新設する。検索・実行・下パネルは入れない。

| ID | P1 F-HL でやる | P1 F-HL でやらない |
| --- | --- | --- |
| F-HL | 拡張子だけで C# / VBA / PowerShell / cmd / Plain を判定（無題・不明は Plain）。行開始状態 + 行スキャナで Keyword / String / Comment / Number / Text / Local / Instance / Method / Type を区別。C# は自前束縛オーバーレイ（ファイル内シンボル + ワークスペース型名 + BCL Reflection）。VBA / PowerShell は IdentifierClassifier に加え型名走査。cmd は IdentifierClassifier のみ（型なし）。Theme に Local `#7dcfff` / Instance `#2ac3de` / Method `#e0af68` / Type `#73daca` を足す。ステータス先頭に言語名。`HighlightSession` は Document に持ち、タブ切替で捨てない。キーワードはソース内静的表 | 検索、Ctrl+F/P、下パネル、問題一覧、csc/PS/cmd 実行、ターミナル、Enter 自動インデント、複数行一括インデント、補完、Roslyn、折りたたみ、括弧強調、RichTextBox 着色、Regex ホットパス、言語手動切替、workspace.xml 言語キー、FileKind 削除/言語化、メニュー「ファイルを開く」、D&D、VBA 同期、Excel COM |

C# 束縛の残り外れ（文書化）: 打ち途中の構文エラー区間、ユーザーコードの C# 6 以降、読み込んでいないアセンブリ、Excel 未起動の COM 型。cmd にクラスはない。VBA は文頭の識別子で次が文字列／識別子なら Method。

括弧強調（F-BR）は本スライスでも残 P1 でもやらず、フェーズ P7-A へ送る。Enter 後の自動インデントは残 P1 の **F-IND**（直前行の先頭空白コピー、言語非依存）に残す。言語対応スマートインデントは **F-SIND**（P7-A）であり F-IND と同居しない。

## 残 P1

字句ハイライト以外の P1（検索、手動 `csc`、問題一覧、PS/cmd 実行、統合ターミナル）は既存の M のまま。Enter 後インデントは F-IND の前行コピーのみ。複数行の Tab/Shift+Tab も残 P1 の F-IND（スマートインデントと混ぜない）。スマートインデント・括弧強調・自動閉じ・定義へ移動・ホバー・枠コメント・VBA キャピタライズ・常時コンパイル・波線・VBA Compile 診断は残 P1 に入れない。

## フェーズ P7（編集器インテリジェンス）— 今は実装しない

P3–P6（デバッガ / パレット）に押し込まない。P0–P2 の「最初の利用可能 IDE」を壊さない。着手時期は採用済み P16（P2 完了の直後。波 C の F-LIVE / F-SQU / F-VBA-BLD は残 P1 の F-CS-BLD / F-PROB 待ち）。設定 XML の新規属性は今増やさない。F-CMP の P5 基本はフェーズ順では P7 の後（P5）に実装される。

| 波 | ID | 内容 | 依存 |
| --- | --- | --- | --- |
| A 構造 | F-BR、F-AC、F-SIND、F-VBA-CASE | 括弧強調、自動閉じ、スマートインデント、VBA キーワード大文字小文字 | Editor + Languages。Build 不要 |
| B ナビ | F-GD、F-HOV、F-DOC、F-CMP の P7 拡張、F-SIG（C） | 定義へ移動、ホバー、枠コメント、ワークスペース／メンバー補完、パラメータヒント | 位置付きシンボル。csc 不要 |
| C 診断 | F-LIVE、F-SQU、F-VBA-BLD | 常時 csc、波線、VBA Compile 診断 | 残 P1 の F-CS-BLD / F-PROB の後。VBA はプッシュ後 |

| ID | P7 で対象 | 今の P1 および P7 でやらない |
| --- | --- | --- |
| F-BR | キャレット隣接の対括弧強調（文字列・コメント外） | 虹色ネスト。cmd。P1 スライスへの混入 |
| F-SIND | C# `{}`、VBA ブロック、PS は C# に準じる 1 段増減 | cmd。F-IND への同居。F-VBA-CASE と混ぜない |
| F-AC | C#/PS の対括弧（閉じ方は採用済み P17）。VBA はブロック開始の Enter で対応終端を 1 回 | VBA に `End For` を書く。既存終端があるときの二重挿入。1 行 If に End If。cmd |
| F-VBA-CASE | 文字列・コメント外のキーワードを表の大文字小文字にする。インデントは変えない | 識別子の宣言合わせを P7 必須にすること。VBE 全整形。F-SIND と混ぜる |
| F-GD | F12 でユーザーソース上の定義へ | Peek、Find All References、BCL / cmd へ F12 |
| F-HOV | 全ホスト（cmd 含む）。定義直前の F-DOC 枠または従来コメント | Object Browser、COM HelpString、無い XML をエラーにする |
| F-DOC | ショートカット 1 つで言語の枠コメントを定義直前に 1 回入れる | 既存枠の二重挿入。C# の `///` `<summary>` を生成すること。キーを確定すること（提案 P20 は確認待ち） |
| F-CMP | P7 でワークスペースのユーザーシンボルと `.` 後のメンバー候補（P12 束縛／ヒューリスティック） | Roslyn。オーバーロード解決・変換・definite assignment。Excel 未起動の COM 型。P5 必須の拡大（提案 P8 は未確定） |
| F-SIG | （C）呼び出し中の粗い引数リスト | P5 必須にすること。完全な型システム |
| F-LIVE | デバウンスした Framework `csc.exe` の診断のみ。生成物は起動しない | EXE 自動起動、キー入力ごとの同期 csc、C# 以外への csc |
| F-SQU | C# は csc、PS は `ParseInput`、VBA は F-VBA-BLD の位置 | 自前パーサをコンパイラ診断と偽る。cmd 波線 |
| F-VBA-BLD | プッシュ後の Excel Compile 失敗を問題一覧と波線へ。Run しない | `Application.Run`、MakeCompiledFile、ダミー Run、キーごとの Compile、Excel 未起動のライブ自動起動、バックグラウンドスレッドの COM |
| F-IND | （P7 の対象外）残 P1 の前行コピー | スマートインデントを F-IND に足すこと |
| F-LSP | 変えない（W） | 外部 LSP / Roslyn を上げること |
| F-CS-BLD | 手動ビルドのまま | 常時コンパイルと同一コマンドにすること |

層: 構造は行レキサの TokenKind（Regex ホットパスと Roslyn 禁止。描画/挿入は Editor、規則は Languages。Languages をコンパイラと呼ばない）。ナビは P12 束縛に定義位置を足す（完全型システムは作らない。BCL は F12 しない）。診断の正は言語別（C# は指定 `csc.exe`＝Build。VBA は Excel Compile＝`WindowsIDE.Vba`。PS は `ParseInput`＝Languages。EXE 起動は Host.Csharp）。

### 常時コンパイル（F-LIVE）

入力停止後にデバウンス（数値は実装時定数。未決）。前回の **csc プロセスだけ** を Kill。UI スレッドで csc を待たない。一時出力へ書き、診断後に削除する。`Process.Start` しない。C# 以外に csc しない。手動ビルドと常時が競合したら **新しい方** で問題一覧を置き換える。コンパイル単位は採用済み P14（ワークスペース内すべての `.cs`）。ユーザー `/r` は提案 P15。`/target` は提案 P18。

PS の波線は `Parser.ParseInput` の構文エラー位置で可（Runspace は開始しない）。VBA の波線は F-VBA-BLD（Excel Compile）の位置。ディスク上の粗いブロック不一致を波線にするのは C で、UI 上は「構造ヒント」としコンパイラと並べて偽らない。cmd は波線なし（W）。

### VBA Compile 診断（F-VBA-BLD）

診断の正は Excel VBA コンパイラ。`WindowsIDE.Vba`。`Build`（csc）にも `Host.Csharp` にも置かない。ディスクを先にプッシュしてから Compile。`Application.Run` しない。`MakeCompiledFile` やダミー Run で代替しない。自前レキサで埋めない。

| 経路 | 動き |
| --- | --- |
| 手動（S） | コマンド「VBA をコンパイル」。Excel 未起動なら起動してよい（プル／プッシュと同じ）。保存確認 → プッシュ → Compile |
| ライブ（C、提案 P25 は確認待ち） | マップ済みブックが既に開いているときだけ。Excel を自動起動しない。デバウンス後にプッシュ（既存の Excel 未保存確認）→ Compile。キー入力ごと禁止 |

COM は STA / UI。VBIDE に診断リストを返す `Compile()` は無い、と書いてよい。ベストエフォート:

1. プッシュ後、対象 `VBProject` を取る（マップのブック）。
2. `Application.VBE.CommandBars` から Compile（通例 Control Id **578**、キャプション依存にしない）を `FindControl`。
3. `Enabled = False` なら直近は成功扱い（問題一覧の VBA 診断をクリア）。
4. `Enabled = True` なら `Execute`。成功後に再び Enabled を見る。
5. 失敗時: `ActiveCodePane.GetSelection` と `CodeModule.Parent.Name` を `vba-map` でディスクパスへ。メッセージは COM から取れなければ「VBA のコンパイルに失敗した」でよい。VBE のモーダルに IDE の MessageBox を重ねない。
6. FindControl 失敗・VBE 未初期化: 問題一覧に **取得失敗** を 1 件。VbaLexer の推測エラーで埋めない。VBE をちら見せしてリトライは C。

### 定義へ移動（F-GD）とホバー（F-HOV）

F-HOV は S。全ホスト。cmd を対象外にしない。F12（F-GD）の cmd 対象外は維持。表示順: 定義直前の F-DOC 枠 → 従来の連続コメント → C# BCL は Framework XML（無ければ出さない）。

| 言語 | F12（F-GD） | ホバー（F-HOV） |
| --- | --- | --- |
| C# | ファイル内 → ワークスペース `.cs`。BCL へは入らない | F-DOC 枠。連続 `///` の `<summary>` をプレーンテキスト（生成はしないが認識する）。BCL は DLL 隣の Framework XML（無ければ出さない、エラーにしない） |
| VBA | ディスク木の Sub / Function / Property（Excel 平坦名ではない） | F-DOC 枠。直前の連続 `'` / `Rem`。COM HelpString は W |
| PS | ファイル内 `function`。ドットソース先は初期対象外 | F-DOC 枠。直前 `#`（S） |
| cmd | 対象外 | F-DOC 枠。直前の連続 `rem` / `::` |

キーは D12 どおり F12。Peek / 参照検索は W。Object Browser は W。Markdown の見出し直前ホバーはフェーズ P8。

### 枠コメント（F-DOC）

全ホスト言語。ショートカットは 1 つ（提案 P20 Ctrl+Alt+D は確認待ち）＋ F-PAL から同コマンド。既存の F-DOC 枠が定義の直前にあれば二重挿入しない。シグネチャが取れれば `args` / `returns` を埋める（取れなければ空）。cmd に関数が無ければ空。C# の `///` `<summary>` は生成しない（ホバー認識はする）。変換もしない。

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

Markdown への同じコマンドはフェーズ P8。

### VBA キャピタライズ（F-VBA-CASE）

`VbaKeywords` の表記を正とするトークン単位。トークンを抜けたとき（空白・演算子・改行）。文字列・コメントは触らない。`Rem` 行はコメントのまま。F-SIND（インデント）と混ぜない。宣言に合わせた識別子ケース合わせは S で後でも可（P7 必須にしない）。VBE 全整形は W。

### 括弧強調（F-BR）と自動閉じ（F-AC）

C# / PS は `()[]{}`。VBA は `()`（`[]` は C）。cmd は対象外。文字列・コメント外のみ。新色は足さない。対は Selection、不一致は既存エラー `#f7768e`。虹色は W。

VBA の終端は実際の語を使う（**`End For` は禁止**）。既存の対応終端があるときは二重挿入しない。1 行 If には End If を入れない。

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

外部 LSP、Roslyn、NuGet、別 csc、C# 6+、pwsh、常時コンパイルによる EXE 自動起動、Excel の自動 `Application.Run`、Compile と Run の混同、キー入力ごとの同期 Excel Compile、Excel 未起動時のライブ Compile による自動起動、バックグラウンドスレッドの Excel COM、`MakeCompiledFile` / ダミー Run による Compile 代替、自前レキサを VBA コンパイラと呼ぶこと、COM HelpString、Object Browser、虹色括弧、Peek、Find All References、cmd の F12 / 波線 / 自動閉じ / スマートインデント、BCL へ F12、自前パーサを csc 診断と偽ること、今の P1 への混入、設定 XML の新規属性、製品への新しい `/r`、Markdown／VBAProject 参照／マクロ（それらはフェーズ P8）。STA の診断 Compile は対象外にしない。

## フェーズ P8（Markdown／参照／マクロ）— 今は実装しない

P5（F-PAL）と P7 の後。P3–P6 デバッガには押し込まない。**フェーズ番号の P8 は、提案 P8（F-CMP の P5 必須範囲）とは別である。** 新しい M は作らない。設定 XML の新規属性は今増やさない（参照 GUID の `vba-map` は提案 P23。P8 実装時。今は要素を足さない）。

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-MD | S | `.md` の字句色分けとプレビュー | `.md` が字句色分けされ、サブセットがプレビューできる。実行されない |
| F-VBA-REF | S | 開いている VBProject の参照 | 開いている VBProject の参照を一覧・追加・削除できる。`.bas` には書かない |
| F-MACRO | S | キー記録／再生と PS 5.1 の薄いコマンド面 | キー記録を再生でき、同じコマンド名を PS 5.1 から呼べる。サクラファイルは動かない。F-PAL の後 |

### Markdown（F-MD）

ホスト言語にしない（D4）。R3 は 4 言語のまま。要件は新 R14。拡張子 `.md` のみ（P11）。`LanguageKind.Markdown`。行スキャナ（Regex 禁止）。実行・デバッグ・csc・F-LIVE しない。見出しは既存 Keyword 色、コードフェンスは String。新しいテーマ色は足さない。プレビューはオーナー描画サブセット（採用済み P21。同梱フォント。WebBrowser は使わない）。見出し・段落・リスト・インライン強調・フェンス。CommonMark 完全は W。生 HTML の実行はしない。F-CMP の Markdown はファイル内見出し・リンク先程度。F-HOV は見出し直前でも可。F-DOC の同じコマンドを足してよい。

### VBAProject 参照（F-VBA-REF）

Excel が開きマップ済みブックがあるとき、`VBProject.References` の一覧・追加・削除。明示操作だけ。ディスク `.bas` に参照は書けない。ビルトイン参照（VBA / Excel）は削除しない。任意の COM DLL / タイプライブラリを足すと、そのコードが Excel プロセスに載る。GUID の vba-map 保存は提案 P23（確認待ち。今は要素を足さない）。

### マクロ（F-MACRO）

キー操作の記録と再生（コマンド ID＋文字）。F-PAL と同じコマンド名を PowerShell 5.1 から呼ぶ固定の薄い面（例: `Invoke-WindowsIdeCommand 'Name'`）。採用済み P22。サクラのマクロファイル互換・PPA・JScript・Python・任意 IDE オブジェクトモデルは W。F-EXT ではない。再生は UI/STA。名前空間は `WindowsIDE.Macro`（拡張ホストではない）。

### フェーズ P8 の非対象

CommonMark 完全、NuGet Markdown パーサ、新しいテーマ色、Markdown の実行・デバッグ・csc、Markdown を D4 ホストに足すこと、サクラ PPA / JScript / マクロファイル互換、拡張ホスト（F-EXT）、今の `workspace.xml` 新規属性、製品への新しい `/r`。

## ワークベンチ

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-EXP | M | フォルダを開く、ツリー、開く/リネーム/削除 | ワークスペース配下が表示され、外部変更はフォーカス復帰で更新。再読込後も展開・選択・スクロールが残る。OwnerDraw ツリーは MouseDown+GetNodeAt で開く。バーは内容が収まるとき非表示。見切れた名前は横スクロールで末尾まで見える。タッチパッド縦横で中身とバーが動く。スクロールでファイル名が二重描画しない |
| F-TAB | M | 複数タブ、未保存印、閉じる | タブ切替でキャレットとスクロールが戻る。閉じる印はタブ矩形内に収まる |
| F-SAVE | M | 保存、すべて保存、名前を付けて保存 | エンコーディング規則どおり書き込む。保存ダイアログは Common Item Dialog |
| F-PAL | S | コマンドパレット | コマンド名で絞り込み実行 |
| F-QO | S | クイックオープン | ワークスペース内ファイル名で開く |
| F-SET | M | 設定 UI または XML 編集 | フォントサイズと VBA `namingMode` を切替できる。半角フォントは Cascadia Mono 固定 |

## 編集器

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-ED | M | 挿入、選択、コピー、切取、貼付、Undo/Redo | 1 万行クラスの C# ファイルで入力が実用。未確定は自前、システム変換窓なし、候補はシステム、変換中キャレット追従 |
| F-LN | M | 行番号、現在行 | 行番号はガター内右寄せ、左右 8 DIP。内容が収まるときは編集器バー非表示。全角の長い行でも横バーが出て末尾までスクロールできる。横スクロール時も本文・選択はガターへ描かない |
| F-FIND | M | ファイル内検索・置換 | 大小無視オプション |
| F-IND | M | Tab/Shift+Tab、Enter で直前行の先頭空白をコピー | Enter で新行が直前行の先頭空白をコピーする。言語非依存。タブ幅は既存 `tabSize`。複数行の Tab/Shift+Tab も残 P1。スマートインデントは F-SIND |
| F-HL | M | 字句ハイライト 4 言語 | キーワード・文字列・コメントに加え、ローカル / メンバー / メソッド / 型が区別できる。名前空間色はしない。C# は自前束縛。Roslyn は使わない |
| F-BR | S | 対応括弧の強調 | キャレット隣接の括弧と対が、文字列・コメント外で強調される。フェーズ P7-A |
| F-SIND | S | 言語対応スマートインデント | 言語規則で 1 段増減する（C# `{}`、VBA ブロック、PS は C# に準じる）。順は C# → VBA → PS。cmd 対象外。フェーズ P7-A |
| F-AC | S | 自動閉じ | C#/PS は対括弧が条件付きで入り、VBA はブロック開始の Enter で対応終端が 1 回だけ入る。フェーズ P7-A。閉じ方は採用済み P17（`{` 入力直後に `}`） |
| F-VBA-CASE | S | VBA キーワードの大文字小文字 | 文字列・コメント外のキーワードが表の大文字小文字になり、インデントは変わらない。フェーズ P7-A |
| F-DOC | S | 枠コメント挿入 | ショートカット 1 つで、言語の枠コメントが定義直前に 1 回入り、既存枠のときは増えない。フェーズ P7-B |
| F-MC | C | マルチカーソル | |
| F-MM | W | ミニマップ | |
| F-VIM | W | Vim モーダル | D12 により初期対象外 |

## 言語と実行

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-CS-BLD | M | `csc.exe` で手動ビルド | エラー行が問題一覧に出る。常時コンパイルは F-LIVE（フェーズ P7） |
| F-CS-RUN | M | ビルド成功後に EXE 起動 | stdout/stderr が出力パネル。常時コンパイルの生成物は起動しない |
| F-PS-RUN | M | `.ps1` を PowerShell 5.1 で実行 | `$PSVersionTable.PSVersion.Major -eq 5` |
| F-CMD-RUN | M | `.cmd` / `.bat` / 選択行を cmd で実行 | |
| F-TERM | M | 統合ターミナル | 既定 powershell.exe 5.1、cmd に切替可 |
| F-PROB | M | 問題一覧 | クリックでファイル+行。残 P1 の受け入れは手動 `csc` の診断を問題一覧に出すこと。将来の常時 csc（F-LIVE、フェーズ P7）と VBA Compile（F-VBA-BLD、フェーズ P7）も同じ診断モデル。VBA Compile の受け入れは F-VBA-BLD。競合したら新しい方で置き換える |
| F-LIVE | S | 常時コンパイル（診断のみ） | 入力停止後にデバウンスした Framework `csc.exe` が走り、失敗が問題一覧と波線に出る。生成物は起動されない。フェーズ P7-C。csc 専用（VBA Compile は F-VBA-BLD） |
| F-SQU | S | 構文エラーの波線 | C# は csc 診断位置、PS は `ParseInput` エラー位置、VBA は F-VBA-BLD の位置に、エラー色の波線が付く。cmd は波線なし。フェーズ P7-C |
| F-VBA-BLD | S | VBA Compile 診断 | プッシュ後の Excel Compile 失敗が問題一覧と波線に出る。Run されない。Excel 未起動のライブは走らない。フェーズ P7-C |

## デバッグ

| ID | 優先 | 言語 | 内容 | 受け入れ |
| --- | --- | --- | --- | --- |
| F-DBG-PS | S | PowerShell | 行ブレーク、ステップ、ローカル変数 | P3 |
| F-DBG-CS | S | C# | PDB + ステップ、コールスタック、ローカル | P4。ICorDebug 等インボックス API のみ |
| F-DBG-CMD | S | cmd | エコー実行、失敗行 | 本格ステップは必須にしない |
| F-DBG-VBA | S | VBA | マクロ指定実行、COM エラー表示。可能なら VBE 連携 | P6 |
| F-DBG-UI | S | 共通 | ブレークガター、続行/停止、デバッグコンソール | |

実行（デバッグなし）は M。ステップ実行は S。

## 補完とナビ

| ID | 優先 | 内容 | 受け入れ |
| --- | --- | --- | --- |
| F-CMP | S | キーワードと開いているファイルの識別子 | P5。全ホスト。キーワード静的表＋開いているファイルの識別子。cmd はキーワード、`%VAR%` / `!VAR!`、ファイル内ラベル。メンバーリストなし。ワークスペースのシンボル名は P5 必須ではない（提案 P8）。P7 でワークスペース／メンバー（S）。Roslyn ではない |
| F-GD | S | 定義へ移動 | F12 でユーザーソース上の定義へジャンプする。BCL / cmd へは入らない。Peek / 参照検索は W。フェーズ P7-B |
| F-HOV | S | ホバー / クイックインフォ | 全ホスト（cmd 含む）で、定義直前の F-DOC 枠または従来コメントがホバーに出る。フェーズ P7-B |
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
