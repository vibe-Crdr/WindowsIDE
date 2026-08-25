# 外観

## 方針

ダーク専用。枠・アイコン・余白は薄く、編集領域を広くする。VS Code の機能は持つが、Activity Bar の大きなアイコン列や派手な影は置かない。配色は neovim の tokyonight 系を基準にする。

## レイアウト

```text
+---------+---------------------------+
| ツリー  | タブ                       |
|         |---------------------------|
|         | 編集器                     |
|         |                           |
+---------+---------------------------+
| パネル（問題 / 出力）                         |
+-------------------------------------+
| ステータス（言語, 行:列, エンコーディング, フォント） |
```

- 左: ファイルツリー（細いスクロール、インデントだけ）
- 中央: タブ + 編集器
- 下: パネル（問題 / 出力）。非表示にできる。デバッグとターミナルは後のフェーズで足す。今は空タブを置かない
- コマンドパレットは画面中央の細い入力。モーダル全面は使わない
- キーは VS Code 風（Ctrl+P クイックオープン、Ctrl+Shift+P パレット、Ctrl+S 保存など）。Vim モーダルは初期対象外

### P0 レイアウト

P0 では下パネルを出さない。問題一覧・出力・デバッグ・ターミナル用のコントロールは作らない。画面はメニュー、`SplitContainer`（左ツリー | 右タブ+編集器）、ステータスバーだけ。Ctrl+P / Ctrl+F / Ctrl+Shift+P はバインドしない。

単位は 96dpi DIP。`Form.Size` は外枠（ClientSize ではない）。

- 起動 Size 1280×800 DIP、MinimumSize 960×600 DIP
- 左スプリッタ幅は 6 DIP、色は LineNumber。起動直後の左ペイン 260 DIP は **初回 OnShown のみ**（幅が足りるとき）。以降はセッション内でユーザーが動かす。`Panel1MinSize` 160 DIP、`Panel2MinSize` 320 DIP。幅は XML に書かない
- 作業領域の 90% にクランプ（高さ優先）。MinimumSize が作業領域より大きければ作業領域まで落とす
- 適用後、作業領域で再センタリングする

### P1 ハイライト

P1 で Local / Instance / Method / Type の 4 色を足す。これ以外の新しい色は足さない。ステータスは **言語 | 行:列 | エンコーディング | フォント**。
言語名は英語（C# / VBA / PowerShell / cmd / Plain）。StatusStrip 余白は左右 8 DIP・上下 4 DIP（全角セルが高ければ extra を足す）。SizingGrip は出さない。
Ctrl+F は P1 F-FIND。

### P1 F-CS-BLD / F-PROB（問題一覧）

左右 split の外側に上下 `SplitContainer`（`Orientation.Horizontal`）。起動時は `Panel2Collapsed = true`（占有しない）。初回ビルドまたは初回実行で展開し、初期下ペイン **180 DIP**（初回だけ。左 260 DIP と同じ）。以降はセッション内スプリッタ。XML に書かない。Esc は FindBar のまま。問題一覧のフォントはツリーと同じ **12 DIP** DualFont（本文 `fontSize` 非連動）。スクロールは ThemedScrollBar **10 DIP** オートハイド。ListView / DataGrid / RichTextBox は使わない。色は既存のみ: 背景 `Background`、error 種別 `Error`、warning 種別 `Comment`、本文 `Foreground`、現在行 `CurrentLine`、枠 `Border`。新しい Theme 色は足さない。ビルド完了で編集フォーカスを奪わない。

### P1 F-CS-RUN（下パネル：問題 / 出力）

下パネルは **問題** と **出力** のみ。空のデバッグ／ターミナルタブは置かない。ヘッダ（タブチップ + ×）は BottomPane。問題一覧側に題名・件数・× の二重クロムは置かない。件数は問題一覧の公開プロパティをヘッダに出す。× で畳み、次のビルドまたは実行で出す。ビルド完了（成功・失敗・合成）と実行の csc 失敗は問題タブ。csc 成功して起動するときは出力を空にして出力タブ。出力はオーナー描画、12 DIP DualFont、ThemedScrollBar 10 DIP。stdout は `Foreground`、stderr は `Error`、起動／終了コードは `Comment`。背景 `Background`、枠 `Border`。4000 行キャップ（先頭捨て）。横幅計測は先頭 4096 文字。新しい Theme 色は足さない。実行完了で編集フォーカスを奪わない。

