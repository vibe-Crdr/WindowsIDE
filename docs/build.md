# ビルド

製品は MSBuild も `dotnet` も使わない。Windows PowerShell 5.1 から Framework `csc.exe` を呼ぶ。

## コンパイラ

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
```

このパス以外で通ったビルドは、このプロジェクトのビルドではない。

## 必須スイッチ

```text
/noconfig
/nostdlib
/platform:x64
/target:winexe
/debug+
/utf8output
```

`/noconfig /nostdlib` のあと、DLL はフルパスで `/r:` する。P0 の EXE は `/win32manifest:src\WindowsIDE\app.manifest` を付ける。`/noconfig` は rsp ではなく `compile.ps1` のコマンドラインに置く（csc は応答ファイル内の `/noconfig` を無視する）。

## 基準参照

```text
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\mscorlib.dll
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.dll
```

ビルドは欠如と 4.8 ファミリー（FileMajorPart=4 かつ FileMinorPart=8）だけを Fail する。パッチ FileVersion は Windows Update の 4.8.1 サービス更新で変わりうるためピンにしない。観測値はログする。サイレントに別 csc / 別フォルダは使わない。

## P0 の `/r`（これだけ）

すべて `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\` のフルパス。

- `mscorlib.dll`
- `System.dll`
- `System.Core.dll`
- `System.Drawing.dll`
- `System.Windows.Forms.dll`
- `System.Xml.dll`

P0 では `Microsoft.CSharp.dll` / Office PIA / `System.Xml.Linq.dll` は足さない。後のフェーズで足すときは [decisions.md](decisions.md) を更新してから。

P13: PowerShell ハイライト用に SMA を `/r` する（GAC `v4.0_3.0.0.0__31bf3856ad364e35`）。P3 では同じ参照を `WindowsIDE.Debug` の実行ホスト（Runspace + Debugger）としても使う。`WindowsIDE.Languages` は `Parser.ParseInput` のみ（Runspace を開かない）。`WindowsIDE.Host.PowerShell` は `powershell.exe` 5.1 の子プロセスであり、この SMA 参照で Runspace を開かない。行 BP は GAC 3.0 に公開 `SetLineBreakpoint` が無い（指定 csc で CS1061）。internal `LineBreakpoint` ctor + 公開 `SetBreakpoints`。`$PSHome` の SMA を製品 `/r` と取り違えない。

```text
C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Management.Automation\v4.0_3.0.0.0__31bf3856ad364e35\System.Management.Automation.dll
```

`WindowsIDE.Languages` のソースを `build/windows-ide.rsp` と `build/windows-ide-tests.rsp` に列挙する（`IdentifierClassifier.cs` と `Languages/CSharp/` を含む）。Languages は Theme / WinForms を参照しない。BCL 型名は実行時 `Assembly.LoadFrom`（コンパイル `/r` は既存 Framework DLL + SMA）。

P7-B の新規ソースも rsp に列挙する。Languages は `VbaKeywordCase.cs` の次（`SymbolKind` / `DeclaredSymbol` / `IdentifierHit` / `IdentifierAtCaret` / `DefinitionResolver` / `DocCommentRules` / `HoverText` / `WorkspaceSymbols` / `CSharp/CSharpSymbols` / `CSharp/BclXmlDocs`）。`HoverInfoControl.cs` は `FindBar.cs` の次（製品 rsp のみ。テスト rsp には足さない）。`VbaFileEnumerator.cs` は `CsFileEnumerator.cs` の次。製品 `/r` は増やさない。

ユーザーコードの常時コンパイル（F-LIVE）も同じ Framework `csc.exe` を使う。live rsp は `/nostdlib /platform:x64 /target:library /utf8output`（`/debug+` も `/debug-` も書かない）。`/noconfig` は rsp に書かない。`/r:` は手動と同じ 6 DLL。`/out` は `%TEMP%\WindowsIDE\build\live\<key>\out.dll`。診断後に live ディレクトリをベストエフォート削除する。生成物は起動しない。製品の `/r` は増やさない。手動 exe 節（`/target:exe /debug+`）は変えない。Framework XML ドキュメントは DLL 隣の `.xml` をディスクから読む（`System.Xml` は既に製品 `/r` 済み）。新しい `/r` は出さない。ユーザー VB.NET（F-VB-BLD、フェーズ P9。今は実装しない）は指定 `vbc.exe` を使う。製品 `/r` に `Microsoft.VisualBasic.dll` を足さない。csc に `.vb` を渡さない。

フェーズ P10（F-WF-DSN。今は実装しない）の製品 `/r` 候補は、同じ Framework フォルダの `System.Design.dll` と `System.Drawing.Design.dll` である。足すときは [decisions.md](decisions.md) の提案 P38 を採用してから。**今は rsp に足さない。** ユーザー手動 csc には足さない（ユーザーは既存 6 DLL で足りる。Design は IDE のホスト用）。`Microsoft.CSharp.dll` は製品にもユーザーにも足さない。

フェーズ P11（F-UF-DSN。今は実装しない）は製品 `/r` を増やさない。System.Design を UserForm のために足さない（P38 は C# WinForms 側）。Office PIA も足さない。Excel は遅延バインディングの Export/Import のみ。

## ユーザー手動 csc（F-CS-BLD）

製品の `compile.ps1`（`/target:winexe`、SMA、フォント `/resource`）と混同しない。IDE がユーザー `.cs` を手動ビルドするときだけ、同じ指定 `csc.exe` を別プロセスで呼ぶ。

- コマンドライン先頭は必ず `/noconfig`。rsp には `/noconfig` を書かない（rsp 内は無視され CS2023）
- rsp（UTF-8 BOM、TEMP）: `/nostdlib /platform:x64 /target:exe /debug+ /utf8output`
- `/r:` は Framework64 の 6 DLL のみ（mscorlib, System, System.Core, System.Drawing, System.Windows.Forms, System.Xml）。SMA / Microsoft.CSharp / Office は足さない。製品 `/r` は増やさない
- `/out` は `%TEMP%\WindowsIDE\build\manual\<key>\out.exe`（`<key>` はワークスペース根または単一ファイルのフルパスを OrdinalIgnoreCase で SHA1 短縮）。PDB 可。生成物は消さない。手動 csc は **`Process.Start` しない**。`/target:exe` は提案 P39 を採用するまで変えない（デザイナー有無で `winexe` にしない）
- ユーザー EXE の起動と、そのプロセス参照だけの Kill は `WindowsIDE.Host.Csharp`。再実行・手動 csc の直前・MainForm.Dispose で Kill する。プロセス名検索はしない
- ユーザー `.ps1` の起動は `WindowsIDE.Host.PowerShell`（`SpecialFolder.System` + `WindowsPowerShell\v1.0\powershell.exe` 子プロセス）。SMA は 1 ショット実行のホストではない。再実行・手動 csc の直前・MainForm.Dispose で Kill する。プロセス名検索はしない
- P3 の PowerShell デバッグは `WindowsIDE.Debug` が同一プロセスで Runspace を開く。`src\WindowsIDE\Debug\` を `Host\Cmd\CmdProcessHost.cs` の次に列挙する。P4-A は既存 Debug 4 本の次に `Debug\DebugStackFrame.cs` と `Debug\CorDebug\` 4 本（`CorDebugNative` / `CorDebugManagedCallback` / `PdbBinder` / `CorDebugSession`）を列挙する。製品 `/r` は増やさない（ICorDebug / ISymUnmanagedBinder は P/Invoke と CoCreateInstance。ISymWrapper は参照しない）。`src\WindowsIDE\Ui\DebugPaneControl.cs` は BottomPane の次（製品 rsp。テスト rsp も BottomPane の次）。Languages / Host.PowerShell は Runspace を開かない
- ユーザー `.cmd` / `.bat` の起動は `WindowsIDE.Host.Cmd`（`SpecialFolder.System` + `cmd.exe` 子プロセス）。再実行・手動 csc の直前・MainForm.Dispose で Kill する。プロセス名検索はしない
- 統合ターミナル（F-TERM）は `WindowsIDE.Terminal`（CreatePseudoConsole + CreateProcessW）。ソースは Host の次に列挙する。kernel32 の P/Invoke に追加 `/r` は不要。Host.* と混ぜない。1 ショットの Kill 集合には入れない。MainForm.Dispose で PTY も閉じる（UI で待たない）
- `WindowsIDE.Vba` のソースは Terminal 9 本の次に列挙する。P2 も PIA `/r` なし。Microsoft.CSharp なし
- 無題は対象外。csproj は作らない

## ユーザー手動 vbc（F-VB-BLD、フェーズ P9。今は実装しない）

製品の `compile.ps1` とユーザー手動 csc（F-CS-BLD）と混同しない。IDE がユーザー `.vb` を手動ビルドするときだけ、指定 `vbc.exe` を別プロセスで呼ぶ。

- パスは `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\vbc.exe` のみ。VS / Roslyn vbc は使わない
- フォーカスがディスク上 `.vb` のとき Ctrl+Shift+B は vbc。それ以外は現行どおり csc
- csc に `.vb` を渡さない。vbc に `.cs` を渡さない。`.vbproj` は作らない
- `/noconfig` をコマンドライン先頭にするかは実装時に csc と同じ罠（応答ファイル内は無視）を踏まないこと
- `/r` は提案 P26（確認待ち）。製品 `/r` は増やさない。`Microsoft.VisualBasic.dll` を IDE 本体へ足さない
- 起動は `WindowsIDE.Host.VbNet`。vbc は `Process.Start` しない
- 無題は拒否（C# 手動ビルドに合わせる）

## レスポンスファイル

`build/windows-ide.rsp` にスイッチと `/r` とソース一覧と `/resource` を置く。`build/compile.ps1` は csc のフルパスと rsp だけを渡す。PowerShell 7 構文は使わない。`src/WindowsIDE/Ui/CommonItemDialog.cs` を rsp に含める。ole32 / shell32 の P/Invoke に追加 `/r` は不要。`src/WindowsIDE/Host/PowerShell/PowerShellProcessHost.cs` を `Host/Csharp/CsharpProcessHost.cs` の次に列挙する（`windows-ide-tests.rsp` も同じ）。`src/WindowsIDE/Host/Cmd/CmdProcessHost.cs` を PowerShell ホストの次に列挙する。`src/WindowsIDE/Debug/` を Cmd ホストの次に列挙する。`src/WindowsIDE/Terminal/` の 9 本を Debug の次に列挙する。`src/WindowsIDE/Vba/` を Terminal 9 本の次に同じ順で列挙する。`src/WindowsIDE/Ui/DebugPaneControl.cs` は BottomPane の次（製品 rsp。テスト rsp にも BottomPane があるので同様）。`src/WindowsIDE/Ui/TerminalControl.cs` はその次に列挙する。`src/WindowsIDE/Editor/CmdSelectionRules.cs` を `FindRules.cs` の次に `windows-ide.rsp` と `windows-ide-tests.rsp` へ列挙する。`src/WindowsIDE/Editor/SquiggleSpan.cs` を `FindRules.cs` / `CmdSelectionRules.cs` の近くに列挙する。`src/WindowsIDE/Languages/PowerShellParseErrors.cs` を `PowerShellSemantic.cs` の次に列挙する。`src/WindowsIDE/Build/LiveCompileUnit.cs` を `CompileUnit.cs` の次に列挙する。`src/WindowsIDE/Vba/VbaCompiler.cs` を `VbaSyncService.cs` の次に列挙する。`src/WindowsIDE/Ui/ChromeMark.cs` を FindBar.cs の次に列挙する。`src/WindowsIDE/Ui/CreateBarGlyph.cs` を ChromeMark.cs の次、FileTreeCreateBar.cs の前に列挙する。`FileTreeCreateBar.cs` をその近くに列挙する。`src/WindowsIDE/Workspace/WorkspaceItemRules.cs` を PathGuard.cs の次、`WorkspaceCreateRules.cs` をその次、`WorkspaceRecycle.cs` を CreateRules の次に列挙する（製品 rsp とテスト rsp）。shell32 の P/Invoke に追加 `/r` は不要。

出力は `build/out/WindowsIDE.exe`。`bin/` や `obj/` は使ってもよいが git に入れない。

フォントは `assets/fonts/` を `/resource` で EXE に埋め込む。[fonts.md](fonts.md)。`src/WindowsIDE/app.config` は `build/out/WindowsIDE.exe.config` にコピーする。

## ソース規則

- 拡張子 `.cs`、UTF-8 **BOM 付き**（Framework csc が UTF-8 を安定して読むため）
- C# 5 のみ。`/langversion` を上げる手段が無い前提で書く
- `async`/`await` は可。`catch`/`finally` 内の `await` は C# 6 なので不可

## テスト

`tests/WindowsIDE.Tests` は NUnit 無しのコンソールランナー。`build/compile-tests.ps1` と `build/windows-ide-tests.rsp` で `build/out/WindowsIDE.Tests.exe` を出す（`/target:exe`）。製品の `Program.cs` は含めない。

検証コマンド（これ以外のコンパイラを使わない）:

```text
powershell.exe -NoProfile -File .\build\compile.ps1
powershell.exe -NoProfile -File .\build\compile-tests.ps1
.\build\out\WindowsIDE.Tests.exe
```

## 検証

`compile.ps1` は次を行う。

1. 固定パスの csc を使い、バナー（`for C# 5`）をログする
2. BCL が 4.8 ファミリーであることを検査し FileVersion をログする
3. 同梱フォント 3 ファイルが無ければ失敗する
4. 出力が x64 PE である（スクリプトが PE ヘッダを読む）
5. 出力フォルダに第三者 DLL が増えていない
6. 埋め込みリソース名 `WindowsIDE.Fonts.CascadiaMonoRegular` / `CascadiaMonoBold` / `SourceHanSansJpRegular` がある
7. `app.config` を `WindowsIDE.exe.config` としてコピーする

製品ビルドの正は Windows 上の `build/compile.ps1` と指定 `csc.exe` である。Linux Cloud Agent にはその `csc.exe` が無い。Cursor 用の `scripts/check_sources.py` は、製品 C# の UTF-8 BOM、同梱フォント 3 ファイルと Cascadia Code の不在、`build/*.rsp` の `/r:` 範囲とソース列挙、NuGet 痕跡を見る。PowerShell 7 構文は警告のみとする。このスクリプトは製品 EXE に入れない。csc バナー、BCL の 4.8 ファミリー検査と FileVersion ログ、x64 PE、埋め込みリソース名の検査は `compile.ps1` に残す。
