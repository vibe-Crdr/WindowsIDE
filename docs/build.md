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

ファイルバージョンが 4.8.9337 / 4.8.9340 からずれたら、ビルドを失敗させて理由を出す（サイレント継続しない）。

## P0 の `/r`（これだけ）

すべて `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\` のフルパス。

- `mscorlib.dll`
- `System.dll`
- `System.Core.dll`
- `System.Drawing.dll`
- `System.Windows.Forms.dll`
- `System.Xml.dll`

P0 では `Microsoft.CSharp.dll` / Office PIA / `System.Xml.Linq.dll` は足さない。後のフェーズで足すときは [decisions.md](decisions.md) を更新してから。

P13: PowerShell ハイライト用にだけ次を `/r` する（実行ホストではない）。`WindowsIDE.Host.PowerShell` は `powershell.exe` 5.1 の子プロセスであり、この SMA 参照で Runspace を開かない。

```text
C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Management.Automation\v4.0_3.0.0.0__31bf3856ad364e35\System.Management.Automation.dll
```

`WindowsIDE.Languages` のソースを `build/windows-ide.rsp` と `build/windows-ide-tests.rsp` に列挙する（`IdentifierClassifier.cs` と `Languages/CSharp/` を含む）。Languages は Theme / WinForms を参照しない。BCL 型名は実行時 `Assembly.LoadFrom`（コンパイル `/r` は既存 Framework DLL + SMA）。

ユーザーコードの常時コンパイル（F-LIVE、フェーズ P7。今は実装しない）も同じ Framework `csc.exe` を使う。製品の `/r` は増やさない。Framework XML ドキュメントは DLL 隣の `.xml` をディスクから読む（`System.Xml` は既に製品 `/r` 済み）。新しい `/r` は出さない。

## ユーザー手動 csc（F-CS-BLD）

製品の `compile.ps1`（`/target:winexe`、SMA、フォント `/resource`）と混同しない。IDE がユーザー `.cs` を手動ビルドするときだけ、同じ指定 `csc.exe` を別プロセスで呼ぶ。

- コマンドライン先頭は必ず `/noconfig`。rsp には `/noconfig` を書かない（rsp 内は無視され CS2023）
- rsp（UTF-8 BOM、TEMP）: `/nostdlib /platform:x64 /target:exe /debug+ /utf8output`
- `/r:` は Framework64 の 6 DLL のみ（mscorlib, System, System.Core, System.Drawing, System.Windows.Forms, System.Xml）。SMA / Microsoft.CSharp / Office は足さない。製品 `/r` は増やさない
- `/out` は `%TEMP%\WindowsIDE\build\manual\<key>\out.exe`（`<key>` はワークスペース根または単一ファイルのフルパスを OrdinalIgnoreCase で SHA1 短縮）。PDB 可。生成物は消さない。手動 csc は **`Process.Start` しない**
- ユーザー EXE の起動と、そのプロセス参照だけの Kill は `WindowsIDE.Host.Csharp`。再実行・手動 csc の直前・MainForm.Dispose で Kill する。プロセス名検索はしない
- ユーザー `.ps1` の起動は `WindowsIDE.Host.PowerShell`（`SpecialFolder.System` + `WindowsPowerShell\v1.0\powershell.exe` 子プロセス）。SMA は実行ホストではない。再実行・手動 csc の直前・MainForm.Dispose で Kill する。プロセス名検索はしない
- 無題は対象外。csproj は作らない

## レスポンスファイル

`build/windows-ide.rsp` にスイッチと `/r` とソース一覧と `/resource` を置く。`build/compile.ps1` は csc のフルパスと rsp だけを渡す。PowerShell 7 構文は使わない。`src/WindowsIDE/Ui/CommonItemDialog.cs` を rsp に含める。ole32 / shell32 の P/Invoke に追加 `/r` は不要。`src/WindowsIDE/Host/PowerShell/PowerShellProcessHost.cs` を `Host/Csharp/CsharpProcessHost.cs` の次に列挙する（`windows-ide-tests.rsp` も同じ）。

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
2. BCL ファイルバージョンを検査する
3. 同梱フォント 3 ファイルが無ければ失敗する
4. 出力が x64 PE である（スクリプトが PE ヘッダを読む）
5. 出力フォルダに第三者 DLL が増えていない
6. 埋め込みリソース名 `WindowsIDE.Fonts.CascadiaMonoRegular` / `CascadiaMonoBold` / `SourceHanSansJpRegular` がある
7. `app.config` を `WindowsIDE.exe.config` としてコピーする

製品ビルドの正は Windows 上の `build/compile.ps1` と指定 `csc.exe` である。Linux Cloud Agent にはその `csc.exe` が無い。Cursor 用の `scripts/check_sources.py` は、製品 C# の UTF-8 BOM、同梱フォント 3 ファイルと Cascadia Code の不在、`build/*.rsp` の `/r:` 範囲とソース列挙、NuGet 痕跡を見る。PowerShell 7 構文は警告のみとする。このスクリプトは製品 EXE に入れない。csc バナー、BCL ファイルバージョン、x64 PE、埋め込みリソース名の検査は `compile.ps1` に残す。
