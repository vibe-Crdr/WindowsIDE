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
| ホスト言語 | IDE が編集・実行・デバッグする言語。C# 5、VBA、Windows PowerShell 5.1、cmd。 |
| Framework csc | `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。Roslyn / VS 付属 csc は使わない。 |
