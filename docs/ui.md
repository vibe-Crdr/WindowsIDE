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
| パネル（問題 / 出力 / デバッグ / ターミナル） |
+-------------------------------------+
| ステータス（言語, 行:列, エンコーディング, フォント） |
```

- 左: ファイルツリー（細いスクロール、インデントだけ）
- 中央: タブ + 編集器
- 下: パネル。非表示にできる
- コマンドパレットは画面中央の細い入力。モーダル全面は使わない
- キーは VS Code 風（Ctrl+P クイックオープン、Ctrl+Shift+P パレット、Ctrl+S 保存など）。Vim モーダルは初期対象外

### P0 レイアウト

P0 では下パネルを出さない。問題一覧・出力・デバッグ・ターミナル用のコントロールは作らない。画面はメニュー、`SplitContainer`（左ツリー | 右タブ+編集器）、ステータスバーだけ。Ctrl+P / Ctrl+F / Ctrl+Shift+P はバインドしない。

### P1 ハイライト

P1 で Local / Instance / Method / Type の 4 色を足す。これ以外の新しい色は足さない。ステータスは **言語 | 行:列 | エンコーディング | フォント**。下パネルと Ctrl+F はまだ無い。

単位は 96dpi DIP。`Form.Size` は外枠（ClientSize ではない）。

- 起動 Size 1280×800 DIP、MinimumSize 960×600 DIP
- 左スプリッタ幅は 6 DIP、色は LineNumber。起動直後の左ペイン 260 DIP は **初回 OnShown のみ**（幅が足りるとき）。以降はセッション内でユーザーが動かす。`Panel1MinSize` 160 DIP、`Panel2MinSize` 320 DIP。幅は XML に書かない
- 作業領域の 90% にクランプ（高さ優先）。MinimumSize が作業領域より大きければ作業領域まで落とす
- 適用後、作業領域で再センタリングする

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

タブ・About はシステム UI ファミリを **12 DIP** の `GraphicsUnit.Pixel` で持つ。高さだけスケールして題名が 96dpi のまま、にはしない。

ツリーは **12 DIP** の `GraphicsUnit.Pixel` で Cascadia Mono + 源ノ角ゴシックをグリフ切替する。本文 `fontSize` には連動しない。

同梱の読み込みに失敗したときだけ Consolas / Yu Gothic / MS Gothic に退避し、ステータスへエラーを出す。

## 編集器の見た目

- 行番号ガターは狭く、現在行だけ少し明るく。数字はガター内で**右寄せ**し、左右に **8 DIP** の余白を取る。本文と選択はガター境界より左へ描かない（部分可視グリフはクリップ、省略記号なし）
- カレント行ハイライト
- ブロックカーソルではなく細いバー（neovim の insert に近い）。日本語 IME の未確定は自前描画（Selection 背景 + 下線）。システム変換窓は出さない。小さい既定箱が残る場合は隠す（START を DefWndProc に渡さない。保険の CompositionFont は IME 可視のシステム顔）。候補リストはシステム。変換中のバーキャレットは未確定内のカーソル位置に追従する
- スクロールバーは自前 **10 DIP**（編集器・ツリー・About のライセンス欄）。矢印無し。内容がビューポートに収まるときは非表示（オートハイド）。横の必要判定は半角幅×文字数ではなく、描画と同じ双フォント計測（編集器は `MeasureRun`、ツリーは `DualFontPainter.Measure`）。ツリーの横位置は自前オフセットで、SysTreeView32 の横スクロールは使わない。トラックはホスト背景（編集器 `EditorBackground`、ツリー `Background`）。つまみ LineNumber、ホバー Comment、押下 Selection
- ミニマップは初期対象外
- タブはファイル名のみ。不要なアイコンを並べない。閉じる印はタブ矩形内に収める

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
