# フォントの同梱

実行環境に Cascadia も源ノ角ゴシックも入っていない。**OS へのフォントインストールは要求しない**（管理者権限も不要）。指定フォントは製品に組み込み、IDE プロセスの中だけで使う。

詳細な見た目は [ui.md](ui.md)。

## 方針

| 項目 | 内容 |
| --- | --- |
| 配布 | フォントファイルをリポジトリの `assets/fonts/` に置き、`csc /resource` で `WindowsIDE.exe` に埋め込む |
| 読み込み | 起動時にリソースからメモリへ出し、**プロセスプライベート**に登録する |
| インストールしない | `C:\Windows\Fonts` へコピーしない。`AddFontResource` のシステム登録もしない |
| ライブラリではない | SIL OFL の TTF/OTF とライセンス文はアセット。NuGet や第三者 DLL には当たらない |

単一 EXE で対象 PC へコピーすれば、フォント無しの Windows 11 でも同じ見た目になる。

## 同梱ファイルと公式 URL（P0）

可変フォント（Variable）と Cascadia Code / NF は使わない。静的 Regular（と半角 Bold）だけ。CJK の他ウェイトとフル OTC は入れない。

| 用途 | ファイル | 論理名（`/resource`） |
| --- | --- | --- |
| 半角 Regular | `assets/fonts/cascadia/CascadiaMono-Regular.ttf` | `WindowsIDE.Fonts.CascadiaMonoRegular` |
| 半角 Bold（同梱のみ。P0 の TextView は未使用） | `assets/fonts/cascadia/CascadiaMono-Bold.ttf` | `WindowsIDE.Fonts.CascadiaMonoBold` |
| 半角ライセンス | `assets/fonts/cascadia/LICENSE` | About から読む（埋め込み可） |
| 全角 Regular | `assets/fonts/source-han-sans/SourceHanSansJP-Regular.ttf` | `WindowsIDE.Fonts.SourceHanSansJpRegular` |
| 全角ライセンス | `assets/fonts/source-han-sans/LICENSE.txt` | About から読む（埋め込み可） |

取得は `build/fetch-fonts.ps1`。公式の **個別ファイル**を優先し、143MB 級の zip 全体はリポジトリに残さない。

| ファイル | URL |
| --- | --- |
| Cascadia Mono Regular/Bold 静的 TTF | 公式タグ [v2407.24](https://github.com/microsoft/cascadia-code/releases/tag/v2407.24)。GitHub release に個別 TTF が無いため、スクリプトは `CascadiaCode-2407.24.zip` を `%TEMP%` へ取り、`ttf/static/CascadiaMono-Regular.ttf` と `ttf/static/CascadiaMono-Bold.ttf` だけ残して zip を消す。Cascadia Code / Variable / NF は残さない |
| Cascadia LICENSE | `https://raw.githubusercontent.com/microsoft/cascadia-code/v2407.24/LICENSE` |
| 源ノ角ゴシック JP Regular の元ファイル（2.005R、言語サブセット OTF） | `https://github.com/adobe-fonts/source-han-sans/raw/2.005R/SubsetOTF/JP/SourceHanSansJP-Regular.otf`（失敗時は `release` ブランチの同パス）。製品に入れるのはこれを静的 TTF に変換したファイル |
| 源ノ角 LICENSE.txt | `https://raw.githubusercontent.com/adobe-fonts/source-han-sans/2.005R/LICENSE.txt` |

可変フォントは禁止。半角は公式の静的 TTF。全角の公式ファイルは静的 OTF（CFF）だが、GDI+ の `DrawString` は CFF を描かず Yu Gothic UI に落ちる。同梱するのは `scripts/convert_source_han_ttf.py` で公式 OTF から作った静的 TTF で、リポジトリに置く。製品ビルドと `build/fetch-fonts.ps1` は Python を起動しない。TTF が無いとき fetch-fonts は公式 OTF を `%TEMP%\WindowsIDE-fonts` に取るだけで、変換は手動（fonttools が要る）。変換は OFL の Modified Version なので、ユーザーに見せるファミリー名は Reserved Font Name「Source」を使わず **源ノ角ゴシック JP** とする。`assets/fonts/source-han-sans/SourceHanSansJP-Regular.otf` が残っていたら fetch-fonts が消す。

## 読み込み方法（インボックスのみ）

フォントファイルごとに `PrivateFontCollection` を分ける。1 つに混ぜると、GDI+ が全角の `FontFamily.Name` を Cascadia Mono と返すことがある。

各フォントについて次の順。全部失敗したときだけ OS フォントへ退避する。

1. `System.Drawing.Text.PrivateFontCollection.AddMemoryFont`（バッファはフォントを捨てるまでピン留めする）
2. 失敗なら一時ファイル（`%TEMP%\WindowsIDE\fonts\` に GUID 名）へ書き出し、`PrivateFontCollection.AddFontFile`
3. 全部失敗時だけ 半角 `Consolas`、全角 `Yu Gothic` / `Yu Gothic UI` / `MS Gothic`。退避は常に `UsedFallback=true` でステータスにエラーを出す。`FontFamily.GenericMonospace` は成功扱いしない

採用条件は、今足したファミリだけを見ること。ステータスのフォント名は `name` テーブル（日本語のファミリー名、無ければ英語）で、`FontFamily.Name` は使わない。全角ファイルなのに GDI 名が Cascadia Mono の顔、および「あ」の `DrawString` が Yu Gothic UI と同じ顔は成功にしない（Yu Gothic UI が無い PC では字形検査をしない）。

GDI 登録系（`gdi32.AddFontMemResourceEx` / `AddFontResourceEx(FR_PRIVATE)`）は GDI+ の `new FontFamily(name)` から参照できないため使わない（D32）。

一時ファイルのライフサイクルは 3 段。書き出し失敗時はその場で削除、成功分は終了時に `FontLoader.Cleanup()`（Program.Main の finally）が削除し、前回異常終了の残りは次回起動時の `LoadFromBytes` 先頭で 1 ファイルずつ掃除する（他インスタンスがロック中のファイルは残る）。`build/fetch-fonts.ps1` の作業場 `%TEMP%\WindowsIDE-fonts` とは別ディレクトリ。

各経路の失敗と最終採用結果は `%TEMP%\WindowsIDE.log` に記録する（MessageBox は出さない）。

全角のファミリー名は **源ノ角ゴシック JP**。ステータスは `Cascadia Mono / 源ノ角ゴシック JP` になる。

`C:\Windows\Fonts` へはコピーしない。OS に Cascadia Mono が入っていても同梱を使う。

半角は **Cascadia Mono のみ**。Cascadia Code は同梱しない（GDI+ ではリガチャがほぼ出ないため）。

## グリフ振り分け

ASCII 印字（U+0020–U+007E）と半角カナ（U+FF61–U+FF9F）は Cascadia Mono。それ以外とサロゲートペアは源ノ角（全角側）。P0 本文描画は Regular のみ。

## ビルド

`build/windows-ide.rsp` に次を足す。

```text
/resource:assets\fonts\cascadia\CascadiaMono-Regular.ttf,WindowsIDE.Fonts.CascadiaMonoRegular
/resource:assets\fonts\cascadia\CascadiaMono-Bold.ttf,WindowsIDE.Fonts.CascadiaMonoBold
/resource:assets\fonts\source-han-sans\SourceHanSansJP-Regular.ttf,WindowsIDE.Fonts.SourceHanSansJpRegular
```

論理名は `WindowsIDE.Fonts.*` で固定し、コード側と食い違わせない。
