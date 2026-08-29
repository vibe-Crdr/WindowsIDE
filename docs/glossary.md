# 用語

| 用語 | 意味 |
| --- | --- |
| WindowsIDE | 本リポジトリが作る、ローカル専用の統合開発環境。成果物 EXE 名も `WindowsIDE.exe`。 |
| 製品コード | ユーザーが実行する IDE 本体。C# 5 のみ。 |
| ツールチェーン | Cursor 用 MCP 起動スクリプトなど、IDE に同梱しない補助。Python はここだけ許可。 |
| 凍結環境 | `docs/constraints.md` に書いた OS / シェル / Office / コンパイラ / BCL。変更は要件改訂。 |
| インボックスアセンブリ | 指定の .NET Framework 4.8.1 フォルダ、GAC、Office が既に入れている DLL。NuGet や自前入手の第三者 DLL は含まない。 |
| 同梱フォント | EXE に埋め込んだ Cascadia Mono と源ノ角ゴシック。OS のフォント一覧には出さない。 |
| 外部ライブラリ | NuGet、git submodule の第三者コード、Web から落とした DLL / コントロール。禁止。 |
| ワークスペース | ユーザーが IDE で開く 1 フォルダ。設定は `.windows-ide/`。 |
| VBA ワークスペース | ワークスペース内で VBA ソースをディレクトリ付きで置く木。Excel 側は平坦。 |
| プル | Excel の VBProject からディスクへ書き出す。 |
| プッシュ | ディスクの VBA ファイルを Excel の VBProject へ取り込む。 |
| namingMode | Excel 側コンポーネント名の付け方。`filename` または `folder_prefix`。 |
| ホスト言語 | IDE が編集・実行・デバッグする言語。C# 5、VBA、Windows PowerShell 5.1、cmd。Markdown は含めない。 |
| cmd バッチの UTF-8 BOM | cmd.exe は UTF-8 BOM（EF BB BF）を先頭コマンドの一部として読む。ユーザー `.cmd` / `.bat` は D23 で BOM を書かない。 |
| 編集言語 | 編集はするが実行・デバッグしない言語。フェーズ P8 の Markdown（`.md`）。 |
| Framework csc | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。Roslyn / VS 付属 csc は使わない。 |
| 編集器インテリジェンス | フェーズ P7 の括弧強調・スマートインデント・自動閉じ・定義へ移動・ホバー・枠コメント・VBA キャピタライズ・常時 csc・波線・VBA Compile 診断。波 A（F-BR / F-AC / F-SIND / F-VBA-CASE）は着手。波 B/C は今は実装しない。P0–P2 の必須には含めない。 |
| フェーズ P8 | Markdown 字句とプレビュー、VBAProject 参照、キー記録マクロ。今は実装しない。**提案 P8**（F-CMP の P5 必須範囲）とは別。 |
| 提案 P8 | F-CMP を P5 でキーワードと開いているファイルに限ること。ワークスペース拡張を P5 必須にしない。確定していない。フェーズ P8 ではない。 |
| VBA Compile | Excel VBA コンパイラによる診断（F-VBA-BLD）。プッシュ後。Run しない。`WindowsIDE.Vba`。csc ではない。 |
| F-DOC | 言語ごとの枠コメントを定義直前に 1 回入れること。ラベルは `summary` / `args` / `returns`（採用済み P19）。 |
| 常時コンパイル | 入力停止後にデバウンスした Framework `csc.exe` で診断だけ再コンパイルすること。生成物は起動しない。手動ビルド（F-CS-BLD）とは別。VBA Compile は含めない。 |
| 波線 | 診断位置の下に既存エラー色で付ける下線。C# は csc、PS は `ParseInput`、VBA は F-VBA-BLD。自前パーサをコンパイラ診断と偽らない。 |
| クイックインフォ | F-HOV。全ホスト（cmd 含む）の要約表示（F-DOC 枠 / `///` / 直前コメント / Framework XML）。Ctrl+K Ctrl+I 相当。スクロールバーのつまみホバーとは別。Object Browser ではない。 |
| つまみホバー | スクロールバーつまみにマウスを乗せたときの色（Comment）。F-HOV ではない。 |