P1 F-FIND の FindBar はタブ直下・編集器の上（入れ子 `Panel`。`TabStrip` は `Dock.Top`、その下の `editorColumn` が `Dock.Fill`。列内は FindBar `Dock.Top`、`TextView` `Dock.Fill`）。`Visible=false` のとき占有しない。検索・置換の入力は DualFont オーナー描画（`DualFontField`。本文と同じ IME。`BackColor=EditorBackground`、`ForeColor=Foreground`。外周 1 物理 px の Theme.Border）。高さはセル＋1px 枠で、行の内側いっぱいに引き伸ばさない。行高はその高さ＋上下 4 DIP。半角は本文と同じ Cascadia Mono と `editor/@fontSize` DIP（`CreateHalfWidth` + `GetDpi` の物理 px。メニューの 12 DIP ではない）。全角は源ノ角ゴシック。ラベル・件数・ボタン・「Aa」はメニュー／ステータスと同じ 12 DIP 双フォント（本文 `fontSize` 非連動）。ボタン内の文字は DualFontPainter で矩形の中央。新しい Theme 色は足さない。0 件かつクエリ非空の件数と検索欄は既存 Error。現在ヒットは既存 Selection。

### P1 F-CMD-RUN 選択行

実行メニューに「選択行を実行」（表示 F8。ShortcutKeys は付けない。ProcessCmdKey）。出力契約は F-CS-RUN と同じ（stdout は `Foreground`、stderr は `Error`、起動／終了は `Comment`）。空のターミナルタブは置かない。

### フェーズ P7（今は実装しない）

波線は既存エラー `#f7768e`。対括弧は Selection。クイックインフォ（F-HOV）の枠は既存の背景 / 前景 / 枠。フェーズ P7 でも新しいテーマ色は足さない。虹色括弧は W。P1 の「これ以外の新しい色は足さない」は維持する。

### フェーズ P8（今は実装しない）

Markdown プレビューは新しいテーマ色を足さない。見出しは Keyword、フェンスは String。プレビュー方式は採用済み P21（オーナー描画サブセット。同梱フォント。WebBrowser は使わない）。同梱フォント（Cascadia Mono / 源ノ角ゴシック）はプレビューでも破らない。

## 色（初期）

| 役割 | 色 |
| --- | --- |
| 背景 | `#1a1b26` |
| 編集背景 | `#16161e` |
| 前景 | `#c0caf5` |
| コメント | `#565f89` |
| 行番号 | `#3b4261` |
| 現在行 | `#292e42` |
| 枠線 | `#1f2335` |
| 選択 | `#3d59a1` |
| エラー | `#f7768e` |
| キーワード | `#bb9af7` |
| 文字列 | `#9ece6a` |
| 数値 | `#ff9e64` |
| ローカル | `#7dcfff` |
| インスタンス | `#2ac3de` |
| メソッド | `#e0af68` |
| 型 | `#73daca` |
| ステータスバー | `#16161e` |

ライトテーマは作らない。

## フォント

半角英数記号と全角を混ぜて描画する。グリフごとにフォントを切り替える。

| 対象 | ファミリ | 備考 |
| --- | --- | --- |
| 半角 | Cascadia Mono（同梱） | 切替なし。Cascadia Code は採用しない |
| 全角 | 源ノ角ゴシック JP（同梱） | OS のフォント名検索はしない |

半角の判定: ASCII 印字（U+0020–U+007E）および一般的な半角カナを Cascadia 側。それ以外の文字は 源ノ角ゴシック。

**OS にこれらのフォントが入っている必要はない。** 実行環境には入っていない前提とする。ファイルは製品 EXE に埋め込み、プロセス内だけで読み込む。手順は [fonts.md](fonts.md)。`C:\Windows\Fonts` へのインストールはしない。

既定サイズは **14**（96dpi の DIP、CSS px 相当、VS Code 14 相当）。設定で変更可。物理ピクセルは `ToPixels(dip, GetDpi(hwnd))`（`round(dip * dpi / 96)`）。`Control.DeviceDpi` は使わない。本文フォントは `GraphicsUnit.Pixel`。行高は `Font.Height` と DIP 余白から計算し、半角・全角でベースラインを揃える。

