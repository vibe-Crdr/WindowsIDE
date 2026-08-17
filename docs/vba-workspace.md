# VBA ワークスペースと Excel 同期

## 目的

ディスクではモジュールをフォルダ分けして扱い、Excel の VBProject は従来どおり平坦なコンポーネント一覧のままにする。同期は明示操作（プル / プッシュ）。

## ディスク側

既定ルートはワークスペースの `vba/`。設定で変更可。

```text
vba/
  Lib/
    StringUtil.bas
  App/
    Main.bas
  ThisWorkbook.cls
```

| 拡張子 | VBComponent 種別 | 初期 |
| --- | --- | --- |
| `.bas` | Standard module | 対象 |
| `.cls` | Class module / document のコード | 対象（document は名前で対応） |
| `.frm` + `.frx` | UserForm | 初期対象外（読み取りのみでもよい） |

エンコーディング既定は **CP932**。マップに上書きがあればそれに従う。

## Excel 側の名前（`namingMode`）

Excel にディレクトリは作れない。代わりに、ワークスペース設定で名前の付け方を切り替える。

| 値 | 規則 | 例 `vba/Lib/StringUtil.bas` |
| --- | --- | --- |
| `filename`（既定） | ファイル名から拡張子を除く。フォルダは IDE 専用 | `StringUtil` |
| `folder_prefix` | `vba/` からの相対フォルダを `_` でつなぎ、最後にファイル名 | `Lib_StringUtil` |

```text
filename:       vba/Lib/StringUtil.bas  →  StringUtil
folder_prefix:  vba/Lib/StringUtil.bas  →  Lib_StringUtil
folder_prefix:  vba/Lib/Text/Join.bas   →  Lib_Text_Join
```

共通規則:

- document モジュール（ThisWorkbook、シート）はモードに関係なく Excel の名前を使う。
- できた名前は VBA 識別子として妥当であること（先頭は英字、使える記号は `_`、長さ 31 文字以内）。違反は同期失敗。黙って切り詰めない。
- 同じ Excel 名になるファイルが木に 2 つあれば、プッシュもプルも失敗させ、両方のパスを問題一覧に出す。黙って上書きしない。
- モード変更はプレビュー（旧名 → 新名）を出してから。確認なしに Excel 側をリネームしない。

## マップファイル

`.windows-ide/vba-map.xml` は次を覚える。

- 対象ブックのフルパス（またはワークスペース相対）
- ルートフォルダ
- `namingMode`（`filename` / `folder_prefix`）
- コンポーネント名 → 相対パス
- document モジュール（ThisWorkbook、シート）の対応
- エンコーディング

Excel で名前を変えたら、次のプルでマップを更新する。ディスク側リネームはプッシュで Excel 側名を合わせる。

## 操作

| 操作 | 動き |
| --- | --- |
| プル | ブックを開き（必要なら起動）、VBComponents をエクスポートし、マップに従ってファイルを作る/更新する。IDE に未保存の同名バッファがあれば確認する |
| プッシュ | ディスクの `.bas` / `.cls` をインポートまたはコード置換する。Excel にだけあるモジュールは削除しない（初回は警告リスト）。削除は別コマンド |
| 実行 | モジュール名とマクロ名を指定し `Application.Run`。失敗は COM メッセージを出力パネルへ |

Excel が開いていて未保存なら、同期前に保存するか中止するかを聞く。

## 前提（Excel）

- Microsoft 365 64-bit
- 「VBA プロジェクト オブジェクト モデルへのアクセスを信頼する」が有効
- マクロ有効ブック（`.xlsm` / `.xlsb`）。`.xlsx` にはプッシュしない

## COM

遅延バインディングを提案する（[decisions.md](decisions.md) P5）。

- `Excel.Application`
- `Workbooks.Open`
- `VBProject` / `VBComponents` / `CodeModule`
- エクスポート: `VBComponent.Export`
- 取り込み: 一時ファイル経由 `Import` または `CodeModule` の一括置換

IDE プロセスは STA。Excel ダイアログをユーザーの前に出すときは、IDE 側でモーダルを重ねて操作不能にしない。

## ディレクトリが Excel に出ないこと

仕様である。ツリーは IDE の開発体験用。README やステータスで「Excel 上は平坦」と分かるようにする。
