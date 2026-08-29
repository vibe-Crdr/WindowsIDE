# 文書一覧

WindowsIDE の仕様はここに置く。実装よりこの文書を優先する。矛盾したら文書を先に直す。

| 文書 | 内容 |
| --- | --- |
| [requirements.md](requirements.md) | 要件定義（目的、機能、非機能、対象外、フェーズ） |
| [constraints.md](constraints.md) | 実行環境・言語・参照アセンブリの凍結条件 |
| [architecture.md](architecture.md) | 構成、プロセス境界、主要コンポーネント |
| [ide-features.md](ide-features.md) | IDE 機能の MoSCoW と受け入れ条件 |
| [vba-workspace.md](vba-workspace.md) | VBA のディレクトリ管理と Excel 同期 |
| [ui.md](ui.md) | ダークテーマ、レイアウト、フォント |
| [fonts.md](fonts.md) | フォント同梱。OS インストール不要 |
| [build.md](build.md) | 製品は `csc.exe`。ユーザー VB.NET は指定 `vbc.exe`（フェーズ P9。今は実装しない） |
| [decisions.md](decisions.md) | 確定事項と提案事項 |
| [glossary.md](glossary.md) | 用語 |

エージェント向けの入口はリポジトリ直下の [AGENTS.md](../AGENTS.md)。ルールは `.cursor/rules/`。