タブはツリー／メニュー／ステータスと同じ **12 DIP** の `GraphicsUnit.Pixel` DualFont（本文 `fontSize` 非連動）。About だけシステム UI ファミリを **12 DIP** の `GraphicsUnit.Pixel` で持つ。高さだけスケールして題名が 96dpi のまま、にはしない。

ツリーは **12 DIP** の `GraphicsUnit.Pixel` で Cascadia Mono + 源ノ角ゴシックをグリフ切替する。本文 `fontSize` には連動しない。

メニュー（MenuStrip）とステータス（StatusStrip）はツリーと同じ **12 DIP** の `GraphicsUnit.Pixel` で Cascadia Mono + 源ノ角ゴシックをグリフ切替する。本文 `fontSize` には連動しない。タブも同じ **12 DIP** DualFont（本文 `fontSize` 非連動）。About だけシステム UI ファミリを **12 DIP** の `GraphicsUnit.Pixel` のままとする。

ドロップダウン項目の幅は DualFont（ニーモニック除去後のラベルと `GetShortcutDisplayText`）に左 24 DIP・タブギャップ・右矢印列（論理 10+8 DIP）を足した行幅と、WinForms の `MaxItemSize`（`base.GetPreferredSize`）の大きい方。兄弟 DualFont 行は同幅にし、ショートカットは共有右端に揃える。本文 `fontSize` 非連動は上記のまま。

同梱の読み込みに失敗したときだけ Consolas / Yu Gothic / MS Gothic に退避し、ステータスへエラーを出す。

## 編集器の見た目

- 行番号ガターは狭く、現在行だけ少し明るく。数字はガター内で**右寄せ**し、左右に **8 DIP** の余白を取る。本文と選択はガター境界より左へ描かない（部分可視グリフはクリップ、省略記号なし）
- カレント行ハイライト
- ブロックカーソルではなく細いバー（neovim の insert に近い）。日本語 IME の未確定は自前描画（Selection 背景 + 下線）。システム変換窓は出さない。小さい既定箱が残る場合は隠す（START を DefWndProc に渡さない。保険の CompositionFont は IME 可視のシステム顔）。候補リストはシステム。変換中のバーキャレットは未確定内のカーソル位置に追従する
- スクロールバーは自前 **10 DIP**（編集器・ツリー・About のライセンス欄）。矢印無し。内容がビューポートに収まるときは非表示（オートハイド）。横の必要判定は半角幅×文字数ではなく、描画と同じ双フォント計測（編集器は `MeasureRun`、ツリーは `DualFontPainter.Measure`）。ツリーの横位置は自前オフセットで、SysTreeView32 の横スクロールは使わない。トラックはホスト背景（編集器 `EditorBackground`、ツリー `Background`）。つまみ LineNumber、つまみホバー Comment、押下 Selection
- ミニマップは初期対象外
- タブはファイル名のみ。不要なアイコンを並べない。閉じる印はタブ矩形内に収め、題名との間と右端に各 6 DIP、色は Foreground、幾何 2 本線（幅 2、SmoothingMode は足さない）、一辺は 8 DIP、垂直中央、ヒットは描画と同じ矩形とする。

## タイトルバー

DWM 属性でキャプションをシェル色に合わせる。`DWMWA_CAPTION_COLOR` = Background、`DWMWA_TEXT_COLOR` = Foreground、`DWMWA_BORDER_COLOR` = Border。失敗時は `DWMWA_USE_IMMERSIVE_DARK_MODE` のみ残す。

## WinForms 実装メモ

- フォーム境界をダークに（クラシック 3D ボーダーを消す）
- システムツリービューをそのまま使わず、ツリーもオーナー描画してよい
- `Application.EnableVisualStyles` は使うが、色は自前
- マニフェストで Per-Monitor DPI Awareness
- `AutoScaleMode.None`。固定 `Height=28` などは 96dpi DIP 下限とし、描画時に手動換算する
- メイン枠の `OnDpiChanged` は `base` のみ。起動サイズの再適用や `ApplyEditorSettings` はしない。子は自分で `GetDpi` する
- About は `AutoScaleMode.None`、アンカー配置、ClientSize 720×540 DIP 以上、ボタン帯 48 DIP。閉じると注記が見切れないこと。開いたときライセンス TextBox は非選択。初期フォーカスは「閉じる」。手動選択してコピーは可
- フォルダを開く / 名前を付けて保存は Windows Common Item Dialog（IFileOpenDialog / IFileSaveDialog）
