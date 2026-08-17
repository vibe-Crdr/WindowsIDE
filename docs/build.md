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

P0 では `System.Management.Automation.dll` / `Microsoft.CSharp.dll` / Office PIA / `System.Xml.Linq.dll` は足さない。後のフェーズで足すときは [decisions.md](decisions.md) を更新してから。

## レスポンスファイル

`build/windows-ide.rsp` にスイッチと `/r` とソース一覧と `/resource` を置く。`build/compile.ps1` は csc のフルパスと rsp だけを渡す。PowerShell 7 構文は使わない。`src/WindowsIDE/Ui/CommonItemDialog.cs` を rsp に含める。ole32 / shell32 の P/Invoke に追加 `/r` は不要。

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
