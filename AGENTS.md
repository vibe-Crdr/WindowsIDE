# AGENTS

このリポジトリでコードや仕様を変えるときの入口。

## 読む順

1. ユーザーの今回の指示
2. [docs/requirements.md](docs/requirements.md)
3. [docs/constraints.md](docs/constraints.md)
4. [docs/decisions.md](docs/decisions.md)
5. 該当する [docs/architecture.md](docs/architecture.md)、[docs/ui.md](docs/ui.md)、[docs/ide-features.md](docs/ide-features.md)、[docs/vba-workspace.md](docs/vba-workspace.md)、[docs/build.md](docs/build.md)
6. `.cursor/rules/`（とくに `windows-ide-requirements.mdc`）

文書と実装が食い違ったら、勝手に片方へ寄せず、食い違いを書いてから文書か実装を直す。

## 動かないもの

- 製品への NuGet、第三者 DLL、C# 6+、PowerShell 7、Roslyn、`dotnet` ビルド
- `scripts/` の Python を製品ランタイムにすること
- 指定パス以外の `csc.exe`

## 製品コード

- 言語は C# 5 のみ。UI は WinForms + 自前編集器（提案が覆るまで）
- 設定は XML
- コメントと公開 API は `.cursor/rules/agent-basic-formatting.mdc`

## Subagents

`.cursor/agents/` の 3 体。言語別・画面別エージェントは置かない。コード探索は組み込み Explore、長いシェル出力は組み込み Bash。

| 呼び出し | 役割 |
| --- | --- |
| `/architect` | 実装前の設計。読み取り専用。Builder へ手順を渡す |
| `/builder` | 計画後の実装。指定 `csc.exe` で検証する |
| `/reviewer` | 実装後・完了前の独立レビュー。読み取り専用。修正は Builder へ返す |

横断変更は architect → builder → reviewer の順。親が「完了」と自己申告しない。

## MCP

プロジェクト MCP の使い分けは `.cursor/rules/project-mcp.mdc`。エージェントまたは Tab がこのリポジトリを編集したあと、`stop` hook が codebase-memory の `index_repository`（project `WindowsIDE`）を一度実行するようフォローアップする。
