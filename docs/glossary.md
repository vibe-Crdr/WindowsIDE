# 用語

| 用語 | 意味 |
| --- | --- |
| WindowsIDE | 本リポジトリが作る、ローカル専用の統合開発環境。成果物 EXE 名も `WindowsIDE.exe`。 |
| 製品コード | ユーザーが実行する IDE 本体。C# 5 のみ。 |
| ツールチェーン | Cursor 用 MCP 起動スクリプトなど、IDE に同梱しない補助。Python はここだけ許可。 |
| 凍結環境 | `docs/constraints.md` に書いた OS / シェル / Office / コンパイラ / BCL。BCL のパッチ FileVersion は Windows Update で動きうる。パスと 4.8 ファミリーは凍結。変更は要件改訂。 |
| インボックスアセンブリ | 指定の .NET Framework 4.8.1 フォルダ、GAC、Office が既に入れている DLL。NuGet や自前入手の第三者 DLL は含まない。 |
| 同梱フォント | EXE に埋め込んだ Cascadia Mono と源ノ角ゴシック。OS のフォント一覧には出さない。 |
| 外部ライブラリ | NuGet、git submodule の第三者コード、Web から落とした DLL / コントロール。禁止。 |
| ワークスペース | ユーザーが IDE で開く 1 フォルダ。設定は `.windows-ide/`。 |
| VBA ワークスペース | ワークスペース内で VBA ソースをディレクトリ付きで置く木。Excel 側は平坦。 |
| プル | Excel の VBProject からディスクへ書き出す。 |
| プッシュ | ディスクの VBA ファイルを Excel の VBProject へ取り込む。 |
| namingMode | Excel 側コンポーネント名の付け方。`filename` または `folder_prefix`。 |
| ホスト言語 | IDE が編集・実行・デバッグする言語。C# 5、VB.NET（指定 `vbc.exe`、フェーズ P9。今は実装しない）、VBA、Windows PowerShell 5.1、cmd。Markdown は含めない。VB.NET ≠ VBA ≠ VBScript（`.vbs` は Plain）。 |
| cmd バッチの UTF-8 BOM | cmd.exe は UTF-8 BOM（EF BB BF）を先頭コマンドの一部として読む。ユーザー `.cmd` / `.bat` は D23 で BOM を書かない。 |
| 編集言語 | 編集はするが実行・デバッグしない言語。フェーズ P8 の Markdown（`.md`）。VB.NET は編集言語ではなくホスト（P9）。 |
| Framework csc | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。Roslyn / VS 付属 csc は使わない。製品ビルドとユーザー C# 用。 |
| Framework vbc | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\vbc.exe`。バナーは Visual Basic 2012。ファイルバージョン 14.8.9221.0。Roslyn / VS 付属 vbc は使わない。ユーザー VB.NET 専用。製品ビルドには使わない。 |
| インデント線 | F-IG。編集器本文の縦ガイド。言語非依存。全ファイル形式。色は行番号色。ツリーのインデントではない。今は実装しない。 |
| 編集器インテリジェンス | フェーズ P7 の括弧強調・スマートインデント・自動閉じ・定義へ移動・ホバー・枠コメント・VBA キャピタライズ・常時 csc・波線・VBA Compile 診断。波 A（F-BR / F-AC / F-SIND / F-VBA-CASE）は完了。波 B（F-GD / F-HOV / F-DOC）は完了。波 C（F-LIVE / F-SQU / F-VBA-BLD 手動）は完了。ライブ VBA は今は実装しない。P0–P2 の必須には含めない。 |
| フェーズ P3 | PowerShell デバッグ（F-DBG-PS / F-DBG-UI 最小）。ディスク上 `.ps1` を同一プロセスの Runspace + SMA Debugger で行 BP・ステップ・ローカル。Host.PowerShell の 1 ショットは子プロセスのまま。 |
| フェーズ P4 | C# デバッグ（P4-A = F-DBG-CS）と cmd デバッグ（P4-B = F-DBG-CMD。今は実装しない）。P4-A は手動 csc の TEMP `out.exe` を ICorDebug.CreateProcess で別プロセスデバッグする。Ctrl+F5 は Host.Csharp のまま。 |
| F-DBG-CS | C# の行ブレーク・ステップ・ローカル・粗いスタック。P4-A。PDB + ICorDebug（インボックス COM）。Host.Csharp の 1 ショット実行（F-CS-RUN）とは別。 |
| フェーズ P8 | Markdown 字句とプレビュー、VBAProject 参照、キー記録マクロ。今は実装しない。**提案 P8**（F-CMP の P5 必須範囲）とは別。 |
| フェーズ P9 | VB.NET ホスト（`.vb`、指定 `vbc.exe`、実行。デバッグは段階）。今は実装しない。P7-A / P8 に押し込まない。 |
| フェーズ P10 | ユーザー C# の WinForms 視覚編集（F-WF-DSN）。今は実装しない。**提案 P10**（`namingMode` 既定 `filename`）とは別。 |
| フェーズ P11 | VBA UserForm の視覚編集（F-UF-DSN。`.frm` / `.frx`）。今は実装しない。**採用済み提案 P11**（言語は拡張子のみ）とは別。 |
| 提案 P8 | F-CMP を P5 でキーワードと開いているファイルに限ること。ワークスペース拡張を P5 必須にしない。確定していない。フェーズ P8 ではない。 |
| VBA Compile | Excel VBA コンパイラによる診断（F-VBA-BLD）。プッシュ後。Run しない。`WindowsIDE.Vba`。csc ではない。 |
| 構造ヒント | 自前パーサ（D31）が見つけた構文・モジュール内構造の誤り。F-VBA-BLD のコンパイラ診断ではない。問題一覧に出すなら別バケット／別文言（「構造」）。波線は既存 Error 色でよい。F-SQU の C「ブロック不一致波線」と同系。今は実装しない。 |
| VBA 構文解析 | IDE 知能用の自前構文解析（D30。製品 C# 5。`WindowsIDE.Languages`）。実装目標は D31（プッシュ前に VBE が構文・構造で落とす誤りをほぼ拾う。Excel 不要）。今は実装しない。VBA Compile（Excel）とは別。コンパイラ診断として問題一覧を埋めない。構造ヒントは可。プッシュはパーサ失敗で拒否しない。 |
| F-DOC | 言語ごとの枠コメントを定義直前に 1 回入れること。ラベルは `summary` / `args` / `returns`（採用済み P19）。 |
| F-WF-DSN | ユーザー C# の Windows Forms 視覚編集。R17。フェーズ P10。今は実装しない。IDE 本体 UI のデザイナーではない。VBA UserForm ではない。対ファイルは `Foo.cs` と `Foo.Designer.cs`。 |
| F-UF-DSN | VBA UserForm の視覚編集。R18。フェーズ P11。今は実装しない。WindowsIDE 内のデザイン面 + Excel Export/Import。VBE 窓の埋め込みではない。C# の F-WF-DSN ではない。`.frm` テキストと不透明 `.frx`。 |
| Designer.cs | WinForms の partial 対の生成側。`InitializeComponent` を置く。P14 の全 `.cs` 1 単位に含める。csc から外さない。 |
| UserForm | Excel VBA のフォーム（`.frm` / `.frx`）。視覚編集は F-UF-DSN / フェーズ P11。今は実装しない。C# の F-WF-DSN ではない。 |
| 常時コンパイル | 入力停止後にデバウンスした Framework `csc.exe` で診断だけ再コンパイルすること。生成物は起動しない。手動ビルド（F-CS-BLD）とは別。VBA Compile は含めない。 |
| 波線 | 診断位置の下に既存エラー色で付ける下線。C# は csc、PS は `ParseInput`、VBA のコンパイラ診断は F-VBA-BLD。構造ヒント（D31）も既存 Error 色でよいがコンパイラ診断と呼ばない。自前パーサをコンパイラ診断と偽らない。 |
| F-DBG-PS | PowerShell の行ブレーク・ステップ・ローカル。P3。同一プロセス Runspace。Ctrl+F5 の子プロセス実行（F-PS-RUN）とは別。 |
| クイックインフォ | F-HOV。P7-B は C# / VBA / PowerShell / cmd（cmd 含む）。VB.NET はフェーズ P9。解決できた定義のシグネチャ常時＋要約（F-DOC 枠 / `///` / 直前コメント / Framework XML）。Ctrl+K Ctrl+I 相当。スクロールバーのつまみホバーとは別。Object Browser ではない。 |
| つまみホバー | スクロールバーつまみにマウスを乗せたときの色（Comment）。F-HOV ではない。 |
