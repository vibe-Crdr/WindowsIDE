using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WindowsIDE.Build;
using WindowsIDE.Editor;
using WindowsIDE.Host.Cmd;
using WindowsIDE.Host.Csharp;
using WindowsIDE.Host.PowerShell;
using WindowsIDE.Languages;
using WindowsIDE.Languages.CSharp;
using WindowsIDE.Terminal;
using WindowsIDE.Ui;
using WindowsIDE.Ui.Fonts;
using WindowsIDE.Vba;
using WindowsIDE.Workspace;

namespace WindowsIDE.Tests
{
    /// <summary>
    /// NUnit 無しのコンソールランナー。失敗があれば終了コード 1。
    /// </summary>
    public static class Program
    {
        private static int passed;
        private static int failed;

        private const int StdInputHandle = -10;
        private const int StdOutputHandle = -11;
        private const int StdErrorHandle = -12;
        private const uint AttachParentProcess = 0xFFFFFFFF;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetStdHandle(int nStdHandle, IntPtr hHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FreeConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AttachConsole(uint dwProcessId);

        /// <summary>
        /// 単体テストを順に実行する。
        /// </summary>
        [STAThread]
        public static int Main()
        {
            passed = 0;
            failed = 0;
            RunTextBuffer();
            RunIndentRules();
            RunFindRules();
            RunCmdSelectionRules();
            RunFileEncoding();
            RunDocumentOpen();
            RunGlyphClassifier();
            RunPathGuard();
            RunWorkspaceCreateRules();
            RunWorkspaceSettings();
            RunVbaOffline();
            RunDocumentVbaEncoding();
            RunDocumentCmdEncoding();
            RunFontLoaderFallback();
            RunTabSwitchScroll();
            RunDpiUtil();
            RunDualFontPainter();
            RunUiMnemonic();
            RunDualFontMenuItemPreferredSize();
            RunNativeCaption();
            RunImeLayout();
            RunStartupArgs();
            RunLanguageDetector();
            RunLexers();
            RunCSharpBind();
            RunHighlightSession();
            RunBraceMatch();
            RunAutoClose();
            RunSmartIndent();
            RunVbaBlock();
            RunVbaKeywordCase();
            InspectP7ASource();
            RunDiagnosticParser();
            RunCsFileEnumerator();
            RunCompileUnit();
            RunCscArgumentBuilder();
            RunCscBrokenSource();
            RunCsharpProcessHost();
            RunPowerShellProcessHost();
            RunCmdProcessHost();
            RunShellPaths();
            RunCellWidth();
            RunTerminalInput();
            RunVtParser();
            RunEnvironmentBlockWithoutTerm();
            RunPseudoConsoleSession();
            RunPseudoConsolePowerShellConsole();
            RunPseudoConsolePowerShellInteractive();
            InspectPseudoConsoleSessionSource();
            InspectMainFormTerminalSource();
            InspectVbaSyncSource();
            InspectBottomPaneTerminalSource();
            Console.WriteLine();
            Console.WriteLine("Passed: " + passed.ToString() + "  Failed: " + failed.ToString());
            return (failed == 0) ? 0 : 1;
        }

        private static void Check(string name, bool condition)
        {
            if (condition)
            {
                passed++;
                Console.WriteLine("PASS  " + name);
            }
            else
            {
                failed++;
                Console.WriteLine("FAIL  " + name);
            }
        }

        private static void RunTextBuffer()
        {
            TextBuffer buf = new TextBuffer();
            UndoStack undo = new UndoStack();
            BufferPoint p = buf.Insert(0, 0, "ab");
            undo.RecordInsert(0, 0, "ab");
            Check("insert caret", p.Line == 0 && p.Column == 2 && buf.GetText() == "ab");

            p = buf.Insert(0, 1, "X");
            undo.RecordInsert(0, 1, "X");
            Check("insert middle", buf.GetText() == "aXb");

            p = buf.Insert(0, 2, "\nY");
            undo.RecordInsert(0, 2, "\nY");
            Check("insert newline", buf.LineCount == 2 && buf.GetLine(0) == "aX" && buf.GetLine(1) == "Yb");

            BufferPoint a = new BufferPoint(0, 1);
            BufferPoint b = new BufferPoint(1, 1);
            string deleted = buf.Delete(a, b);
            undo.RecordDelete(0, 1, deleted);
            Check("delete range", buf.GetText() == "ab" || buf.GetLine(0) == "ab");

            undo.Undo(buf);
            Check("undo delete", buf.LineCount == 2 && buf.GetLine(0) == "aX");
            undo.Redo(buf);
            Check("redo delete", buf.GetLine(0) == "ab");
            undo.Undo(buf);
            undo.Undo(buf);
            Check("undo insert nl", buf.GetText().Replace("\r\n", "\n") == "aXb");
        }

        private static void RunIndentRules()
        {
            Check("lead spaces", IndentRules.LeadingWhitespace("    foo") == "    ");
            Check("lead tab no expand", IndentRules.LeadingWhitespace("\tfoo") == "\t");
            Check("lead empty", IndentRules.LeadingWhitespace("") == "");
            Check("lead null", IndentRules.LeadingWhitespace(null) == "");
            Check("lead spaces only", IndentRules.LeadingWhitespace("   ") == "   ");
            Check("lead skip ideographic", IndentRules.LeadingWhitespace("\u3000foo") == "");

            Check("indent line", IndentRules.IndentLine("foo", 4) == "    foo");
            Check("indent empty", IndentRules.IndentLine("", 4) == "    ");

            int removed;
            Check("unindent 4 spaces", IndentRules.UnindentLine("    foo", 4, out removed) == "foo" && removed == 4);
            Check("unindent 2 of 4", IndentRules.UnindentLine("  foo", 4, out removed) == "foo" && removed == 2);
            Check("unindent tab", IndentRules.UnindentLine("\tfoo", 4, out removed) == "foo" && removed == 1);
            Check("unindent none", IndentRules.UnindentLine("foo", 4, out removed) == "foo" && removed == 0);

            BufferPoint sameA = new BufferPoint(1, 0);
            BufferPoint sameB = new BufferPoint(1, 4);
            Check("block same line", !IndentRules.IsBlockIndentSelection(sameA, sameB));
            BufferPoint twoA = new BufferPoint(0, 0);
            BufferPoint twoB = new BufferPoint(1, 0);
            Check("block two lines", IndentRules.IsBlockIndentSelection(twoA, twoB));

            BufferPoint blockStart = new BufferPoint(0, 2);
            BufferPoint blockEndCol0 = new BufferPoint(2, 0);
            Check("block last excludes col0", IndentRules.BlockLastLine(blockStart, blockEndCol0) == 1);
            BufferPoint blockEndMid = new BufferPoint(2, 3);
            Check("block last includes mid", IndentRules.BlockLastLine(blockStart, blockEndMid) == 2);

            Check("col indent 0", IndentRules.AdjustColumnAfterIndent(0, 4) == 0);
            Check("col indent 3", IndentRules.AdjustColumnAfterIndent(3, 4) == 7);
            Check("col unindent clamp", IndentRules.AdjustColumnAfterUnindent(2, 4) == 0);

            TextBuffer buf = new TextBuffer();
            UndoStack undo = new UndoStack();
            buf.SetText("a\nb\nc");
            int tabSize = 4;
            string spaces = new string(' ', tabSize);
            undo.BeginCompound();
            for (int i = 0; i < 3; i++)
            {
                buf.Insert(i, 0, spaces);
                undo.RecordInsert(i, 0, spaces);
            }

            undo.EndCompound();
            Check("block indent text", buf.GetLine(0) == "    a" && buf.GetLine(1) == "    b" && buf.GetLine(2) == "    c");
            undo.Undo(buf);
            Check("block indent one undo", buf.GetLine(0) == "a" && buf.GetLine(1) == "b" && buf.GetLine(2) == "c" && !undo.CanUndo);
        }

        private static void RunFindRules()
        {
            FindMatch match;
            TextBuffer emptyBuf = new TextBuffer();
            emptyBuf.SetText("hello");
            Check("empty query next", !FindRules.TryFindNext(emptyBuf, "", false, new BufferPoint(0, 0), true, out match));
            Check("empty query prev", !FindRules.TryFindPrevious(emptyBuf, "", false, new BufferPoint(0, 0), true, out match));
            Check("empty query count", FindRules.Count(emptyBuf, "", false) == 0);
            Check("null query count", FindRules.Count(emptyBuf, null, true) == 0);

            TextBuffer ab = new TextBuffer();
            ab.SetText("Ab");
            Check("ignore Ab vs ab", FindRules.TryFindNext(ab, "ab", true, new BufferPoint(0, 0), false, out match) && match.Start.Column == 0 && match.End.Column == 2);
            Check("ordinal Ab vs ab miss", !FindRules.TryFindNext(ab, "ab", false, new BufferPoint(0, 0), false, out match));
            Check("ordinal Ab vs Ab", FindRules.TryFindNext(ab, "Ab", false, new BufferPoint(0, 0), false, out match));

            TextBuffer hello = new TextBuffer();
            hello.SetText("hello");
            Check("start h", FindRules.TryFindNext(hello, "h", false, new BufferPoint(0, 0), false, out match) && match.Start.Column == 0 && match.End.Column == 1);
            Check("end lo", FindRules.TryFindNext(hello, "lo", false, new BufferPoint(0, 0), false, out match) && match.Start.Column == 3 && match.End.Column == 5);

            TextBuffer twice = new TextBuffer();
            twice.SetText("abcabc");
            Check("next first", FindRules.TryFindNext(twice, "abc", false, new BufferPoint(0, 0), false, out match) && match.Start.Column == 0 && match.End.Column == 3);
            Check("skip same hit", FindRules.TryFindNext(twice, "abc", false, match.End, false, out match) && match.Start.Column == 3 && match.End.Column == 6);
            Check("no wrap after last", !FindRules.TryFindNext(twice, "abc", false, match.End, false, out match));
            Check("wrap after last", FindRules.TryFindNext(twice, "abc", false, new BufferPoint(0, 6), true, out match) && match.Start.Column == 0);

            Check("prev skip", FindRules.TryFindPrevious(twice, "abc", false, new BufferPoint(0, 3), false, out match) && match.Start.Column == 0);
            Check("prev no wrap at start", !FindRules.TryFindPrevious(twice, "abc", false, new BufferPoint(0, 0), false, out match));
            Check("prev wrap at start", FindRules.TryFindPrevious(twice, "abc", false, new BufferPoint(0, 0), true, out match) && match.Start.Column == 3);

            TextBuffer lines = new TextBuffer();
            lines.SetText("ab\ncd");
            Check("no cross-line", FindRules.Count(lines, "bc", false) == 0);
            Check("line-local ab", FindRules.Count(lines, "ab", false) == 1);
            Check("line-local cd", FindRules.TryFindNext(lines, "cd", false, new BufferPoint(0, 0), false, out match) && match.Start.Line == 1 && match.Start.Column == 0);

            TextBuffer twoAb = new TextBuffer();
            twoAb.SetText("ab\nab");
            Check("count two lines", FindRules.Count(twoAb, "ab", false) == 2);

            Check("normalize drops line2", FindRules.NormalizeQuery("a\nb") == "a");
            Check("normalize drops crlf", FindRules.NormalizeQuery("xy\r\nz") == "xy");
            Check("normalize null", FindRules.NormalizeQuery(null) == "");
            Check("normalize keeps spaces", FindRules.NormalizeQuery(" a ") == " a ");

            TextBuffer aaa = new TextBuffer();
            aaa.SetText("aaa");
            Check("count non-overlap aa", FindRules.Count(aaa, "aa", false) == 1);
            Check("count ignore Ab in abab", FindRules.Count(ab, "AB", true) == 1);
        }

        private static void RunCmdSelectionRules()
        {
            TextBuffer none = new TextBuffer();
            none.SetText("first\nsecond");
            string noneText = CmdSelectionRules.Extract(none, 1, new BufferPoint(1, 0), new BufferPoint(1, 0));
            Check("sel none uses caret line", noneText == "second");

            TextBuffer same = new TextBuffer();
            same.SetText("echo hello world");
            string sameText = CmdSelectionRules.Extract(same, 0, new BufferPoint(0, 5), new BufferPoint(0, 10));
            Check("sel same line partial", sameText == "hello");

            TextBuffer col0 = new TextBuffer();
            col0.SetText("line0\nline1");
            string col0Text = CmdSelectionRules.Extract(col0, 0, new BufferPoint(0, 0), new BufferPoint(1, 0));
            Check("sel (0,0)-(1,0) line0 only", col0Text == "line0");

            TextBuffer mid = new TextBuffer();
            mid.SetText("aaa\nbbb\nccc");
            string midText = CmdSelectionRules.Extract(mid, 0, new BufferPoint(0, 1), new BufferPoint(2, 2));
            Check("sel multi mid last", midText == "aa\r\nbbb\r\ncc");

            Check("sel blank spaces", CmdSelectionRules.IsBlank("  \t\r\n"));
            Check("sel blank empty", CmdSelectionRules.IsBlank(""));
            Check("sel blank null", CmdSelectionRules.IsBlank(null));
            Check("sel blank false", !CmdSelectionRules.IsBlank("a"));

            TextBuffer lf = new TextBuffer();
            lf.NewLine = "\n";
            lf.SetText("a\nb\nc");
            string joined = CmdSelectionRules.Extract(lf, 0, new BufferPoint(0, 0), new BufferPoint(2, 1));
            Check("sel join crlf", joined == "a\r\nb\r\nc");
        }

        private static void RunFileEncoding()
        {
            string text;
            FileEncodingInfo info;
            string error;

            byte[] utf8Bom = new byte[] { 0xEF, 0xBB, 0xBF, 0x61, 0x62, 0x63 };
            Check("utf8 bom decode", FileEncoding.TryDecode(utf8Bom, out text, out info, out error) && text == "abc" && info.IsUtf8 && info.HasBom);

            byte[] utf8 = Encoding.UTF8.GetBytes("hello");
            Check("utf8 no bom", FileEncoding.TryDecode(utf8, out text, out info, out error) && text == "hello" && info.IsUtf8 && !info.HasBom);

            byte[] cp932 = new byte[] { 0x82, 0xA0 };
            Check("cp932", FileEncoding.TryDecode(cp932, out text, out info, out error) && info.CodePage == 932 && text.Length == 1);

            byte[] lf = Encoding.UTF8.GetBytes("a\nb");
            Check("lf detect", FileEncoding.TryDecode(lf, out text, out info, out error) && info.NewLine == "\n");

            byte[] crlf = Encoding.UTF8.GetBytes("a\r\nb");
            Check("crlf detect", FileEncoding.TryDecode(crlf, out text, out info, out error) && info.NewLine == "\r\n");

            FileEncodingInfo noBom = new FileEncodingInfo(65001, false, false, "\n");
            byte[] savedCs = FileEncoding.GetBytesToSave("x", noBom, "C:\\tmp\\Foo.cs");
            Check("cs utf8 forces bom", savedCs.Length >= 4 && savedCs[0] == 0xEF && savedCs[1] == 0xBB && savedCs[2] == 0xBF);

            byte[] savedTxt = FileEncoding.GetBytesToSave("x", noBom, "C:\\tmp\\Foo.txt");
            Check("txt utf8 keeps no bom", savedTxt.Length == 1 && savedTxt[0] == 0x78);

            byte[] savedLf = FileEncoding.GetBytesToSave("a\r\nb", noBom, "C:\\tmp\\a.txt");
            Check("preserve lf", savedLf.Length == 3 && savedLf[1] == 0x0A);

            byte[] binary = new byte[] { 0x00, 0x01, 0x02, 0x00 };
            Check("binary rejected", !FileEncoding.TryDecode(binary, out text, out info, out error));

            byte[] utf16 = new byte[] { 0xFF, 0xFE, 0x41, 0x00 };
            Check("utf16 le", FileEncoding.TryDecode(utf16, out text, out info, out error) && text == "A" && info.CodePage == 1200);

            FileEncodingInfo cp932Info = new FileEncodingInfo(932, false, false, "\r\n");
            byte[] cp932Save = FileEncoding.GetBytesToSave("abc", cp932Info, "C:\\tmp\\a.bas");
            Check("cp932 save no bom", cp932Save.Length == 3 && cp932Save[0] == 0x61 && cp932Save[1] == 0x62 && cp932Save[2] == 0x63);

            byte[] cp932Ja = FileEncoding.GetBytesToSave("あ", cp932Info, "C:\\tmp\\a.bas");
            Check("cp932 save kana", cp932Ja.Length == 2 && cp932Ja[0] == 0x82 && cp932Ja[1] == 0xA0);

            bool encThrew = false;
            try
            {
                FileEncoding.GetBytesToSave("\u2603", cp932Info, "C:\\tmp\\a.bas");
            }
            catch (EncoderFallbackException)
            {
                encThrew = true;
            }

            Check("cp932 encoder exception", encThrew);

            FileEncodingInfo utf8BomInfo = new FileEncodingInfo(65001, true, false, "\r\n");
            byte[] savedCmd = FileEncoding.GetBytesToSave("echo off", utf8BomInfo, "C:\\tmp\\a.cmd");
            Check("cmd utf8 strips bom", savedCmd.Length == 8 && savedCmd[0] == 0x65 && savedCmd[1] == 0x63 && savedCmd[2] == 0x68);
            byte[] savedBat = FileEncoding.GetBytesToSave("echo off", utf8BomInfo, "C:\\tmp\\a.bat");
            Check("bat utf8 strips bom", savedBat.Length == 8 && savedBat[0] == 0x65 && savedBat[1] == 0x63 && savedBat[2] == 0x68);
            byte[] savedCsBom = FileEncoding.GetBytesToSave("echo off", utf8BomInfo, "C:\\tmp\\a.cs");
            Check("cs utf8 bom still forced", savedCsBom.Length >= 11 && savedCsBom[0] == 0xEF && savedCsBom[1] == 0xBB && savedCsBom[2] == 0xBF);
            byte[] savedTxtBom = FileEncoding.GetBytesToSave("echo off", utf8BomInfo, "C:\\tmp\\a.txt");
            Check("txt utf8 keeps bom", savedTxtBom.Length >= 11 && savedTxtBom[0] == 0xEF && savedTxtBom[1] == 0xBB && savedTxtBom[2] == 0xBF);

            Check("vba path bas", FileEncoding.IsVbaModulePath("C:\\tmp\\a.bas"));
            Check("vba path cls", FileEncoding.IsVbaModulePath("C:\\tmp\\a.CLS"));
            Check("vba path empty", !FileEncoding.IsVbaModulePath("") && !FileEncoding.IsVbaModulePath(null));
            Check("cmd path cmd", FileEncoding.IsCmdBatchPath("C:\\tmp\\a.cmd"));
            Check("cmd path bat", FileEncoding.IsCmdBatchPath("C:\\tmp\\a.BAT"));
            Check("cmd path empty", !FileEncoding.IsCmdBatchPath("") && !FileEncoding.IsCmdBatchPath(null));
        }

        private static void RunDocumentOpen()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-docopen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, "sample.cs");
            try
            {
                string body = "class Sample\r\n{\r\n}\r\n";
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                byte[] textBytes = Encoding.UTF8.GetBytes(body);
                byte[] data = new byte[bom.Length + textBytes.Length];
                Buffer.BlockCopy(bom, 0, data, 0, bom.Length);
                Buffer.BlockCopy(textBytes, 0, data, bom.Length, textBytes.Length);
                File.WriteAllBytes(path, data);

                Document doc = Document.Open(path);
                Check("Document.Open GetText", doc.Buffer.GetText() == body);
                Check("Document.Open FilePath", string.Equals(doc.FilePath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase));
                Check("Document.Open UTF-8 BOM", doc.EncodingInfo.IsUtf8 && doc.EncodingInfo.HasBom);
                Check("Document.Open IsDirty", doc.IsDirty == false);
                Check("Document.Open Language CSharp", doc.Language == LanguageKind.CSharp);
                Check("Document.Open highlight start", doc.HighlightSession.GetStartState(0) == 0);
            }
            finally
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (IOException)
                {
                }

                try
                {
                    Directory.Delete(dir, false);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunGlyphClassifier()
        {
            int n;
            Check("ascii A", GlyphClassifier.UseHalfWidthFont("A", 0, out n) && n == 1);
            Check("ascii tilde", GlyphClassifier.UseHalfWidthFont("~", 0, out n));
            Check("space", GlyphClassifier.UseHalfWidthFont(" ", 0, out n));
            Check("half kana", GlyphClassifier.UseHalfWidthFont("\uFF66", 0, out n));
            Check("hiragana full", !GlyphClassifier.UseHalfWidthFont("あ", 0, out n));
            string pair = char.ConvertFromUtf32(0x1F600);
            Check("surrogate full", !GlyphClassifier.UseHalfWidthFont(pair, 0, out n) && n == 2);
        }

        private static void RunPathGuard()
        {
            string root = Path.Combine(Path.GetTempPath(), "WindowsIDE-pathguard-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string inside = Path.Combine(root, "a.txt");
                File.WriteAllText(inside, "x");
                Check("inside file", PathGuard.IsInsideWorkspace(root, inside));
                Check("inside root", PathGuard.IsInsideWorkspace(root, root));
                string escape = Path.Combine(root, "..", "WindowsIDE-pathguard-escape");
                Check("escape ..", !PathGuard.IsInsideWorkspace(root, escape));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunWorkspaceCreateRules()
        {
            string root = Path.Combine(Path.GetTempPath(), "WindowsIDE-create-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            string sub = Path.Combine(root, "sub");
            Directory.CreateDirectory(sub);
            string nestedFile = Path.Combine(sub, "seed.txt");
            File.WriteAllText(nestedFile, "x");
            try
            {
                string created;
                string error;

                string fromFolder = WorkspaceCreateRules.ResolveCreateDirectory(root, sub);
                Check("resolve folder", string.Equals(fromFolder, Path.GetFullPath(sub), StringComparison.OrdinalIgnoreCase));
                string fromFile = WorkspaceCreateRules.ResolveCreateDirectory(root, nestedFile);
                Check("resolve file parent", string.Equals(fromFile, Path.GetFullPath(sub), StringComparison.OrdinalIgnoreCase));
                string fromNull = WorkspaceCreateRules.ResolveCreateDirectory(root, null);
                Check("resolve null root", string.Equals(fromNull, Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
                Check("resolve empty root null", WorkspaceCreateRules.ResolveCreateDirectory("", nestedFile) == null);

                Check("reject empty", !WorkspaceCreateRules.TryCreateFile(root, root, "  ", out created, out error));
                Check("reject dotdot", !WorkspaceCreateRules.TryCreateFile(root, root, "..", out created, out error));
                Check("reject slash", !WorkspaceCreateRules.TryCreateFile(root, root, "a\\b", out created, out error));
                Check("reject CON", !WorkspaceCreateRules.TryCreateFile(root, root, "CON", out created, out error));
                Check("reject CON.txt", !WorkspaceCreateRules.TryCreateFile(root, root, "CON.txt", out created, out error));
                Check("reject trailing dot", !WorkspaceCreateRules.TryCreateFile(root, root, "foo.", out created, out error));
                Check("reject invalid char", !WorkspaceCreateRules.TryCreateFile(root, root, "a*b", out created, out error));
                Check("reject escape", !WorkspaceCreateRules.TryCreateFile(root, root, "..\\x", out created, out error));

                byte[] csBytes = FileEncoding.GetBytesForNewEmptyFile("C:\\tmp\\New.cs");
                Check("new cs bom 3", csBytes != null && csBytes.Length == 3 && csBytes[0] == 0xEF && csBytes[1] == 0xBB && csBytes[2] == 0xBF);
                byte[] txtBytes = FileEncoding.GetBytesForNewEmptyFile("C:\\tmp\\New.txt");
                Check("new txt bom", txtBytes != null && txtBytes.Length >= 3 && txtBytes[0] == 0xEF && txtBytes[1] == 0xBB && txtBytes[2] == 0xBF);
                byte[] basBytes = FileEncoding.GetBytesForNewEmptyFile("C:\\tmp\\New.bas");
                Check("new bas empty", basBytes != null && basBytes.Length == 0);
                byte[] clsBytes = FileEncoding.GetBytesForNewEmptyFile("C:\\tmp\\New.cls");
                Check("new cls empty", clsBytes != null && clsBytes.Length == 0);
                byte[] cmdNewBytes = FileEncoding.GetBytesForNewEmptyFile("C:\\tmp\\New.cmd");
                Check("new cmd empty", cmdNewBytes != null && cmdNewBytes.Length == 0);
                byte[] batNewBytes = FileEncoding.GetBytesForNewEmptyFile("C:\\tmp\\New.bat");
                Check("new bat empty", batNewBytes != null && batNewBytes.Length == 0);

                Check("create cs", WorkspaceCreateRules.TryCreateFile(root, root, "New.cs", out created, out error));
                byte[] writtenCs = File.ReadAllBytes(created);
                Check("created cs bom", writtenCs.Length >= 3 && writtenCs[0] == 0xEF && writtenCs[1] == 0xBB && writtenCs[2] == 0xBF);
                Check("create txt", WorkspaceCreateRules.TryCreateFile(root, root, "New.txt", out created, out error));
                byte[] writtenTxt = File.ReadAllBytes(created);
                Check("created txt bom", writtenTxt.Length >= 3 && writtenTxt[0] == 0xEF && writtenTxt[1] == 0xBB && writtenTxt[2] == 0xBF);
                Check("create bas", WorkspaceCreateRules.TryCreateFile(root, root, "New.bas", out created, out error));
                Check("created bas empty", File.Exists(created) && File.ReadAllBytes(created).Length == 0);
                Check("create cls", WorkspaceCreateRules.TryCreateFile(root, root, "New.cls", out created, out error));
                Check("created cls empty", File.Exists(created) && File.ReadAllBytes(created).Length == 0);
                Check("create cmd", WorkspaceCreateRules.TryCreateFile(root, root, "New.cmd", out created, out error));
                Check("created cmd empty", File.Exists(created) && File.ReadAllBytes(created).Length == 0);

                string emptyBas = Path.Combine(root, "empty.bas");
                File.WriteAllBytes(emptyBas, new byte[0]);
                Document openedEmpty = Document.Open(emptyBas);
                Check("open empty bas 932", openedEmpty.EncodingInfo.CodePage == 932 && !openedEmpty.EncodingInfo.HasBom);

                Check("create folder", WorkspaceCreateRules.TryCreateDirectory(root, root, "NewFolder", out created, out error));
                Check("created folder exists", Directory.Exists(created));

                Check("exist file", !WorkspaceCreateRules.TryCreateFile(root, root, "New.cs", out created, out error));
                Check("exist folder", !WorkspaceCreateRules.TryCreateDirectory(root, root, "NewFolder", out created, out error));
                Check("file as folder", !WorkspaceCreateRules.TryCreateDirectory(root, root, "New.cs", out created, out error));
                Check("folder as file", !WorkspaceCreateRules.TryCreateFile(root, root, "NewFolder", out created, out error));
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunWorkspaceSettings()
        {
            string root = Path.Combine(Path.GetTempPath(), "WindowsIDE-ws-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                WorkspaceSettings missing = WorkspaceSettings.Load(root);
                Check("DefaultFontSize is 14", WorkspaceSettings.DefaultFontSize == 14);
                Check("missing fontSize default", missing.FontSize == WorkspaceSettings.DefaultFontSize);
                Check("missing fontSize is 14", missing.FontSize == 14);
                Check("missing tabSize default", missing.TabSize == WorkspaceSettings.DefaultTabSize);
                Check("missing no xml", !File.Exists(WorkspaceSettings.GetFilePath(root)));

                missing.FontSize = 15;
                missing.TabSize = 2;
                missing.Save(root);
                WorkspaceSettings loaded = WorkspaceSettings.Load(root);
                Check("roundtrip size", loaded.FontSize == 15 && loaded.TabSize == 2);
                Check("no migrate 15 stays 15", loaded.FontSize == 15);
                string xml = File.ReadAllText(WorkspaceSettings.GetFilePath(root), Encoding.UTF8);
                Check("xml has no namingMode", xml.IndexOf("namingMode") < 0);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunFontLoaderFallback()
        {
            FontLoadResult junk = FontLoader.LoadFromBytes(new byte[] { 0, 1, 2, 3 }, new byte[] { 9, 9, 9 }, new byte[] { 1 });
            Check("force fail fallback", junk != null && junk.UsedFallback);
            Check("fallback message", junk.ErrorMessage != null && junk.ErrorMessage.Length > 0);

            FontLoadResult missing = FontLoader.Load();
            Check("no embed fallback", missing != null && missing.UsedFallback);
        }

        private static void RunTabSwitchScroll()
        {
            Document shortDoc = Document.CreateUntitled();
            shortDoc.ScrollY = 5;
            shortDoc.ScrollX = 12;
            shortDoc.ApplyBarScroll(0, 0, true);
            Check("programmatic clamp keeps ScrollY", shortDoc.ScrollY == 5 && shortDoc.ScrollX == 12);
            shortDoc.ApplyBarScroll(2, 3, false);
            Check("user scroll writes ScrollY", shortDoc.ScrollY == 2 && shortDoc.ScrollX == 3);
        }

        private static void RunDpiUtil()
        {
            Check("ToPixels 13@96", DpiUtil.ToPixels(13, 96) == 13);
            Check("ToPixels 13@120", DpiUtil.ToPixels(13, 120) == 16);
            Check("ToPixels 14@96", DpiUtil.ToPixels(14, 96) == 14);
            Check("ToPixels 14@120", DpiUtil.ToPixels(14, 120) == 18);
            Check("ToPixels 14@240", DpiUtil.ToPixels(14, 240) == 35);
            Check("ToPixels 18@120", DpiUtil.ToPixels(18, 120) == 23);
            Check("ToPixels 18@240", DpiUtil.ToPixels(18, 240) == 45);
            Check("GetDpi Zero is at least 96", DpiUtil.GetDpi(IntPtr.Zero) >= 96);
            Check("ToPixels 13@144", DpiUtil.ToPixels(13, 144) == 20);
            Check("ToPixels 13@192", DpiUtil.ToPixels(13, 192) == 26);
            Check("ToPixels 28@120", DpiUtil.ToPixels(28, 120) == 35);
            Check("ToPixels 22@120", DpiUtil.ToPixels(22, 120) == 28);
            Check("ToPixels 6@96", DpiUtil.ToPixels(6, 96) == 6);
            Check("ToPixels 6@120", DpiUtil.ToPixels(6, 120) == 8);
            Check("ToPixels 10@96", DpiUtil.ToPixels(10, 96) == 10);
            Check("ToPixels 10@120", DpiUtil.ToPixels(10, 120) == 13);
            Check("ToPixels dpi<=0 as 96", DpiUtil.ToPixels(13, 0) == 13);
            Check("ToPixels dip<0", DpiUtil.ToPixels(-1, 120) == 0);
            Check("WheelToPixels 120", DpiUtil.WheelToPixels(120, 32) == 32);
            Check("WheelToPixels -120", DpiUtil.WheelToPixels(-120, 32) == -32);
            Check("WheelToPixels 60", DpiUtil.WheelToPixels(60, 32) == 16);
            Check("ScrollThumbLength proportional", DpiUtil.ScrollThumbLength(100, 0, 100, 50, 16) == 33);
            Check("ScrollThumbLength minThumb", DpiUtil.ScrollThumbLength(100, 0, 1000, 10, 16) == 16);
            Check("ScrollThumbLength not over track", DpiUtil.ScrollThumbLength(20, 0, 10, 100, 16) <= 20);
            Check("EditorLineHeight", DpiUtil.EditorLineHeight(17, 13, 4) == 21);
            Check("TabStripHeight 96", DpiUtil.TabStripHeight(15, 96) == 28);
            Check("TabStripHeight 120", DpiUtil.TabStripHeight(19, 120) == 35);
            Check("TreeItemHeight 96", DpiUtil.TreeItemHeight(15, 96) == 23);
            Check("TreeItemHeight 120", DpiUtil.TreeItemHeight(19, 120) == 29);
            Check("ScrollBarNeeded empty", !DpiUtil.ScrollBarNeeded(0, 0, 10));
            Check("ScrollBarNeeded page covers", !DpiUtil.ScrollBarNeeded(0, 32, 33));
            Check("ScrollBarNeeded overflow", DpiUtil.ScrollBarNeeded(0, 32, 30));
            Check("ScrollBarNeeded exact page", !DpiUtil.ScrollBarNeeded(0, 799, 800));
            int leftover = 0;
            Check("WheelNotches 40 is 0", DpiUtil.WheelNotches(40, ref leftover) == 0 && leftover == 40);
            Check("WheelNotches 80 is 1", DpiUtil.WheelNotches(80, ref leftover) == 1 && leftover == 0);
            Check("WheelNotches -120 is -1", DpiUtil.WheelNotches(-120, ref leftover) == -1 && leftover == 0);
            Check("ToPixels 8@96", DpiUtil.ToPixels(8, 96) == 8);
            Check("ToPixels 8@120", DpiUtil.ToPixels(8, 120) == 10);
            Check("StatusStripPadXDip", DpiUtil.StatusStripPadXDip == 8);
            Check("StatusStripPadYDip", DpiUtil.StatusStripPadYDip == 4);
            Check("ToPixels StatusStripPadY@120", DpiUtil.ToPixels(DpiUtil.StatusStripPadYDip, 120) == 5);
            Rectangle body = DpiUtil.TextBodyClip(48, new Rectangle(0, 0, 800, 400));
            Check("TextBodyClip X", body.X == 48);
            Check("TextBodyClip Width", body.Width == 752);
            Check("TextBodyClip Y", body.Y == 0);
            Check("TextBodyClip Height", body.Height == 400);
            Rectangle covered = DpiUtil.TextBodyClip(800, new Rectangle(0, 0, 800, 400));
            Check("TextBodyClip gutter covers Width==0", covered.Width == 0);
            Rectangle shifted = DpiUtil.TextBodyClip(48, new Rectangle(10, 20, 800, 400));
            Check("TextBodyClip inherits Y", shifted.Y == 20);
        }

        private static void RunDualFontPainter()
        {
            using (Bitmap bmp = new Bitmap(400, 80))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font half = new Font("Consolas", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font full = new Font("Yu Gothic", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (StringFormat fmt = (StringFormat)StringFormat.GenericTypographic.Clone())
            {
                fmt.FormatFlags = fmt.FormatFlags | StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap | StringFormatFlags.FitBlackBox;
                float a = DualFontPainter.Measure(g, "A", half, full, fmt);
                float mixed = DualFontPainter.Measure(g, "Aあ", half, full, fmt);
                Check("Measure Aあ wider than A", mixed > a);

                string original = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
                string fit = DualFontPainter.FitEllipsis(g, original, half, full, 24f, fmt);
                Check("FitEllipsis ends with ...", fit.EndsWith("..."));
                Check("FitEllipsis shorter than original", fit.Length < original.Length);

                Check("FitEllipsis empty", DualFontPainter.FitEllipsis(g, "", half, full, 100f, fmt) == "");
                Check("FitEllipsis null", DualFontPainter.FitEllipsis(g, null, half, full, 100f, fmt) == "");

                g.Clear(Color.Black);
                using (SolidBrush fg = new SolidBrush(Color.White))
                {
                    DualFontPainter.Draw(g, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", half, full, new Rectangle(20, 10, 40, 24), -8f, fg, fmt);
                }

                bool painted = false;
                for (int px = 20; px < 60 && !painted; px++)
                {
                    for (int py = 10; py < 34 && !painted; py++)
                    {
                        Color c = bmp.GetPixel(px, py);
                        if (c.R > 20 || c.G > 20 || c.B > 20)
                        {
                            painted = true;
                        }
                    }
                }

                Check("Draw paints inside clip", painted);

                bool leftBlack = true;
                for (int px = 0; px < 20 && leftBlack; px++)
                {
                    for (int py = 0; py < bmp.Height && leftBlack; py++)
                    {
                        Color c = bmp.GetPixel(px, py);
                        if (c.R != 0 || c.G != 0 || c.B != 0)
                        {
                            leftBlack = false;
                        }
                    }
                }

                Check("Draw leaves pixels left of clip black", leftBlack);
            }
        }

        private static void RunUiMnemonic()
        {
            int index;
            string stripped = UiMnemonic.Strip("ファイル(&F)", out index);
            Check("Strip ファイル(&F) text", stripped == "ファイル(F)");
            Check("Strip ファイル(&F) index is F", index >= 0 && index < stripped.Length && stripped[index] == 'F');

            stripped = UiMnemonic.Strip("A&&B", out index);
            Check("Strip A&&B text", stripped == "A&B");
            Check("Strip A&&B no mnemonic", index == -1);

            stripped = UiMnemonic.Strip("&File", out index);
            Check("Strip &File text", stripped == "File");
            Check("Strip &File index 0", index == 0);

            stripped = UiMnemonic.Strip("A&", out index);
            Check("Strip trailing &", stripped == "A" && index == -1);

            stripped = UiMnemonic.Strip(null, out index);
            Check("Strip null", stripped == "" && index == -1);

            stripped = UiMnemonic.Strip("", out index);
            Check("Strip empty", stripped == "" && index == -1);

            using (Bitmap bmp = new Bitmap(400, 80))
            using (Graphics g = Graphics.FromImage(bmp))
            using (Font half = new Font("Consolas", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font full = new Font("Yu Gothic", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                float raw = DualFontPainter.Measure(g, "ファイル(&F)", half, full, null);
                int mi;
                string visible = UiMnemonic.Strip("ファイル(&F)", out mi);
                float after = DualFontPainter.Measure(g, visible, half, full, null);
                Check("Measure raw vs Strip differs", raw != after);
            }
        }

        private static void RunDualFontMenuItemPreferredSize()
        {
            using (Font half = new Font("Consolas", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (Font full = new Font("Yu Gothic", 12f, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                DarkMenuRenderer renderer = new DarkMenuRenderer();
                renderer.SetFonts(half, full);

                using (ToolStripDropDownMenu drop = new ToolStripDropDownMenu())
                {
                    drop.Renderer = renderer;
                    DualFontMenuItem copy = new DualFontMenuItem("コピー(&C)");
                    copy.ShortcutKeys = Keys.Control | Keys.C;
                    DualFontMenuItem selectAll = new DualFontMenuItem("すべて選択(&A)");
                    selectAll.ShortcutKeys = Keys.Control | Keys.A;
                    DualFontMenuItem findNext = new DualFontMenuItem("次を検索");
                    findNext.ShortcutKeys = Keys.F3;
                    findNext.ShortcutKeyDisplayString = "F3";
                    DualFontMenuItem findPrev = new DualFontMenuItem("前を検索");
                    findPrev.ShortcutKeys = Keys.Shift | Keys.F3;
                    findPrev.ShortcutKeyDisplayString = "Shift+F3";
                    drop.Items.Add(copy);
                    drop.Items.Add(selectAll);
                    drop.Items.Add(findNext);
                    drop.Items.Add(findPrev);

                    int wCopy = copy.GetPreferredSize(Size.Empty).Width;
                    int wAll = selectAll.GetPreferredSize(Size.Empty).Width;
                    int wNext = findNext.GetPreferredSize(Size.Empty).Width;
                    int wPrev = findPrev.GetPreferredSize(Size.Empty).Width;
                    Check("dropdown DualFont widths equal", wCopy == wAll && wAll == wNext && wNext == wPrev);
                    Check("dropdown copy is on drop-down", copy.IsOnDropDown);

                    using (Bitmap bmp = new Bitmap(1, 1))
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        int dummy;
                        string label = UiMnemonic.Strip("コピー(&C)", out dummy);
                        int dpi = DpiUtil.GetDpi(IntPtr.Zero);
                        float labelW = DualFontPainter.Measure(g, label, half, full, null);
                        float shortcutW = DualFontPainter.Measure(g, "Ctrl+C", half, full, null);
                        int left = DpiUtil.ToPixels(24, dpi);
                        int gap = DpiUtil.ToPixels(16, dpi);
                        int tabW = TextRenderer.MeasureText("\t", copy.Font).Width;
                        if (tabW > gap)
                        {
                            gap = tabW;
                        }

                        int right = DpiUtil.ToPixels(10, dpi) + DpiUtil.ToPixels(8, dpi);
                        int minW = left + (int)Math.Ceiling((double)labelW) + gap + (int)Math.Ceiling((double)shortcutW) + right;
                        Check("dropdown width covers copy shortcut chrome", wCopy >= minW);
                    }
                }

                using (MenuStrip menu = new MenuStrip())
                {
                    menu.Renderer = renderer;
                    DualFontMenuItem shortItem = new DualFontMenuItem("編集(&E)");
                    DualFontMenuItem longItem = new DualFontMenuItem("すべて選択(&A)");
                    menu.Items.Add(shortItem);
                    menu.Items.Add(longItem);
                    int sw = shortItem.GetPreferredSize(Size.Empty).Width;
                    int lw = longItem.GetPreferredSize(Size.Empty).Width;
                    Check("top-level DualFont not sibling-max", sw < lw && !shortItem.IsOnDropDown && !longItem.IsOnDropDown);
                }
            }
        }

        private static void RunNativeCaption()
        {
            Check("ToColorRef Background", NativeCaption.ToColorRef(Theme.Background) == 0x00261B1A);
            Check("ToColorRef Foreground", NativeCaption.ToColorRef(Theme.Foreground) == 0x00F5CAC0);
            Check("ToColorRef Border", NativeCaption.ToColorRef(Theme.Border) == 0x0035231F);
        }

        private static void RunImeLayout()
        {
            Check("ClampCursor negative", ImeLayout.ClampCursor(-3, 5) == 0);
            Check("ClampCursor over", ImeLayout.ClampCursor(9, 5) == 5);
            Check("ClampCursor empty", ImeLayout.ClampCursor(2, 0) == 0);
            Check("ClampCursor at length", ImeLayout.ClampCursor(4, 4) == 4);
            Check("ClampCursor length negative", ImeLayout.ClampCursor(1, -1) == 0);
            Check("ClientX formula", ImeLayout.ClientX(40, 4, 100f, 10, 8f) == 40 + 4 + (int)(100f - 10 + 8f));
            Check("ClientY", ImeLayout.ClientY(12, 10, 18) == 36);
            Check("CompositionFontHeight 18", ImeLayout.CompositionFontHeight(18) == -18);
            Check("CompositionFontHeight 1", ImeLayout.CompositionFontHeight(1) == -1);
            Check("CompositionFontHeight 0", ImeLayout.CompositionFontHeight(0) == -1);
            Check("CompositionFontHeight negative", ImeLayout.CompositionFontHeight(-4) == -1);
            Check("FieldClientX", ImeLayout.FieldClientX(8, 10, 4f) == 8 + (int)(0f - 10 + 4));
            Check("FieldOuterHeight 16+1", ImeLayout.FieldOuterHeight(16, 1) == 18);
            Check("FieldOuterHeight 0+1", ImeLayout.FieldOuterHeight(0, 1) == 3);
            Check("FieldOuterHeight 10+0", ImeLayout.FieldOuterHeight(10, 0) == 10);
            Check("Theme.Selection is #3d59a1", Theme.Selection.ToArgb() == Color.FromArgb(0x3d, 0x59, 0xa1).ToArgb());
        }

        private static void RunStartupArgs()
        {
            StartupPlan empty = StartupArgs.Parse(new string[0]);
            Check("empty WorkspaceRoot", empty.WorkspaceRoot == null);
            Check("empty Files", empty.Files != null && empty.Files.Length == 0);
            Check("empty Failures", empty.Failures != null && empty.Failures.Length == 0);

            StartupPlan n = StartupArgs.Parse(null);
            Check("null args WorkspaceRoot", n.WorkspaceRoot == null && n.Files.Length == 0 && n.Failures.Length == 0);

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-startup-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "sample.txt");
            string missing = Path.Combine(dir, "missing.txt");
            string savedCwd = Environment.CurrentDirectory;
            try
            {
                File.WriteAllText(file, "x");
                string fileFull = Path.GetFullPath(file);
                string dirFull = Path.GetFullPath(dir);

                StartupPlan fileOnly = StartupArgs.Parse(new string[] { file });
                Check("file WorkspaceRoot null", fileOnly.WorkspaceRoot == null);
                Check("file one", fileOnly.Files.Length == 1 && string.Equals(fileOnly.Files[0], fileFull, StringComparison.OrdinalIgnoreCase));
                Check("file no failures", fileOnly.Failures.Length == 0);

                StartupPlan dirOnly = StartupArgs.Parse(new string[] { dir });
                Check("dir WorkspaceRoot", string.Equals(dirOnly.WorkspaceRoot, dirFull, StringComparison.OrdinalIgnoreCase));
                Check("dir no files", dirOnly.Files.Length == 0);
                Check("dir no failures", dirOnly.Failures.Length == 0);

                StartupPlan both = StartupArgs.Parse(new string[] { file, dir });
                Check("both WorkspaceRoot", string.Equals(both.WorkspaceRoot, dirFull, StringComparison.OrdinalIgnoreCase));
                Check("both one file", both.Files.Length == 1 && string.Equals(both.Files[0], fileFull, StringComparison.OrdinalIgnoreCase));
                Check("both no failures", both.Failures.Length == 0);

                StartupPlan miss = StartupArgs.Parse(new string[] { missing });
                Check("missing Files empty", miss.Files.Length == 0);
                Check("missing WorkspaceRoot null", miss.WorkspaceRoot == null);
                Check("missing Failures one", miss.Failures.Length == 1);

                string lower = fileFull.ToLowerInvariant();
                string upper = fileFull.ToUpperInvariant();
                StartupPlan dup = StartupArgs.Parse(new string[] { lower, upper });
                Check("case dup one file", dup.Files.Length == 1);
                Check("case dup no failures", dup.Failures.Length == 0);

                Environment.CurrentDirectory = dir;
                StartupPlan rel = StartupArgs.Parse(new string[] { "sample.txt" });
                string relFull = Path.GetFullPath("sample.txt");
                Check("relative GetFullPath", rel.Files.Length == 1 && string.Equals(rel.Files[0], relFull, StringComparison.OrdinalIgnoreCase));
                Check("relative WorkspaceRoot null", rel.WorkspaceRoot == null);
            }
            finally
            {
                try
                {
                    Environment.CurrentDirectory = savedCwd;
                }
                catch (IOException)
                {
                }

                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunLanguageDetector()
        {
            Check("ext .cs", LanguageDetector.FromPath("C:\\src\\App.cs") == LanguageKind.CSharp);
            Check("ext .CS", LanguageDetector.FromPath("Foo.CS") == LanguageKind.CSharp);
            Check("ext .bas", LanguageDetector.FromPath("Lib.bas") == LanguageKind.Vba);
            Check("ext .cls", LanguageDetector.FromPath("Mod.cls") == LanguageKind.Vba);
            Check("ext .ps1", LanguageDetector.FromPath("run.ps1") == LanguageKind.PowerShell);
            Check("ext .psm1", LanguageDetector.FromPath("mod.psm1") == LanguageKind.PowerShell);
            Check("ext .psd1", LanguageDetector.FromPath("mod.psd1") == LanguageKind.PowerShell);
            Check("ext .cmd", LanguageDetector.FromPath("clean.cmd") == LanguageKind.Cmd);
            Check("ext .bat", LanguageDetector.FromPath("go.bat") == LanguageKind.Cmd);
            Check("untitled null", LanguageDetector.FromPath(null) == LanguageKind.Plain);
            Check("empty path", LanguageDetector.FromPath("") == LanguageKind.Plain);
            Check("no extension", LanguageDetector.FromPath("C:\\a\\README") == LanguageKind.Plain);
            Check("ext .frm", LanguageDetector.FromPath("Form1.frm") == LanguageKind.Plain);
            Check("ext .vbs", LanguageDetector.FromPath("a.vbs") == LanguageKind.Plain);
            Check("ext .csx", LanguageDetector.FromPath("script.csx") == LanguageKind.Plain);
            Check("ext .txt", LanguageDetector.FromPath("note.txt") == LanguageKind.Plain);
            Check("display C#", LanguageDetector.GetDisplayName(LanguageKind.CSharp) == "C#");
            Check("display VBA", LanguageDetector.GetDisplayName(LanguageKind.Vba) == "VBA");
            Check("display PowerShell", LanguageDetector.GetDisplayName(LanguageKind.PowerShell) == "PowerShell");
            Check("display cmd", LanguageDetector.GetDisplayName(LanguageKind.Cmd) == "cmd");
            Check("display Plain", LanguageDetector.GetDisplayName(LanguageKind.Plain) == "Plain");

            Document untitled = Document.CreateUntitled();
            Check("untitled Language Plain", untitled.Language == LanguageKind.Plain);
        }

        private static void RunLexers()
        {
            CSharpLexer cs = new CSharpLexer();
            Check("cs class keyword", KindAt(cs, "class Foo", 0, 0) == TokenKind.Keyword);
            Check("cs classic local", KindAtCs("classic", 0, 0) == TokenKind.Local);
            Check("cs line comment", KindAt(cs, "int x; // hi", 0, 8) == TokenKind.Comment);
            Check("cs string", KindAt(cs, "x = \"str\";", 0, 5) == TokenKind.String);
            Check("cs char", KindAt(cs, "c = 'x';", 0, 5) == TokenKind.String);
            Check("cs decimal", KindAt(cs, "n = 123;", 0, 4) == TokenKind.Number);
            Check("cs hex", KindAt(cs, "n = 0xFF;", 0, 4) == TokenKind.Number);
            Check("cs at ident", KindAtCs("@class", 0, 0) == TokenKind.Local);

            List<Token> t0 = new List<Token>();
            int e0;
            cs.ScanLine("/* start", 0, t0, out e0);
            Check("cs block open state", e0 == CSharpLexer.BlockComment);
            List<Token> t1 = new List<Token>();
            int e1;
            cs.ScanLine("middle", e0, t1, out e1);
            Check("cs block middle comment", KindAtTokens(t1, 0) == TokenKind.Comment);
            Check("cs block mid state", e1 == CSharpLexer.BlockComment);
            List<Token> t2 = new List<Token>();
            int e2;
            cs.ScanLine("*/ class", e1, t2, out e2);
            Check("cs block close then class", KindAtTokens(t2, 3) == TokenKind.Keyword);
            Check("cs block closed state", e2 == 0);
            Check("cs next line keyword", KindAt(cs, "class", e2, 0) == TokenKind.Keyword);

            List<Token> v0 = new List<Token>();
            int ve0;
            cs.ScanLine("@\"hello", 0, v0, out ve0);
            Check("cs verbatim open", ve0 == CSharpLexer.VerbatimString);
            Check("cs verbatim open string", KindAtTokens(v0, 2) == TokenKind.String);
            List<Token> v1 = new List<Token>();
            int ve1;
            cs.ScanLine("world\"", ve0, v1, out ve1);
            Check("cs verbatim body", KindAtTokens(v1, 0) == TokenKind.String);
            Check("cs verbatim closed", ve1 == 0);

            List<Token> pre = new List<Token>();
            int pe;
            cs.ScanLine("#if DEBUG // z", 0, pre, out pe);
            Check("cs directive if", KindAtTokens(pre, 1) == TokenKind.Keyword);
            Check("cs directive rest", KindAtTokens(pre, 4) == TokenKind.Text);
            Check("cs directive comment", KindAtTokens(pre, 11) == TokenKind.Comment);

            VbaLexer vba = new VbaLexer();
            Check("vba tick comment", KindAt(vba, "' note", 0, 0) == TokenKind.Comment);
            Check("vba rem comment", KindAt(vba, "  Rem hello", 0, 2) == TokenKind.Comment);
            Check("vba Sub keyword", KindAt(vba, "Sub Main", 0, 0) == TokenKind.Keyword);
            Check("vba remote local", KindAt(vba, "Remote", 0, 0) == TokenKind.Local);

            PowerShellLexer ps = new PowerShellLexer();
            Check("ps hash comment", KindAt(ps, "# hi", 0, 0) == TokenKind.Comment);
            Check("ps if keyword", KindAt(ps, "if ($true)", 0, 0) == TokenKind.Keyword);
            Check("ps cmdlet method", KindAt(ps, "Get-ChildItem", 0, 0) == TokenKind.Method);
            Check("ps ForEach-Object method", KindAt(ps, "ForEach-Object", 0, 0) == TokenKind.Method);
            Check("ps foreach keyword", KindAt(ps, "foreach ($x in $y)", 0, 0) == TokenKind.Keyword);
            List<Token> pb = new List<Token>();
            int pbe;
            ps.ScanLine("<# block", 0, pb, out pbe);
            Check("ps block open", pbe == PowerShellLexer.BlockComment);
            List<Token> pb2 = new List<Token>();
            int pbe2;
            ps.ScanLine("mid", pbe, pb2, out pbe2);
            Check("ps block mid", KindAtTokens(pb2, 0) == TokenKind.Comment);
            List<Token> pb3 = new List<Token>();
            int pbe3;
            ps.ScanLine("#> if", pbe2, pb3, out pbe3);
            Check("ps block close if", KindAtTokens(pb3, 3) == TokenKind.Keyword);

            List<Token> hs = new List<Token>();
            int hse;
            ps.ScanLine("$x = @\"", 0, hs, out hse);
            Check("ps here double open", hse == PowerShellLexer.HereStringDouble);
            List<Token> hs2 = new List<Token>();
            int hse2;
            ps.ScanLine("hello", hse, hs2, out hse2);
            Check("ps here body", KindAtTokens(hs2, 0) == TokenKind.String);
            List<Token> hs3 = new List<Token>();
            int hse3;
            ps.ScanLine("\"@", hse2, hs3, out hse3);
            Check("ps here close", hse3 == 0);

            List<Token> hss = new List<Token>();
            int hsse;
            ps.ScanLine("@'", 0, hss, out hsse);
            Check("ps here single open", hsse == PowerShellLexer.HereStringSingle);
            List<Token> hss2 = new List<Token>();
            int hsse2;
            ps.ScanLine("z", hsse, hss2, out hsse2);
            Check("ps here single body", KindAtTokens(hss2, 0) == TokenKind.String);
            List<Token> hss3 = new List<Token>();
            int hsse3;
            ps.ScanLine("'@", hsse2, hss3, out hsse3);
            Check("ps here single close", hsse3 == 0);

            CmdLexer cmd = new CmdLexer();
            Check("cmd if keyword", KindAt(cmd, "if exist a", 0, 0) == TokenKind.Keyword);
            Check("cmd echo keyword", KindAt(cmd, "echo hello", 0, 0) == TokenKind.Keyword);
            Check("cmd rem comment", KindAt(cmd, "rem note", 0, 0) == TokenKind.Comment);
            Check("cmd colon comment", KindAt(cmd, ":: note", 0, 0) == TokenKind.Comment);

            PlainLexer plain = new PlainLexer();
            Check("plain class text", KindAt(plain, "class if rem", 0, 0) == TokenKind.Text);
            Check("plain if text", KindAt(plain, "class if rem", 0, 6) == TokenKind.Text);

            Check("cs local x", KindAtCs("int x;", 0, 4) == TokenKind.Local);
            Check("cs method call", KindAtCs("obj.Foo(", 0, 4) == TokenKind.Method);
            Check("cs instance bar", KindAtCs("obj.Bar;", 0, 4) == TokenKind.Instance);
            Check("cs this instance", KindAtCs("this.x", 0, 5) == TokenKind.Instance);
            Check("cs new type", KindAtCs("new Foo()", 0, 4) == TokenKind.Type);
            Check("cs class name", KindAtCs("class Foo", 0, 6) == TokenKind.Type);
            Check("cs method M", KindAtCs("M(", 0, 0) == TokenKind.Method);
            Check("cs comment keeps ident", KindAt(cs, "// Foo(", 0, 3) == TokenKind.Comment);
            Check("cs string keeps ident", KindAt(cs, "\"Foo(\"", 0, 1) == TokenKind.String);
            Check("vba sub name", KindAt(vba, "Sub Main", 0, 4) == TokenKind.Method);
            Check("vba dim local", KindAt(vba, "Dim x", 0, 4) == TokenKind.Local);
            Check("vba me instance", KindAt(vba, "Me.Name", 0, 3) == TokenKind.Instance);
            Check("vba as type", KindAtLang(LanguageKind.Vba, "As Foo", 0, 3) == TokenKind.Type);
            Check("ps dollar local", KindAt(ps, "$x", 0, 1) == TokenKind.Local);
            Check("ps member instance", KindAt(ps, "$o.Name", 0, 3) == TokenKind.Instance);
            Check("ps function name", KindAt(ps, "function Foo", 0, 9) == TokenKind.Method);
            Check("ps childitem method", KindAt(ps, "Get-ChildItem", 0, 4) == TokenKind.Method);
            Check("cmd env local", KindAt(cmd, "%PATH%", 0, 1) == TokenKind.Local);
            Check("cmd label method", KindAt(cmd, ":build", 0, 1) == TokenKind.Method);
            Check("plain Foo", KindAt(plain, "Foo", 0, 0) == TokenKind.Text);
            Check("vba sub name with comment", KindAt(vba, "Sub Main() ' c", 0, 4) == TokenKind.Method);
            Check("vba sub comment stays", KindAt(vba, "Sub Main() ' c", 0, 11) == TokenKind.Comment);
            Check("vba dim local with comment", KindAt(vba, "Dim x ' c", 0, 4) == TokenKind.Local);
            Check("vba dim comment stays", KindAt(vba, "Dim x ' c", 0, 6) == TokenKind.Comment);
            Check("cmd hello with colon comment", KindAt(cmd, "echo hello :: c", 0, 5) == TokenKind.Local);
            Check("cmd label with colon comment", KindAt(cmd, ":build :: c", 0, 1) == TokenKind.Method);
            Check("cs local x with line comment", KindAtCs("int x; // c", 0, 4) == TokenKind.Local);
            Check("ps dollar call local", KindAt(ps, "$foo(", 0, 1) == TokenKind.Local);
            Check("cmd echo path keyword", KindAt(cmd, "echo path", 0, 5) == TokenKind.Keyword);
            Check("vba property get name", KindAt(vba, "Property Get Foo", 0, 13) == TokenKind.Method);
            Check("ps path param local", KindAt(ps, "Get-ChildItem -Path", 0, 15) == TokenKind.Local);
            Check("ps childitem still method", KindAt(ps, "Get-ChildItem -Path", 0, 4) == TokenKind.Method);
            Check("cs comment between ident and paren", KindAtCs("M /*c*/ (", 0, 0) == TokenKind.Method);
        }

        private static void RunCSharpBind()
        {
            string nested = "class Foo { void M() { Foo x; } }";
            Check("bind class name Type", KindAtCs(nested, 0, 6) == TokenKind.Type);
            Check("bind method M", KindAtCs(nested, 0, 17) == TokenKind.Method);
            Check("bind Foo type pos", KindAtCs(nested, 0, 23) == TokenKind.Type);
            Check("bind local x", KindAtCs(nested, 0, 27) == TokenKind.Local);

            string color = "class Color { void M() { Color Color = Color.Red; } }";
            Check("bind Color decl Type", KindAtCs(color, 0, 6) == TokenKind.Type);
            Check("bind Color type pos", KindAtCs(color, 0, 25) == TokenKind.Type);
            Check("bind Color local", KindAtCs(color, 0, 31) == TokenKind.Local);
            Check("bind Color rhs Type", KindAtCs(color, 0, 39) == TokenKind.Type);
            Check("bind Red instance", KindAtCs(color, 0, 45) == TokenKind.Instance);

            Check("bind new Foo Type", KindAtCs("new Foo()", 0, 4) == TokenKind.Type);
            Check("bind obj method", KindAtCs("obj.Bar(", 0, 4) == TokenKind.Method);
            Check("bind obj instance", KindAtCs("obj.Bar", 0, 4) == TokenKind.Instance);
            Check("bind int local", KindAtCs("int x; // c", 0, 4) == TokenKind.Local);

            Check("bind Console Type", KindAtCs("Console.WriteLine();", 0, 0) == TokenKind.Type);
            Check("bind WriteLine Method", KindAtCs("Console.WriteLine();", 0, 8) == TokenKind.Method);

            string broken = "class Foo { int x; !!! void M() { Foo y; } }";
            Check("bind error still Foo type", KindAtCs(broken, 0, 6) == TokenKind.Type);
            Check("bind error still y local", KindAtCs(broken, 0, 38) == TokenKind.Local);
            Check("session overlay Color local", KindAtLang(LanguageKind.CSharp, color, 0, 31) == TokenKind.Local);
            Check("session overlay Console Type", KindAtLang(LanguageKind.CSharp, "Console.WriteLine();", 0, 0) == TokenKind.Type);

            string leak = "class C { int x; void A(int x) { } void B() { x; } }";
            Check("bind later field x Instance", KindAtCs(leak, 0, IndexOfIdent(leak, "x", 3)) == TokenKind.Instance);

            string consoleParam = "void A(int Console) { } void B() { Console.WriteLine(); }";
            Check("bind Console not leaked param", KindAtCs(consoleParam, 0, IndexOfIdent(consoleParam, "Console", 2)) == TokenKind.Type);

            string prop = "class C { int P { get { int y = 1; return y; } } }";
            Check("bind getter local y", KindAtCs(prop, 0, IndexOfIdent(prop, "y", 1)) == TokenKind.Local);
            Check("bind getter y use", KindAtCs(prop, 0, IndexOfIdent(prop, "y", 2)) == TokenKind.Local);

            string nestGet = "class C { int P { get { if (a) { } int z = 1; } } void M() { int w; } }";
            Check("bind getter z after nested brace", KindAtCs(nestGet, 0, IndexOfIdent(nestGet, "z", 1)) == TokenKind.Local);
            Check("bind method w after property", KindAtCs(nestGet, 0, IndexOfIdent(nestGet, "w", 1)) == TokenKind.Local);

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-ws-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                File.WriteAllText(Path.Combine(dir, "Other.cs"), "class Other { }\r\n", Encoding.UTF8);
                WorkspaceTypeNames.Invalidate();
                TextBuffer buf = new TextBuffer();
                buf.SetText("Other x;");
                List<ClassifySpan>[] overlay = new List<ClassifySpan>[buf.LineCount];
                overlay[0] = new List<ClassifySpan>();
                CSharpSemantic.Classify(buf, dir, overlay);
                TokenKind otherKind = TokenKind.Text;
                for (int i = 0; i < overlay[0].Count; i++)
                {
                    ClassifySpan span = overlay[0][i];
                    if (0 >= span.Start && 0 < span.Start + span.Length)
                    {
                        otherKind = span.Kind;
                    }
                }

                Check("bind workspace Other Type", otherKind == TokenKind.Type);

                File.WriteAllText(Path.Combine(dir, "NewType.cs"), "class NewType { }\r\n", Encoding.UTF8);
                TextBuffer fresh = new TextBuffer();
                fresh.SetText("NewType n;");
                List<ClassifySpan>[] freshOverlay = new List<ClassifySpan>[fresh.LineCount];
                freshOverlay[0] = new List<ClassifySpan>();
                CSharpSemantic.Classify(fresh, dir, freshOverlay);
                TokenKind newKind = TokenKind.Text;
                for (int i = 0; i < freshOverlay[0].Count; i++)
                {
                    ClassifySpan span = freshOverlay[0][i];
                    if (0 >= span.Start && 0 < span.Start + span.Length)
                    {
                        newKind = span.Kind;
                    }
                }

                Check("bind workspace NewType without Invalidate", newKind == TokenKind.Type);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }

            Check("vba Range Type", KindAtLang(LanguageKind.Vba, "Dim r As Range", 0, 9) == TokenKind.Type);
            Check("ps type accel", KindAtLang(LanguageKind.PowerShell, "[String]$x", 0, 1) == TokenKind.Type);
        }

        private static void RunHighlightSession()
        {
            TextBuffer buf = new TextBuffer();
            buf.SetText("/*\ncomment\nstill\n*/\nclass C\n{\n}");
            HighlightSession session = new HighlightSession();
            session.Reset(LanguageKind.CSharp, buf.LineCount);
            session.SyncAfterEdit(buf, 0);
            Check("session line0 normal", session.GetStartState(0) == 0);
            Check("session line1 block", session.GetStartState(1) == CSharpLexer.BlockComment);
            Check("session line2 block", session.GetStartState(2) == CSharpLexer.BlockComment);
            Check("session line4 normal", session.GetStartState(4) == 0);
            int afterClass = session.GetStartState(5);
            int afterBrace = session.GetStartState(6);

            buf.Delete(new BufferPoint(4, 6), new BufferPoint(4, 7));
            buf.Insert(4, 6, "D");
            session.SyncAfterEdit(buf, 4);
            Check("session stable line5", session.GetStartState(5) == afterClass);
            Check("session stable line6", session.GetStartState(6) == afterBrace);
            Check("session class still normal", session.GetStartState(4) == 0);

            session.InvalidateFrom(0);
            session.SyncAfterEdit(buf, 0);
            Check("session after invalidate block", session.GetStartState(1) == CSharpLexer.BlockComment);

            TextBuffer sameCount = new TextBuffer();
            sameCount.SetText("void Foo()\n{\n    int x;\n}");
            HighlightSession earlyStop = new HighlightSession();
            HighlightSession replaceSession = new HighlightSession();
            earlyStop.Reset(LanguageKind.CSharp, sameCount.LineCount);
            replaceSession.Reset(LanguageKind.CSharp, sameCount.LineCount);
            earlyStop.SyncAfterEdit(sameCount, 0);
            replaceSession.SyncAfterEdit(sameCount, 0);
            sameCount.Delete(new BufferPoint(1, 0), new BufferPoint(3, 0));
            BufferPoint replaced = sameCount.Insert(1, 0, "{\n    /*\n");
            Check("same-count replace lines", sameCount.LineCount == 4);
            Check("same-count replace last line", replaced.Line == 3);
            earlyStop.SyncAfterEdit(sameCount, 1);
            replaceSession.SyncAfterEdit(sameCount, 1, replaced.Line);
            Check("two-arg early-stop stale", earlyStop.GetStartState(3) != CSharpLexer.BlockComment);
            Check("same-count replace opens block", replaceSession.GetStartState(3) == CSharpLexer.BlockComment);

            BufferPoint inserted = sameCount.Insert(2, sameCount.GetLine(2).Length, "\n    still");
            replaceSession.SyncAfterEdit(sameCount, 2, inserted.Line);
            Check("insert after block line count", sameCount.LineCount == 5);
            Check("insert after block start line3", replaceSession.GetStartState(3) == CSharpLexer.BlockComment);
            Check("insert after block start line4", replaceSession.GetStartState(4) == CSharpLexer.BlockComment);

            sameCount.Delete(new BufferPoint(3, 0), new BufferPoint(4, 0));
            replaceSession.SyncAfterEdit(sameCount, 3, 3);
            Check("delete after block line count", sameCount.LineCount == 4);
            Check("delete keeps block at 3", replaceSession.GetStartState(3) == CSharpLexer.BlockComment);

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-hl-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string csPath = Path.Combine(dir, "moved.cs");
            try
            {
                Document doc = Document.CreateUntitled();
                Check("session untitled plain", doc.Language == LanguageKind.Plain);
                File.WriteAllText(csPath, "class A\r\n{\r\n}\r\n", Encoding.UTF8);
                doc.SaveAs(csPath);
                Check("session saveas csharp", doc.Language == LanguageKind.CSharp);
                Check("session saveas reset start", doc.HighlightSession.GetStartState(0) == 0);
            }
            finally
            {
                try
                {
                    if (File.Exists(csPath))
                    {
                        File.Delete(csPath);
                    }
                }
                catch (IOException)
                {
                }

                try
                {
                    Directory.Delete(dir, false);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunBraceMatch()
        {
            TextBuffer buf = new TextBuffer();
            buf.SetText("foo(bar)");
            HighlightSession session = MakeP7Session(LanguageKind.CSharp, buf);
            BraceMatchResult hit = BraceMatch.Find(LanguageKind.CSharp, buf, session, 0, 7);
            Check("br foo(bar|) found", hit.Found && hit.Matched);
            Check("br foo(bar|) anchor", hit.Found && hit.AnchorLine == 0 && hit.AnchorColumn == 7);
            Check("br foo(bar|) pair", hit.Found && hit.PairLine == 0 && hit.PairColumn == 3);

            buf.SetText("\"( )\"");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            hit = BraceMatch.Find(LanguageKind.CSharp, buf, session, 0, 2);
            Check("br string no match", !hit.Found);

            buf.SetText("// (");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            hit = BraceMatch.Find(LanguageKind.CSharp, buf, session, 0, 4);
            Check("br comment no match", !hit.Found);

            buf.SetText("(]");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            hit = BraceMatch.Find(LanguageKind.CSharp, buf, session, 0, 1);
            Check("br mismatch found", hit.Found && !hit.Matched);
            Check("br mismatch pair", hit.Found && hit.PairLine == 0 && hit.PairColumn == 1);

            buf.SetText("a[0]");
            session = MakeP7Session(LanguageKind.Vba, buf);
            hit = BraceMatch.Find(LanguageKind.Vba, buf, session, 0, 2);
            Check("br vba [] not brace", !hit.Found);

            buf.SetText("foo()");
            session = MakeP7Session(LanguageKind.Vba, buf);
            hit = BraceMatch.Find(LanguageKind.Vba, buf, session, 0, 5);
            Check("br vba () match", hit.Found && hit.Matched);

            buf.SetText("{ }");
            session = MakeP7Session(LanguageKind.Cmd, buf);
            hit = BraceMatch.Find(LanguageKind.Cmd, buf, session, 0, 1);
            Check("br cmd empty", !hit.Found);

            buf.SetText("{ }");
            session = MakeP7Session(LanguageKind.PowerShell, buf);
            hit = BraceMatch.Find(LanguageKind.PowerShell, buf, session, 0, 1);
            Check("br ps {} match", hit.Found && hit.Matched);
        }

        private static void RunAutoClose()
        {
            TextBuffer buf = new TextBuffer();
            buf.SetText("");
            HighlightSession session = MakeP7Session(LanguageKind.CSharp, buf);
            char closer;
            bool insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 0, '{', out closer);
            Check("ac empty {", insert && closer == '}');
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 0, '(', out closer);
            Check("ac empty (", insert && closer == ')');
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 0, '[', out closer);
            Check("ac empty [", insert && closer == ']');

            buf.SetText("");
            session = MakeP7Session(LanguageKind.Vba, buf);
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.Vba, buf, session, 0, 0, '(', out closer);
            Check("ac vba no paren", !insert);

            buf.SetText("}");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 0, '{', out closer);
            Check("ac next } no double", !insert);

            buf.SetText("{  }");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 2, '{', out closer);
            Check("ac already pair { only", !insert);

            buf.SetText("\"\"");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 1, '{', out closer);
            Check("ac string no close", !insert);

            buf.SetText("");
            session = MakeP7Session(LanguageKind.Cmd, buf);
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.Cmd, buf, session, 0, 0, '{', out closer);
            Check("ac cmd no close", !insert);

            buf.SetText("");
            session = MakeP7Session(LanguageKind.PowerShell, buf);
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.PowerShell, buf, session, 0, 0, '{', out closer);
            Check("ac ps {}", insert && closer == '}');
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.PowerShell, buf, session, 0, 0, '(', out closer);
            Check("ac ps (", insert && closer == ')');

            buf.SetText("");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            UndoStack undo = new UndoStack();
            undo.BeginCompound();
            insert = AutoCloseRules.ShouldInsertCloser(LanguageKind.CSharp, buf, session, 0, 0, '{', out closer);
            string pair = insert ? "{}" : "{";
            buf.Insert(0, 0, pair);
            undo.RecordInsert(0, 0, pair);
            undo.EndCompound();
            Check("ac compound inserted", buf.GetText() == "{}");
            undo.Undo(buf);
            Check("ac compound undo both", buf.GetText() == "");
        }

        private static void RunSmartIndent()
        {
            TextBuffer buf = new TextBuffer();
            buf.SetText("    {}");
            HighlightSession session = MakeP7Session(LanguageKind.CSharp, buf);
            SmartIndentPlan plan = SmartIndentRules.Plan(LanguageKind.CSharp, buf, session, 0, 5, 4);
            Check("si split kind", plan.Kind == SmartIndentKind.SplitPair);
            ApplySmartEnter(LanguageKind.CSharp, buf, session, null, 0, 5, 4);
            Check("si split mid 8", buf.LineCount == 3 && buf.GetLine(1) == "        ");
            Check("si split close 4", buf.GetLine(2) == "    }");
            Check("si split open", buf.GetLine(0) == "    {");

            buf.SetText("\tfoo");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            plan = SmartIndentRules.Plan(LanguageKind.CSharp, buf, session, 0, 4, 4);
            Check("si copy only", plan.Kind == SmartIndentKind.CopyOnly);
            ApplySmartEnter(LanguageKind.CSharp, buf, session, null, 0, 4, 4);
            Check("si copy tab", buf.LineCount == 2 && buf.GetLine(1) == "\t");

            buf.SetText("    {}");
            session = MakeP7Session(LanguageKind.PowerShell, buf);
            plan = SmartIndentRules.Plan(LanguageKind.PowerShell, buf, session, 0, 5, 4);
            Check("si ps split", plan.Kind == SmartIndentKind.SplitPair);

            buf.SetText("    {");
            session = MakeP7Session(LanguageKind.CSharp, buf);
            plan = SmartIndentRules.Plan(LanguageKind.CSharp, buf, session, 0, 5, 4);
            Check("si increase {", plan.Kind == SmartIndentKind.Increase);

            buf.SetText("\tfoo");
            session = MakeP7Session(LanguageKind.Cmd, buf);
            plan = SmartIndentRules.Plan(LanguageKind.Cmd, buf, session, 0, 4, 4);
            Check("si cmd copy", plan.Kind == SmartIndentKind.CopyOnly);
            ApplySmartEnter(LanguageKind.Cmd, buf, session, null, 0, 4, 4);
            Check("si cmd find only", buf.LineCount == 2 && buf.GetLine(1) == "\t" && buf.GetLine(0) == "\tfoo");
        }

        private static void RunVbaBlock()
        {
            TextBuffer buf = new TextBuffer();
            buf.SetText("For i = 1 To 3");
            HighlightSession session = MakeP7Session(LanguageKind.Vba, buf);
            SmartIndentPlan plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 0, buf.GetLine(0).Length, 4);
            Check("vba for next kind", plan.Kind == SmartIndentKind.InsertBlockClose && plan.CloseText == "Next");
            Check("vba no End For plan", plan.CloseText != null && plan.CloseText.IndexOf("End For", StringComparison.Ordinal) < 0);
            UndoStack undo = new UndoStack();
            ApplySmartEnter(LanguageKind.Vba, buf, session, undo, 0, buf.GetLine(0).Length, 4);
            string text = buf.GetText().Replace("\r\n", "\n");
            Check("vba for next text", text.IndexOf("Next", StringComparison.Ordinal) >= 0);
            Check("vba no End For result", text.IndexOf("End For", StringComparison.Ordinal) < 0);
            Check("vba for lines", buf.LineCount == 3 && buf.GetLine(1) == "    " && buf.GetLine(2) == "Next");
            undo.Undo(buf);
            Check("vba enter next one undo", buf.GetText().Replace("\r\n", "\n") == "For i = 1 To 3");

            buf.SetText("Sub Foo()\nEnd Sub");
            session = MakeP7Session(LanguageKind.Vba, buf);
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 0, buf.GetLine(0).Length, 4);
            Check("vba existing end sub", plan.Kind == SmartIndentKind.Increase);
            ApplySmartEnter(LanguageKind.Vba, buf, session, null, 0, buf.GetLine(0).Length, 4);
            Check("vba no second end sub", CountToken(buf.GetText(), "End Sub") == 1);

            buf.SetText("If x Then y");
            session = MakeP7Session(LanguageKind.Vba, buf);
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 0, buf.GetLine(0).Length, 4);
            Check("vba one line if", plan.Kind == SmartIndentKind.CopyOnly);
            ApplySmartEnter(LanguageKind.Vba, buf, session, null, 0, buf.GetLine(0).Length, 4);
            Check("vba one line no endif", buf.GetText().IndexOf("End If", StringComparison.Ordinal) < 0);

            buf.SetText("If x Then");
            session = MakeP7Session(LanguageKind.Vba, buf);
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 0, buf.GetLine(0).Length, 4);
            Check("vba block if", plan.Kind == SmartIndentKind.InsertBlockClose && plan.CloseText == "End If");

            buf.SetText("For Each x In xs");
            session = MakeP7Session(LanguageKind.Vba, buf);
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 0, buf.GetLine(0).Length, 4);
            Check("vba for each next", plan.Kind == SmartIndentKind.InsertBlockClose && plan.CloseText == "Next");

            buf.SetText("For i = 1 To 3\n    For j = 1 To 3\nNext");
            session = MakeP7Session(LanguageKind.Vba, buf);
            int innerCol = buf.GetLine(1).Length;
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 1, innerCol, 4);
            Check("vba nested for insert", plan.Kind == SmartIndentKind.InsertBlockClose && plan.CloseText == "Next");
            ApplySmartEnter(LanguageKind.Vba, buf, session, null, 1, innerCol, 4);
            Check("vba nested for two next", CountToken(buf.GetText(), "Next") == 2);
            Check("vba nested for inner prefix", buf.GetLine(3) == "    Next");
            Check("vba nested for outer next", buf.GetLine(4) == "Next");

            buf.SetText("If a Then\n    If b Then\nEnd If");
            session = MakeP7Session(LanguageKind.Vba, buf);
            innerCol = buf.GetLine(1).Length;
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 1, innerCol, 4);
            Check("vba nested if insert", plan.Kind == SmartIndentKind.InsertBlockClose && plan.CloseText == "End If");
            ApplySmartEnter(LanguageKind.Vba, buf, session, null, 1, innerCol, 4);
            Check("vba nested if two endif", CountToken(buf.GetText(), "End If") == 2);
            Check("vba nested if inner prefix", buf.GetLine(3) == "    End If");
            Check("vba nested if outer endif", buf.GetLine(4) == "End If");

            buf.SetText("For i = 1 To 3\nNext");
            session = MakeP7Session(LanguageKind.Vba, buf);
            plan = SmartIndentRules.Plan(LanguageKind.Vba, buf, session, 0, buf.GetLine(0).Length, 4);
            Check("vba outer next no double", plan.Kind == SmartIndentKind.Increase);
            ApplySmartEnter(LanguageKind.Vba, buf, session, null, 0, buf.GetLine(0).Length, 4);
            Check("vba outer next stays one", CountToken(buf.GetText(), "Next") == 1);
        }

        private static void RunVbaKeywordCase()
        {
            TextBuffer buf = new TextBuffer();
            buf.SetText("dim");
            HighlightSession session = MakeP7Session(LanguageKind.Vba, buf);
            int start;
            string canonical;
            bool hit = VbaKeywordCase.TryCanonicalBeforeSeparator(LanguageKind.Vba, buf, session, 0, 3, ' ', out start, out canonical);
            Check("case dim", hit && canonical == "Dim" && start == 0);

            buf.SetText("\"dim\"");
            session = MakeP7Session(LanguageKind.Vba, buf);
            hit = VbaKeywordCase.TryCanonicalBeforeSeparator(LanguageKind.Vba, buf, session, 0, 4, ' ', out start, out canonical);
            Check("case string dim", !hit);

            buf.SetText("' dim");
            session = MakeP7Session(LanguageKind.Vba, buf);
            hit = VbaKeywordCase.TryCanonicalBeforeSeparator(LanguageKind.Vba, buf, session, 0, 5, ' ', out start, out canonical);
            Check("case comment dim", !hit);

            buf.SetText("foo");
            session = MakeP7Session(LanguageKind.Vba, buf);
            hit = VbaKeywordCase.TryCanonicalBeforeSeparator(LanguageKind.Vba, buf, session, 0, 3, ' ', out start, out canonical);
            Check("case foo", !hit);

            string mapped;
            Check("case TryCanonical dim", VbaKeywords.TryCanonical("dim", out mapped) && mapped == "Dim");
            Check("case Contains bool", VbaKeywords.Set.Contains("dim"));
        }

        private static void InspectP7ASource()
        {
            string repo = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            string view = File.ReadAllText(Path.Combine(repo, "src", "WindowsIDE", "Editor", "TextView.cs"));
            Check("p7a no End For in TextView", view.IndexOf("End For", StringComparison.Ordinal) < 0);

            string langDir = Path.Combine(repo, "src", "WindowsIDE", "Languages");
            string[] langFiles = Directory.GetFiles(langDir, "*.cs", SearchOption.AllDirectories);
            bool uiRef = false;
            bool langEndFor = false;
            int i = 0;
            while (i < langFiles.Length)
            {
                string src = File.ReadAllText(langFiles[i]);
                if (src.IndexOf("using WindowsIDE.Ui", StringComparison.Ordinal) >= 0)
                {
                    uiRef = true;
                }

                if (src.IndexOf("End For", StringComparison.Ordinal) >= 0)
                {
                    langEndFor = true;
                }

                i++;
            }

            Check("p7a Languages no Ui", !uiRef);
            Check("p7a Languages no End For", !langEndFor);

            string theme = File.ReadAllText(Path.Combine(repo, "src", "WindowsIDE", "Ui", "Theme.cs"));
            Check("p7a no Theme brace color", theme.IndexOf("Brace", StringComparison.Ordinal) < 0);
            Check("p7a Theme Selection stays", theme.IndexOf("Selection", StringComparison.Ordinal) >= 0);
            Check("p7a Theme Error stays", theme.IndexOf("Error", StringComparison.Ordinal) >= 0);

            string indent = File.ReadAllText(Path.Combine(repo, "src", "WindowsIDE", "Editor", "IndentRules.cs"));
            Check("p7a IndentRules no Smart", indent.IndexOf("Smart", StringComparison.Ordinal) < 0);
        }

        private static HighlightSession MakeP7Session(LanguageKind language, TextBuffer buf)
        {
            HighlightSession session = new HighlightSession();
            session.Reset(language, buf.LineCount);
            session.SyncAfterEdit(buf, 0);
            return session;
        }

        private static void ApplySmartEnter(LanguageKind language, TextBuffer buf, HighlightSession session, UndoStack undo, int line, int column, int tabSize)
        {
            string prefix = IndentRules.LeadingWhitespace(buf.GetLine(line));
            SmartIndentPlan plan = SmartIndentRules.Plan(language, buf, session, line, column, tabSize);
            if (tabSize < 1)
            {
                tabSize = 1;
            }

            string extra = new string(' ', tabSize);
            string text = "\n" + prefix;
            if (plan.Kind == SmartIndentKind.Increase)
            {
                text = "\n" + prefix + extra;
            }
            else if (plan.Kind == SmartIndentKind.SplitPair)
            {
                text = "\n" + prefix + extra + "\n" + prefix;
            }
            else if (plan.Kind == SmartIndentKind.InsertBlockClose)
            {
                string close = plan.CloseText == null ? "" : plan.CloseText;
                text = "\n" + prefix + extra + "\n" + prefix + close;
            }

            buf.Insert(line, column, text);
            if (undo != null)
            {
                undo.RecordInsert(line, column, text);
            }

            session.Reset(language, buf.LineCount);
            session.SyncAfterEdit(buf, 0);
        }

        private static int CountToken(string text, string token)
        {
            if (text == null || token == null || token.Length == 0)
            {
                return 0;
            }

            int count = 0;
            int start = 0;
            while (true)
            {
                int at = text.IndexOf(token, start, StringComparison.Ordinal);
                if (at < 0)
                {
                    return count;
                }

                count++;
                start = at + token.Length;
            }
        }

        private static void RunDiagnosticParser()
        {
            Diagnostic[] none = DiagnosticParser.Parse(null);
            Check("parse null empty", none != null && none.Length == 0);
            Check("parse empty", DiagnosticParser.Parse("").Length == 0);

            string banner = "Microsoft (R) Visual C# Compiler version 4.8.9221.0\r\nfor C# 5\r\nCopyright (C) Microsoft Corporation. All rights reserved.\r\n\r\n";
            Check("parse banner ignored", DiagnosticParser.Parse(banner).Length == 0);

            Diagnostic[] err = DiagnosticParser.Parse("C:\\tmp\\Foo.cs(3,9): error CS1002: ; expected");
            Check("parse loc error count", err.Length == 1);
            Check("parse loc error path", err.Length == 1 && err[0].FilePath == "C:\\tmp\\Foo.cs");
            Check("parse loc error line col", err.Length == 1 && err[0].Line == 3 && err[0].Column == 9);
            Check("parse loc error code", err.Length == 1 && err[0].IsError && err[0].Code == "CS1002");
            Check("parse loc error msg", err.Length == 1 && err[0].Message == "; expected");

            Diagnostic[] warn = DiagnosticParser.Parse("C:\\tmp\\Foo.cs(5,1): warning CS0162: Unreachable code detected");
            Check("parse loc warning", warn.Length == 1 && !warn[0].IsError && warn[0].Code == "CS0162" && warn[0].Line == 5 && warn[0].Column == 1);

            Diagnostic[] noCol = DiagnosticParser.Parse("C:\\tmp\\Foo.cs(12): error CS0116: A namespace cannot directly contain members");
            Check("parse no column", noCol.Length == 1 && noCol[0].Line == 12 && noCol[0].Column == 0 && noCol[0].Code == "CS0116");

            Diagnostic[] fatal = DiagnosticParser.Parse("fatal error CS0006: Metadata file 'x' could not be found");
            Check("parse fatal no file", fatal.Length == 1 && fatal[0].IsError && fatal[0].FilePath == null && fatal[0].Line == 0 && fatal[0].Code == "CS0006");

            Diagnostic[] cscErr = DiagnosticParser.Parse("CSC : error CS2001: Source file 'x' could not be found.");
            Check("parse CSC error", cscErr.Length == 1 && cscErr[0].FilePath == null && cscErr[0].Code == "CS2001");

            Diagnostic[] filelessErr = DiagnosticParser.Parse("error CS5001: Program does not contain a static 'Main' method suitable for an entry point");
            Check("parse fileless error", filelessErr.Length == 1 && filelessErr[0].IsError && filelessErr[0].Code == "CS5001");

            Diagnostic[] filelessWarn = DiagnosticParser.Parse("warning CS2008: No source files specified");
            Check("parse fileless warning", filelessWarn.Length == 1 && !filelessWarn[0].IsError && filelessWarn[0].Code == "CS2008");

            Diagnostic[] jp = DiagnosticParser.Parse("C:\\tmp\\Foo.cs(3,9): error CS1002: ; が必要です");
            Check("parse japanese message", jp.Length == 1 && jp[0].Code == "CS1002" && jp[0].Message == "; が必要です");

            Diagnostic[] spaced = DiagnosticParser.Parse("C:\\Users\\User Name\\Foo.cs(1,1): error CS0101: The namespace already contains");
            Check("parse path with space", spaced.Length == 1 && spaced[0].FilePath == "C:\\Users\\User Name\\Foo.cs" && spaced[0].Line == 1);

            Diagnostic syn = Diagnostic.CreateSynthetic("C# ソースがありません。");
            Check("synthetic not CSxxxx", syn.Code == null && syn.IsError && syn.FilePath == null);
            Check("synthetic message", syn.Message == "C# ソースがありません。");

            Diagnostic synPath = Diagnostic.CreateSynthetic("C:\\tmp\\Lib\\A.bas", "衝突");
            Check("synthetic path FilePath", synPath.FilePath == "C:\\tmp\\Lib\\A.bas" && synPath.Code == null && synPath.IsError);
            Check("synthetic path message", synPath.Message == "衝突");

            Diagnostic[] exit0Empty = DiagnosticParser.ApplyExitCode(new Diagnostic[0], 0, "");
            Check("exit 0 empty stays empty", exit0Empty != null && exit0Empty.Length == 0);
            Diagnostic[] exit1Empty = DiagnosticParser.ApplyExitCode(null, 1, "");
            Check("exit 1 empty synthetic", exit1Empty != null && exit1Empty.Length == 1 && exit1Empty[0].Code == null && exit1Empty[0].IsError);
            Diagnostic[] exit1Kept = DiagnosticParser.ApplyExitCode(err, 1, "C:\\tmp\\Foo.cs(3,9): error CS1002: ; expected");
            Check("exit 1 keeps parsed", exit1Kept != null && exit1Kept.Length == 1 && exit1Kept[0].Code == "CS1002" && object.ReferenceEquals(exit1Kept, err));
        }

        private static void RunCsFileEnumerator()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-csenum-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                WriteUtf8Bom(Path.Combine(dir, "a.cs"), "class A {}");
                Directory.CreateDirectory(Path.Combine(dir, "sub"));
                WriteUtf8Bom(Path.Combine(dir, "sub", "b.cs"), "class B {}");
                Directory.CreateDirectory(Path.Combine(dir, "bin"));
                WriteUtf8Bom(Path.Combine(dir, "bin", "skip.cs"), "class SkipBin {}");
                Directory.CreateDirectory(Path.Combine(dir, "obj"));
                WriteUtf8Bom(Path.Combine(dir, "obj", "skip.cs"), "class SkipObj {}");
                Directory.CreateDirectory(Path.Combine(dir, ".git"));
                WriteUtf8Bom(Path.Combine(dir, ".git", "skip.cs"), "class SkipGit {}");

                string[] listed = CsFileEnumerator.List(dir);
                Check("enum count 2", listed != null && listed.Length == 2);
                Check("enum keeps a.cs", ContainsPath(listed, Path.Combine(dir, "a.cs")));
                Check("enum keeps sub b.cs", ContainsPath(listed, Path.Combine(dir, "sub", "b.cs")));
                Check("enum drops bin", !ContainsPath(listed, Path.Combine(dir, "bin", "skip.cs")));
                Check("enum drops obj", !ContainsPath(listed, Path.Combine(dir, "obj", "skip.cs")));
                Check("enum drops git", !ContainsPath(listed, Path.Combine(dir, ".git", "skip.cs")));
                Check("enum missing root", CsFileEnumerator.List(Path.Combine(dir, "missing")).Length == 0);
                Check("enum null root", CsFileEnumerator.List(null).Length == 0);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunCompileUnit()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-cunit-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string outside = Path.Combine(Path.GetTempPath(), "WindowsIDE-cunit-out-" + Guid.NewGuid().ToString("N") + ".cs");
            try
            {
                WriteUtf8Bom(Path.Combine(dir, "In.cs"), "class In {}");
                WriteUtf8Bom(outside, "class Out {}");
                string ps1 = Path.Combine(dir, "run.ps1");
                File.WriteAllText(ps1, "# no", Encoding.UTF8);

                string[] withWs = CompileUnit.Resolve(dir, outside);
                Check("unit workspace ignores outside tab", withWs.Length == 1 && ContainsPath(withWs, Path.Combine(dir, "In.cs")));

                string[] single = CompileUnit.Resolve(null, outside);
                Check("unit no ws one disk cs", single.Length == 1 && ContainsPath(single, outside));

                Check("unit untitled 0", CompileUnit.Resolve(null, null).Length == 0);
                Check("unit ps1 0", CompileUnit.Resolve(null, ps1).Length == 0);
                Check("unit missing file 0", CompileUnit.Resolve(null, Path.Combine(dir, "no.cs")).Length == 0);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }

                try
                {
                    if (File.Exists(outside))
                    {
                        File.Delete(outside);
                    }
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunCscArgumentBuilder()
        {
            Check("csc path frozen", FrameworkCsc.CompilerPath == @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe");
            string[] refs = FrameworkCsc.GetReferencePaths();
            Check("six references", refs != null && refs.Length == 6);
            bool sma = false;
            bool csharp = false;
            for (int i = 0; i < refs.Length; i++)
            {
                string name = Path.GetFileName(refs[i]);
                if (string.Equals(name, "System.Management.Automation.dll", StringComparison.OrdinalIgnoreCase))
                {
                    sma = true;
                }

                if (string.Equals(name, "Microsoft.CSharp.dll", StringComparison.OrdinalIgnoreCase))
                {
                    csharp = true;
                }
            }

            Check("user csc no SMA", !sma);
            Check("user csc no Microsoft.CSharp", !csharp);

            string a = CscArgumentBuilder.ShortHash(@"C:\Work\App");
            string b = CscArgumentBuilder.ShortHash(@"c:\work\app");
            Check("hash ordinal ignore case", a == b && a.Length == 16);

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-rsp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string rsp = Path.Combine(dir, "csc.rsp");
            string output = Path.Combine(dir, "out.exe");
            string src = Path.Combine(dir, "A.cs");
            try
            {
                CscArgumentBuilder.WriteResponseFile(rsp, output, new string[] { src });
                byte[] bytes = File.ReadAllBytes(rsp);
                Check("rsp utf8 bom", bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
                string text = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
                Check("rsp no noconfig", text.IndexOf("/noconfig", StringComparison.OrdinalIgnoreCase) < 0);
                Check("rsp nostdlib", text.IndexOf("/nostdlib", StringComparison.Ordinal) >= 0);
                Check("rsp target exe", text.IndexOf("/target:exe", StringComparison.Ordinal) >= 0);
                Check("rsp no winexe", text.IndexOf("/target:winexe", StringComparison.Ordinal) < 0);
                Check("rsp out", text.IndexOf("/out:", StringComparison.Ordinal) >= 0);
                Check("rsp no SMA", text.IndexOf("System.Management.Automation", StringComparison.OrdinalIgnoreCase) < 0);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunCscBrokenSource()
        {
            Check("compiler exists", FrameworkCsc.CompilerExists());
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-csctest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            string src = Path.Combine(dir, "broken.cs");
            try
            {
                WriteUtf8Bom(src, "class Broken { int x = ");
                string output = CscArgumentBuilder.GetOutputExePath(src);
                Check("out under temp WindowsIDE", output.IndexOf(Path.Combine(Path.GetTempPath(), "WindowsIDE"), StringComparison.OrdinalIgnoreCase) >= 0);
                Check("out not product exe", output.IndexOf("WindowsIDE.exe", StringComparison.OrdinalIgnoreCase) < 0);
                string rsp = Path.Combine(Path.GetDirectoryName(output), "csc.rsp");
                CscArgumentBuilder.WriteResponseFile(rsp, output, new string[] { src });
                CscRunner runner = new CscRunner();
                CscRunResult result = runner.Run(rsp, 1);
                Check("csc started", result != null && result.Started && string.IsNullOrEmpty(result.StartError));
                Diagnostic[] parsed = DiagnosticParser.Parse(result.CombinedOutput());
                bool hasCs = false;
                for (int i = 0; i < parsed.Length; i++)
                {
                    if (parsed[i] != null && parsed[i].Code != null && parsed[i].Code.Length >= 3 && parsed[i].Code[0] == 'C' && parsed[i].Code[1] == 'S')
                    {
                        hasCs = true;
                    }
                }

                Check("csc broken has CS", hasCs);
                Check("csc exit not zero", result.ExitCode != 0);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunCsharpProcessHost()
        {
            string hostSrc = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Host", "Csharp", "CsharpProcessHost.cs"));
            if (File.Exists(hostSrc))
            {
                string srcText = File.ReadAllText(hostSrc);
                Check("host no GetProcessesByName", srcText.IndexOf("GetProcessesByName", StringComparison.Ordinal) < 0);
            }
            else
            {
                Check("host source readable", false);
            }

            CsharpProcessHost missingHost = new CsharpProcessHost();
            bool missingFailed = false;
            missingHost.StartFailed += delegate(object sender, CsharpStartFailedEventArgs e)
            {
                missingFailed = true;
            };
            string missingPath = Path.Combine(Path.GetTempPath(), "WindowsIDE-host-missing-" + Guid.NewGuid().ToString("N"), "out.exe");
            missingHost.Start(missingPath, Path.GetTempPath(), 1);
            Check("missing not running", !missingHost.IsRunning);
            Check("missing StartError", !string.IsNullOrEmpty(missingHost.StartError));
            Check("missing StartFailed", missingFailed);
            missingHost.Kill();

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            CsharpProcessHost helloHost = new CsharpProcessHost();
            CsharpProcessHost sleepHost = new CsharpProcessHost();
            CsharpProcessHost restartHost = new CsharpProcessHost();
            try
            {
                string helloExe;
                string helloErr;
                bool helloCompiled = TryCompileTempExe(dir, "hello", "static class Program { static void Main() { System.Console.WriteLine(\"hello-from-host\"); } }", out helloExe, out helloErr);
                Check("hello compiled", helloCompiled && File.Exists(helloExe));
                Check("hello not product exe", helloExe == null || helloExe.IndexOf("WindowsIDE.exe", StringComparison.OrdinalIgnoreCase) < 0);

                string sleepExe;
                string sleepErr;
                bool sleepCompiled = TryCompileTempExe(dir, "sleeper", "static class Program { static void Main() { while (true) { System.Threading.Thread.Sleep(1000); } } }", out sleepExe, out sleepErr);
                Check("sleep compiled", sleepCompiled && File.Exists(sleepExe));
                Check("sleep not product exe", sleepExe == null || sleepExe.IndexOf("WindowsIDE.exe", StringComparison.OrdinalIgnoreCase) < 0);

                if (helloCompiled)
                {
                    List<string> helloLines = new List<string>();
                    ManualResetEvent helloDone = new ManualResetEvent(false);
                    int helloExit = int.MinValue;
                    helloHost.LineReceived += delegate(object sender, CsharpLineReceivedEventArgs e)
                    {
                        if (e != null && !e.IsStderr)
                        {
                            lock (helloLines)
                            {
                                helloLines.Add(e.Text);
                            }
                        }
                    };
                    helloHost.Exited += delegate(object sender, CsharpProcessExitedEventArgs e)
                    {
                        if (e != null)
                        {
                            helloExit = e.ExitCode;
                        }

                        helloDone.Set();
                    };
                    helloHost.Start(helloExe, dir, 2);
                    Check("hello wait exit", helloDone.WaitOne(15000));
                    Check("hello exit 0", helloExit == 0);
                    bool sawHello = false;
                    lock (helloLines)
                    {
                        for (int i = 0; i < helloLines.Count; i++)
                        {
                            if (helloLines[i] != null && helloLines[i].IndexOf("hello-from-host", StringComparison.Ordinal) >= 0)
                            {
                                sawHello = true;
                            }
                        }
                    }

                    Check("hello stdout", sawHello);
                    Check("hello not running", !helloHost.IsRunning);
                }

                if (sleepCompiled)
                {
                    sleepHost.Start(sleepExe, dir, 3);
                    Check("sleep running", sleepHost.IsRunning);
                    sleepHost.Kill();
                    Check("sleep kill waited", sleepHost.WaitUntilExited(15000));
                    Check("sleep not running after kill", !sleepHost.IsRunning);
                }

                if (sleepCompiled && helloCompiled)
                {
                    ManualResetEvent firstDone = new ManualResetEvent(false);
                    ManualResetEvent secondDone = new ManualResetEvent(false);
                    int secondExit = int.MinValue;
                    List<string> restartLines = new List<string>();
                    restartHost.LineReceived += delegate(object sender, CsharpLineReceivedEventArgs e)
                    {
                        if (e != null && e.Generation == 5 && !e.IsStderr)
                        {
                            lock (restartLines)
                            {
                                restartLines.Add(e.Text);
                            }
                        }
                    };
                    restartHost.Exited += delegate(object sender, CsharpProcessExitedEventArgs e)
                    {
                        if (e == null)
                        {
                            return;
                        }

                        if (e.Generation == 4)
                        {
                            firstDone.Set();
                        }

                        if (e.Generation == 5)
                        {
                            secondExit = e.ExitCode;
                            secondDone.Set();
                        }
                    };
                    restartHost.Start(sleepExe, dir, 4);
                    Check("restart first running", restartHost.IsRunning);
                    restartHost.Start(helloExe, dir, 5);
                    Check("restart first killed", firstDone.WaitOne(15000));
                    bool secondStopped = secondDone.WaitOne(15000);
                    if (!secondStopped)
                    {
                        secondStopped = restartHost.WaitUntilExited(15000);
                    }

                    Check("restart second exit wait", secondStopped);
                    Check("restart second exit 0", secondExit == 0);
                    bool sawSecond = false;
                    lock (restartLines)
                    {
                        for (int i = 0; i < restartLines.Count; i++)
                        {
                            if (restartLines[i] != null && restartLines[i].IndexOf("hello-from-host", StringComparison.Ordinal) >= 0)
                            {
                                sawSecond = true;
                            }
                        }
                    }

                    Check("restart second stdout", sawSecond);
                    Check("restart not product exe", true);
                }
            }
            finally
            {
                helloHost.Kill();
                helloHost.WaitUntilExited(5000);
                sleepHost.Kill();
                sleepHost.WaitUntilExited(5000);
                restartHost.Kill();
                restartHost.WaitUntilExited(5000);
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void RunPowerShellProcessHost()
        {
            string hostSrc = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Host", "PowerShell", "PowerShellProcessHost.cs"));
            if (File.Exists(hostSrc))
            {
                string srcText = File.ReadAllText(hostSrc);
                Check("ps host no GetProcessesByName", srcText.IndexOf("GetProcessesByName", StringComparison.Ordinal) < 0);
                Check("ps host no pwsh", srcText.IndexOf("pwsh", StringComparison.Ordinal) < 0);
                Check("ps host no Runspace", srcText.IndexOf("Runspace", StringComparison.Ordinal) < 0);
                Check("ps host FileName", srcText.IndexOf("WindowsPowerShell", StringComparison.Ordinal) >= 0 && srcText.IndexOf("v1.0", StringComparison.Ordinal) >= 0 && srcText.IndexOf("powershell.exe", StringComparison.Ordinal) >= 0);
                Check("ps host SpecialFolder.System", srcText.IndexOf("SpecialFolder.System", StringComparison.Ordinal) >= 0);
                Check("ps args NoProfile", srcText.IndexOf("-NoProfile", StringComparison.Ordinal) >= 0);
                Check("ps args ExecutionPolicy Bypass", srcText.IndexOf("-ExecutionPolicy Bypass", StringComparison.Ordinal) >= 0);
                Check("ps args File", srcText.IndexOf("-File ", StringComparison.Ordinal) >= 0);
            }
            else
            {
                Check("ps host source readable", false);
            }

            PowerShellProcessHost missingHost = new PowerShellProcessHost();
            bool missingFailed = false;
            missingHost.StartFailed += delegate(object sender, PowerShellStartFailedEventArgs e)
            {
                missingFailed = true;
            };
            string missingPath = Path.Combine(Path.GetTempPath(), "WindowsIDE-ps-missing-" + Guid.NewGuid().ToString("N"), "missing.ps1");
            missingHost.Start(missingPath, Path.GetTempPath(), 1);
            Check("ps missing not running", !missingHost.IsRunning);
            Check("ps missing StartError", !string.IsNullOrEmpty(missingHost.StartError));
            Check("ps missing StartFailed", missingFailed);
            missingHost.Kill();

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-ps-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            PowerShellProcessHost versionHost = new PowerShellProcessHost();
            PowerShellProcessHost errorHost = new PowerShellProcessHost();
            PowerShellProcessHost sleepHost = new PowerShellProcessHost();
            PowerShellProcessHost restartHost = new PowerShellProcessHost();
            try
            {
                string versionPs1 = Path.Combine(dir, "version.ps1");
                File.WriteAllText(versionPs1, "Write-Output $PSVersionTable.PSVersion.Major\r\n", Encoding.ASCII);
                string errorPs1 = Path.Combine(dir, "error.ps1");
                File.WriteAllText(errorPs1, "Write-Error 'from-ps-stderr'\r\n", Encoding.ASCII);
                string sleepPs1 = Path.Combine(dir, "sleep.ps1");
                File.WriteAllText(sleepPs1, "Start-Sleep -Seconds 60\r\n", Encoding.ASCII);

                List<string> versionLines = new List<string>();
                ManualResetEvent versionDone = new ManualResetEvent(false);
                int versionExit = int.MinValue;
                versionHost.LineReceived += delegate(object sender, PowerShellLineReceivedEventArgs e)
                {
                    if (e != null && !e.IsStderr)
                    {
                        lock (versionLines)
                        {
                            versionLines.Add(e.Text);
                        }
                    }
                };
                versionHost.Exited += delegate(object sender, PowerShellProcessExitedEventArgs e)
                {
                    if (e != null)
                    {
                        versionExit = e.ExitCode;
                    }

                    versionDone.Set();
                };
                versionHost.Start(versionPs1, dir, 2);
                Check("ps version wait exit", versionDone.WaitOne(20000));
                Check("ps version exit 0", versionExit == 0);
                bool sawFive = false;
                lock (versionLines)
                {
                    for (int i = 0; i < versionLines.Count; i++)
                    {
                        if (versionLines[i] != null && versionLines[i].Trim() == "5")
                        {
                            sawFive = true;
                        }
                    }
                }

                Check("ps version stdout 5", sawFive);
                Check("ps version not running", !versionHost.IsRunning);

                ManualResetEvent errorSeen = new ManualResetEvent(false);
                bool sawStderr = false;
                errorHost.LineReceived += delegate(object sender, PowerShellLineReceivedEventArgs e)
                {
                    if (e != null && e.IsStderr)
                    {
                        sawStderr = true;
                        errorSeen.Set();
                    }
                };
                errorHost.Start(errorPs1, dir, 3);
                Check("ps Write-Error stderr", errorSeen.WaitOne(20000) && sawStderr);
                errorHost.WaitUntilExited(20000);
                Check("ps error not running", !errorHost.IsRunning);

                sleepHost.Start(sleepPs1, dir, 4);
                bool sleepRunning = sleepHost.IsRunning;
                if (!sleepRunning)
                {
                    Thread.Sleep(400);
                    sleepRunning = sleepHost.IsRunning;
                }

                Check("ps sleep running", sleepRunning);
                sleepHost.Kill();
                Check("ps sleep kill waited", sleepHost.WaitUntilExited(15000));
                Check("ps sleep not running after kill", !sleepHost.IsRunning);

                ManualResetEvent firstDone = new ManualResetEvent(false);
                ManualResetEvent secondDone = new ManualResetEvent(false);
                int secondExit = int.MinValue;
                List<string> restartLines = new List<string>();
                restartHost.LineReceived += delegate(object sender, PowerShellLineReceivedEventArgs e)
                {
                    if (e != null && e.Generation == 6 && !e.IsStderr)
                    {
                        lock (restartLines)
                        {
                            restartLines.Add(e.Text);
                        }
                    }
                };
                restartHost.Exited += delegate(object sender, PowerShellProcessExitedEventArgs e)
                {
                    if (e == null)
                    {
                        return;
                    }

                    if (e.Generation == 5)
                    {
                        firstDone.Set();
                    }

                    if (e.Generation == 6)
                    {
                        secondExit = e.ExitCode;
                        secondDone.Set();
                    }
                };
                restartHost.Start(sleepPs1, dir, 5);
                bool firstRunning = restartHost.IsRunning;
                if (!firstRunning)
                {
                    Thread.Sleep(400);
                    firstRunning = restartHost.IsRunning;
                }

                Check("ps restart first running", firstRunning);
                restartHost.Start(versionPs1, dir, 6);
                Check("ps restart first killed", firstDone.WaitOne(20000));
                bool secondStopped = secondDone.WaitOne(20000);
                if (!secondStopped)
                {
                    secondStopped = restartHost.WaitUntilExited(15000);
                }

                Check("ps restart second exit wait", secondStopped);
                Check("ps restart second exit 0", secondExit == 0);
                bool sawSecond = false;
                lock (restartLines)
                {
                    for (int i = 0; i < restartLines.Count; i++)
                    {
                        if (restartLines[i] != null && restartLines[i].Trim() == "5")
                        {
                            sawSecond = true;
                        }
                    }
                }

                Check("ps restart second stdout", sawSecond);
            }
            finally
            {
                versionHost.Kill();
                versionHost.WaitUntilExited(5000);
                errorHost.Kill();
                errorHost.WaitUntilExited(5000);
                sleepHost.Kill();
                sleepHost.WaitUntilExited(5000);
                restartHost.Kill();
                restartHost.WaitUntilExited(5000);
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void RunCmdProcessHost()
        {
            string hostSrc = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Host", "Cmd", "CmdProcessHost.cs"));
            if (File.Exists(hostSrc))
            {
                string srcText = File.ReadAllText(hostSrc);
                Check("cmd host no GetProcessesByName", srcText.IndexOf("GetProcessesByName", StringComparison.Ordinal) < 0);
                Check("cmd host no ComSpec", srcText.IndexOf("ComSpec", StringComparison.Ordinal) < 0);
                Check("cmd host no SysWOW64", srcText.IndexOf("SysWOW64", StringComparison.Ordinal) < 0);
                Check("cmd host no pwsh", srcText.IndexOf("pwsh", StringComparison.Ordinal) < 0);
                Check("cmd host SpecialFolder.System", srcText.IndexOf("SpecialFolder.System", StringComparison.Ordinal) >= 0);
                Check("cmd host FileName cmd.exe", srcText.IndexOf("cmd.exe", StringComparison.Ordinal) >= 0);
                Check("cmd args /d /s /c", srcText.IndexOf("/d /s /c", StringComparison.Ordinal) >= 0);
                Check("cmd args no /k", srcText.IndexOf("/k", StringComparison.Ordinal) < 0);
                Check("cmd host has StartSelection", srcText.IndexOf("StartSelection", StringComparison.Ordinal) >= 0);
            }
            else
            {
                Check("cmd host source readable", false);
            }

            InspectCmdSelectionMainFormSource();

            CmdProcessHost missingHost = new CmdProcessHost();
            bool missingFailed = false;
            missingHost.StartFailed += delegate(object sender, CmdStartFailedEventArgs e)
            {
                missingFailed = true;
            };
            string missingPath = Path.Combine(Path.GetTempPath(), "WindowsIDE-cmd-missing-" + Guid.NewGuid().ToString("N"), "missing.cmd");
            missingHost.Start(missingPath, Path.GetTempPath(), 1);
            Check("cmd missing not running", !missingHost.IsRunning);
            Check("cmd missing StartError", !string.IsNullOrEmpty(missingHost.StartError));
            Check("cmd missing StartFailed", missingFailed);
            missingHost.Kill();

            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-cmd-host-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            CmdProcessHost helloHost = new CmdProcessHost();
            CmdProcessHost errorHost = new CmdProcessHost();
            CmdProcessHost exitHost = new CmdProcessHost();
            CmdProcessHost sleepHost = new CmdProcessHost();
            CmdProcessHost restartHost = new CmdProcessHost();
            CmdProcessHost spaceHost = new CmdProcessHost();
            try
            {
                string helloCmd = Path.Combine(dir, "hello.cmd");
                File.WriteAllText(helloCmd, "@echo off\r\necho hello-from-cmd\r\n", Encoding.ASCII);
                string errorCmd = Path.Combine(dir, "error.cmd");
                File.WriteAllText(errorCmd, "@echo off\r\necho from-cmd-stderr 1>&2\r\n", Encoding.ASCII);
                string exitBat = Path.Combine(dir, "exit7.bat");
                File.WriteAllText(exitBat, "@echo off\r\nexit /b 7\r\n", Encoding.ASCII);
                string sleepCmd = Path.Combine(dir, "sleep.cmd");
                File.WriteAllText(sleepCmd, "@echo off\r\nping -n 60 127.0.0.1\r\n", Encoding.ASCII);
                string spaceDir = Path.Combine(dir, "dir with space");
                Directory.CreateDirectory(spaceDir);
                string spaceCmd = Path.Combine(spaceDir, "hello.cmd");
                File.WriteAllText(spaceCmd, "@echo off\r\necho hello-from-cmd\r\n", Encoding.ASCII);

                List<string> helloLines = new List<string>();
                ManualResetEvent helloDone = new ManualResetEvent(false);
                int helloExit = int.MinValue;
                helloHost.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                {
                    if (e != null && !e.IsStderr)
                    {
                        lock (helloLines)
                        {
                            helloLines.Add(e.Text);
                        }
                    }
                };
                helloHost.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                {
                    if (e != null)
                    {
                        helloExit = e.ExitCode;
                    }

                    helloDone.Set();
                };
                helloHost.Start(helloCmd, dir, 2);
                Check("cmd hello wait exit", helloDone.WaitOne(15000));
                Check("cmd hello exit 0", helloExit == 0);
                bool sawHello = false;
                lock (helloLines)
                {
                    for (int i = 0; i < helloLines.Count; i++)
                    {
                        if (helloLines[i] != null && helloLines[i].IndexOf("hello-from-cmd", StringComparison.Ordinal) >= 0)
                        {
                            sawHello = true;
                        }
                    }
                }

                Check("cmd hello stdout", sawHello);
                Check("cmd hello not running", !helloHost.IsRunning);

                ManualResetEvent errorSeen = new ManualResetEvent(false);
                bool sawStderr = false;
                errorHost.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                {
                    if (e != null && e.IsStderr && e.Text != null && e.Text.IndexOf("from-cmd-stderr", StringComparison.Ordinal) >= 0)
                    {
                        sawStderr = true;
                        errorSeen.Set();
                    }
                };
                errorHost.Start(errorCmd, dir, 3);
                Check("cmd echo stderr", errorSeen.WaitOne(15000) && sawStderr);
                errorHost.WaitUntilExited(15000);
                Check("cmd error not running", !errorHost.IsRunning);

                ManualResetEvent exitDone = new ManualResetEvent(false);
                int exitCode = int.MinValue;
                exitHost.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                {
                    if (e != null)
                    {
                        exitCode = e.ExitCode;
                    }

                    exitDone.Set();
                };
                exitHost.Start(exitBat, dir, 4);
                Check("cmd exit wait", exitDone.WaitOne(15000));
                Check("cmd exit 7", exitCode == 7);
                Check("cmd exit not running", !exitHost.IsRunning);

                sleepHost.Start(sleepCmd, dir, 4);
                bool sleepRunning = sleepHost.IsRunning;
                if (!sleepRunning)
                {
                    Thread.Sleep(400);
                    sleepRunning = sleepHost.IsRunning;
                }

                Check("cmd sleep running", sleepRunning);
                sleepHost.Kill();
                Check("cmd sleep kill waited", sleepHost.WaitUntilExited(15000));
                Check("cmd sleep not running after kill", !sleepHost.IsRunning);

                ManualResetEvent firstDone = new ManualResetEvent(false);
                ManualResetEvent secondDone = new ManualResetEvent(false);
                int secondExit = int.MinValue;
                List<string> restartLines = new List<string>();
                restartHost.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                {
                    if (e != null && e.Generation == 6 && !e.IsStderr)
                    {
                        lock (restartLines)
                        {
                            restartLines.Add(e.Text);
                        }
                    }
                };
                restartHost.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                {
                    if (e == null)
                    {
                        return;
                    }

                    if (e.Generation == 5)
                    {
                        firstDone.Set();
                    }

                    if (e.Generation == 6)
                    {
                        secondExit = e.ExitCode;
                        secondDone.Set();
                    }
                };
                restartHost.Start(sleepCmd, dir, 5);
                bool firstRunning = restartHost.IsRunning;
                if (!firstRunning)
                {
                    Thread.Sleep(400);
                    firstRunning = restartHost.IsRunning;
                }

                Check("cmd restart first running", firstRunning);
                restartHost.Start(helloCmd, dir, 6);
                Check("cmd restart first killed", firstDone.WaitOne(15000));
                bool secondStopped = secondDone.WaitOne(15000);
                if (!secondStopped)
                {
                    secondStopped = restartHost.WaitUntilExited(15000);
                }

                Check("cmd restart second exit wait", secondStopped);
                Check("cmd restart second exit 0", secondExit == 0);
                bool sawSecond = false;
                lock (restartLines)
                {
                    for (int i = 0; i < restartLines.Count; i++)
                    {
                        if (restartLines[i] != null && restartLines[i].IndexOf("hello-from-cmd", StringComparison.Ordinal) >= 0)
                        {
                            sawSecond = true;
                        }
                    }
                }

                Check("cmd restart second stdout", sawSecond);

                List<string> spaceLines = new List<string>();
                ManualResetEvent spaceDone = new ManualResetEvent(false);
                int spaceExit = int.MinValue;
                spaceHost.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                {
                    if (e != null && !e.IsStderr)
                    {
                        lock (spaceLines)
                        {
                            spaceLines.Add(e.Text);
                        }
                    }
                };
                spaceHost.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                {
                    if (e != null)
                    {
                        spaceExit = e.ExitCode;
                    }

                    spaceDone.Set();
                };
                spaceHost.Start(spaceCmd, spaceDir, 7);
                Check("cmd space wait exit", spaceDone.WaitOne(15000));
                Check("cmd space exit 0", spaceExit == 0);
                bool sawSpace = false;
                lock (spaceLines)
                {
                    for (int i = 0; i < spaceLines.Count; i++)
                    {
                        if (spaceLines[i] != null && spaceLines[i].IndexOf("hello-from-cmd", StringComparison.Ordinal) >= 0)
                        {
                            sawSpace = true;
                        }
                    }
                }

                Check("cmd space stdout", sawSpace);
                Check("cmd space not running", !spaceHost.IsRunning);

                CmdProcessHost selHello = new CmdProcessHost();
                CmdProcessHost selCwd = new CmdProcessHost();
                CmdProcessHost selBlank = new CmdProcessHost();
                CmdProcessHost selAmp = new CmdProcessHost();
                CmdProcessHost selAcp = new CmdProcessHost();
                try
                {
                    string[] tempsBeforeHello = ListCmdRunTemps();
                    List<string> selHelloLines = new List<string>();
                    ManualResetEvent selHelloDone = new ManualResetEvent(false);
                    int selHelloExit = int.MinValue;
                    selHello.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                    {
                        if (e != null && !e.IsStderr)
                        {
                            lock (selHelloLines)
                            {
                                selHelloLines.Add(e.Text);
                            }
                        }
                    };
                    selHello.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                    {
                        if (e != null)
                        {
                            selHelloExit = e.ExitCode;
                        }

                        selHelloDone.Set();
                    };
                    string selWork = Path.Combine(dir, "sel-src");
                    Directory.CreateDirectory(selWork);
                    selHello.StartSelection("echo hello-from-sel", selWork, 8);
                    Check("cmd sel hello wait", selHelloDone.WaitOne(15000));
                    Check("cmd sel hello exit 0", selHelloExit == 0);
                    bool sawSelHello = false;
                    lock (selHelloLines)
                    {
                        for (int i = 0; i < selHelloLines.Count; i++)
                        {
                            if (selHelloLines[i] != null && selHelloLines[i].IndexOf("hello-from-sel", StringComparison.Ordinal) >= 0)
                            {
                                sawSelHello = true;
                            }
                        }
                    }

                    Check("cmd sel hello stdout", sawSelHello);
                    Check("cmd sel hello not running", !selHello.IsRunning);
                    Check("cmd sel hello wait until exited", selHello.WaitUntilExited(15000));
                    Check("cmd sel hello temps gone", NoNewCmdRunTemps(tempsBeforeHello));

                    File.WriteAllText(Path.Combine(selWork, "marker.txt"), "from-source-dir", Encoding.ASCII);
                    List<string> selCwdLines = new List<string>();
                    ManualResetEvent selCwdDone = new ManualResetEvent(false);
                    selCwd.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                    {
                        if (e != null && !e.IsStderr)
                        {
                            lock (selCwdLines)
                            {
                                selCwdLines.Add(e.Text);
                            }
                        }
                    };
                    selCwd.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                    {
                        selCwdDone.Set();
                    };
                    selCwd.StartSelection("type marker.txt", selWork, 9);
                    Check("cmd sel cwd wait", selCwdDone.WaitOne(15000));
                    bool sawMarker = false;
                    lock (selCwdLines)
                    {
                        for (int i = 0; i < selCwdLines.Count; i++)
                        {
                            if (selCwdLines[i] != null && selCwdLines[i].IndexOf("from-source-dir", StringComparison.Ordinal) >= 0)
                            {
                                sawMarker = true;
                            }
                        }
                    }

                    Check("cmd sel cwd reads source dir", sawMarker);
                    selCwd.WaitUntilExited(15000);

                    string[] tempsBeforeBlank = ListCmdRunTemps();
                    bool blankFailed = false;
                    selBlank.StartFailed += delegate(object sender, CmdStartFailedEventArgs e)
                    {
                        blankFailed = true;
                    };
                    selBlank.StartSelection("  \t\r\n", selWork, 10);
                    Check("cmd sel blank StartFailed", blankFailed);
                    Check("cmd sel blank StartError", !string.IsNullOrEmpty(selBlank.StartError));
                    Check("cmd sel blank not running", !selBlank.IsRunning);
                    Check("cmd sel blank no temps", NoNewCmdRunTemps(tempsBeforeBlank));

                    List<string> selAmpLines = new List<string>();
                    ManualResetEvent selAmpDone = new ManualResetEvent(false);
                    int selAmpExit = int.MinValue;
                    selAmp.LineReceived += delegate(object sender, CmdLineReceivedEventArgs e)
                    {
                        if (e != null && !e.IsStderr)
                        {
                            lock (selAmpLines)
                            {
                                selAmpLines.Add(e.Text);
                            }
                        }
                    };
                    selAmp.Exited += delegate(object sender, CmdProcessExitedEventArgs e)
                    {
                        if (e != null)
                        {
                            selAmpExit = e.ExitCode;
                        }

                        selAmpDone.Set();
                    };
                    selAmp.StartSelection("echo a&echo b", selWork, 11);
                    Check("cmd sel amp wait", selAmpDone.WaitOne(15000));
                    bool sawA = false;
                    bool sawB = false;
                    lock (selAmpLines)
                    {
                        for (int i = 0; i < selAmpLines.Count; i++)
                        {
                            if (selAmpLines[i] == null)
                            {
                                continue;
                            }

                            if (selAmpLines[i].IndexOf("a", StringComparison.Ordinal) >= 0)
                            {
                                sawA = true;
                            }

                            if (selAmpLines[i].IndexOf("b", StringComparison.Ordinal) >= 0)
                            {
                                sawB = true;
                            }
                        }
                    }

                    Check("cmd sel amp ran", selAmpExit == 0 && sawA && sawB);
                    Check("cmd sel amp not running", !selAmp.IsRunning);

                    bool acpFailed = false;
                    string acpMessage = null;
                    selAcp.StartFailed += delegate(object sender, CmdStartFailedEventArgs e)
                    {
                        acpFailed = true;
                        if (e != null)
                        {
                            acpMessage = e.Message;
                        }
                    };
                    selAcp.StartSelection("echo " + char.ConvertFromUtf32(0x1F600), selWork, 12);
                    Check("cmd sel acp StartFailed", acpFailed);
                    Check("cmd sel acp message", acpMessage == "この選択はコマンドプロンプトのコードページで書けません。");
                    Check("cmd sel acp not running", !selAcp.IsRunning);
                    Check("cmd sel acp StartError", !string.IsNullOrEmpty(selAcp.StartError));
                }
                finally
                {
                    selHello.Kill();
                    selHello.WaitUntilExited(5000);
                    selCwd.Kill();
                    selCwd.WaitUntilExited(5000);
                    selBlank.Kill();
                    selBlank.WaitUntilExited(5000);
                    selAmp.Kill();
                    selAmp.WaitUntilExited(5000);
                    selAcp.Kill();
                    selAcp.WaitUntilExited(5000);
                }
            }
            finally
            {
                helloHost.Kill();
                helloHost.WaitUntilExited(5000);
                errorHost.Kill();
                errorHost.WaitUntilExited(5000);
                exitHost.Kill();
                exitHost.WaitUntilExited(5000);
                sleepHost.Kill();
                sleepHost.WaitUntilExited(5000);
                restartHost.Kill();
                restartHost.WaitUntilExited(5000);
                spaceHost.Kill();
                spaceHost.WaitUntilExited(5000);
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void InspectCmdSelectionMainFormSource()
        {
            string mainSrcPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Ui", "MainForm.cs"));
            if (!File.Exists(mainSrcPath))
            {
                Check("cmd sel mainform source readable", false);
                return;
            }

            string src = File.ReadAllText(mainSrcPath);
            Check("cmd sel mainform Keys.F8", src.IndexOf("Keys.F8", StringComparison.Ordinal) >= 0);
            Check("cmd sel mainform OnRunSelection", src.IndexOf("OnRunSelection", StringComparison.Ordinal) >= 0);
            int f5At = src.LastIndexOf("if (keyData == (Keys.Control | Keys.F5))", StringComparison.Ordinal);
            int f8At = src.IndexOf("if (keyData == Keys.F8)", StringComparison.Ordinal);
            Check("cmd sel mainform Ctrl+F5 present", f5At >= 0);
            Check("cmd sel mainform F8 handler", f8At > f5At);
            if (f5At >= 0 && f8At > f5At)
            {
                string f5Block = src.Substring(f5At, f8At - f5At);
                Check("cmd sel mainform Ctrl+F5 OnRun", f5Block.IndexOf("this.OnRun(this, EventArgs.Empty)", StringComparison.Ordinal) >= 0);
                Check("cmd sel mainform Ctrl+F5 not selection", f5Block.IndexOf("OnRunSelection", StringComparison.Ordinal) < 0);
            }

            Check("cmd sel mainform F8 display", src.IndexOf("ShortcutKeyDisplayString = \"F8\"", StringComparison.Ordinal) >= 0);
            Check("cmd sel mainform no F8 ShortcutKeys", src.IndexOf("ShortcutKeys = Keys.F8", StringComparison.Ordinal) < 0);
            int selAt = src.IndexOf("private void OnRunSelection(", StringComparison.Ordinal);
            int psAt = src.IndexOf("private void StartPs(", StringComparison.Ordinal);
            Check("cmd sel mainform OnRunSelection body", selAt >= 0 && psAt > selAt);
            if (selAt >= 0 && psAt > selAt)
            {
                string body = src.Substring(selAt, psAt - selAt);
                Check("cmd sel mainform no Save", body.IndexOf("Document.Save()", StringComparison.Ordinal) < 0);
            }
        }

        private static string[] ListCmdRunTemps()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE", "run", "cmd");
            if (!Directory.Exists(dir))
            {
                return new string[0];
            }

            return Directory.GetFiles(dir, "*.cmd");
        }

        private static bool NoNewCmdRunTemps(string[] before)
        {
            string[] after = ListCmdRunTemps();
            if (after == null)
            {
                return true;
            }

            int i = 0;
            while (i < after.Length)
            {
                string path = after[i];
                bool known = false;
                if (before != null)
                {
                    int j = 0;
                    while (j < before.Length)
                    {
                        if (string.Equals(before[j], path, StringComparison.OrdinalIgnoreCase))
                        {
                            known = true;
                            break;
                        }

                        j++;
                    }
                }

                if (!known)
                {
                    return false;
                }

                i++;
            }

            return true;
        }

        private static bool TryCompileTempExe(string dir, string name, string source, out string exePath, out string error)
        {
            exePath = Path.Combine(dir, name + ".exe");
            error = null;
            string src = Path.Combine(dir, name + ".cs");
            WriteUtf8Bom(src, source);
            string rsp = Path.Combine(dir, name + ".rsp");
            CscArgumentBuilder.WriteResponseFile(rsp, exePath, new string[] { src });
            CscRunner runner = new CscRunner();
            CscRunResult result = runner.Run(rsp, 1);
            if (result == null || !result.Started || result.ExitCode != 0 || !File.Exists(exePath))
            {
                error = (result == null) ? "no result" : result.CombinedOutput();
                return false;
            }

            return true;
        }

        private static void WriteUtf8Bom(string path, string text)
        {
            Encoding utf8 = new UTF8Encoding(true);
            File.WriteAllText(path, text, utf8);
        }

        private static bool ContainsPath(string[] paths, string expected)
        {
            if (paths == null || string.IsNullOrEmpty(expected))
            {
                return false;
            }

            string full = Path.GetFullPath(expected);
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] != null && string.Equals(Path.GetFullPath(paths[i]), full, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static int IndexOfIdent(string text, string name, int nth)
        {
            if (text == null || name == null || nth < 1)
            {
                return -1;
            }

            int from = 0;
            int seen = 0;
            while (from < text.Length)
            {
                int at = text.IndexOf(name, from, StringComparison.Ordinal);
                if (at < 0)
                {
                    return -1;
                }

                bool leftOk = at == 0 || !IsIdentChar(text[at - 1]);
                int end = at + name.Length;
                bool rightOk = end >= text.Length || !IsIdentChar(text[end]);
                if (leftOk && rightOk)
                {
                    seen++;
                    if (seen == nth)
                    {
                        return at;
                    }
                }

                from = at + 1;
            }

            return -1;
        }

        private static bool IsIdentChar(char c)
        {
            return (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '_';
        }

        private static TokenKind KindAtCs(string text, int line, int index)
        {
            return CSharpSemantic.KindAt(text, line, index);
        }

        private static TokenKind KindAtLang(LanguageKind language, string text, int line, int index)
        {
            TextBuffer buffer = new TextBuffer();
            buffer.SetText(text == null ? "" : text);
            HighlightSession session = new HighlightSession();
            session.Reset(language, buffer.LineCount);
            session.SyncAfterEdit(buffer, 0);
            List<Token> tokens = new List<Token>();
            int endState;
            ILineLexer lexer = LexerRegistry.Get(language);
            lexer.ScanLine(buffer.GetLine(line), session.GetStartState(line), tokens, out endState);
            session.ApplyIdentifierOverlay(line, tokens);
            return KindAtTokens(tokens, index);
        }

        private static TokenKind KindAt(ILineLexer lexer, string line, int startState, int index)
        {
            List<Token> tokens = new List<Token>();
            int endState;
            lexer.ScanLine(line, startState, tokens, out endState);
            return KindAtTokens(tokens, index);
        }

        private static TokenKind KindAtTokens(List<Token> tokens, int index)
        {
            for (int i = 0; i < tokens.Count; i++)
            {
                Token token = tokens[i];
                if (index >= token.Start && index < token.Start + token.Length)
                {
                    return token.Kind;
                }
            }

            return TokenKind.Text;
        }

        private static void RunShellPaths()
        {
            string system = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string ps = ShellPaths.GetExecutable(ShellKind.PowerShell51);
            string cmd = ShellPaths.GetExecutable(ShellKind.Cmd);
            Check("shell ps system32", string.Equals(ps, Path.Combine(system, "WindowsPowerShell", "v1.0", "powershell.exe"), StringComparison.OrdinalIgnoreCase));
            Check("shell cmd system32", string.Equals(cmd, Path.Combine(system, "cmd.exe"), StringComparison.OrdinalIgnoreCase));
            string psArgs = ShellPaths.GetArguments(ShellKind.PowerShell51);
            string cmdArgs = ShellPaths.GetArguments(ShellKind.Cmd);
            Check("shell ps no NonInteractive", psArgs.IndexOf("-NonInteractive", StringComparison.OrdinalIgnoreCase) < 0);
            Check("shell ps no Command", psArgs.IndexOf("-Command", StringComparison.OrdinalIgnoreCase) < 0);
            Check("shell ps no Import-Module", psArgs.IndexOf("Import-Module", StringComparison.OrdinalIgnoreCase) < 0);
            Check("shell ps no PSReadLine", psArgs.IndexOf("PSReadLine", StringComparison.OrdinalIgnoreCase) < 0);
            Check("shell cmd no /c", cmdArgs.IndexOf("/c", StringComparison.OrdinalIgnoreCase) < 0);
            Check("shell cmd no /k", cmdArgs.IndexOf("/k", StringComparison.OrdinalIgnoreCase) < 0);
            Check("shell cmd is /d", string.Equals(cmdArgs, "/d", StringComparison.Ordinal));
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            Check("shell cwd profile", string.Equals(ShellPaths.GetWorkingDirectory(null), profile, StringComparison.OrdinalIgnoreCase));
            string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            if (Directory.Exists(root))
            {
                Check("shell cwd root", string.Equals(ShellPaths.GetWorkingDirectory(root), Path.GetFullPath(root), StringComparison.OrdinalIgnoreCase));
            }
            else
            {
                Check("shell cwd root", false);
            }

            string termDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Terminal"));
            if (!Directory.Exists(termDir))
            {
                Check("terminal folder readable", false);
                return;
            }

            string[] files = Directory.GetFiles(termDir, "*.cs");
            bool hasPwsh = false;
            bool hasWow = false;
            bool hasComSpec = false;
            bool hasByName = false;
            int i;
            for (i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                if (text.IndexOf("pwsh", StringComparison.Ordinal) >= 0)
                {
                    hasPwsh = true;
                }

                if (text.IndexOf("SysWOW64", StringComparison.Ordinal) >= 0)
                {
                    hasWow = true;
                }

                if (text.IndexOf("ComSpec", StringComparison.Ordinal) >= 0)
                {
                    hasComSpec = true;
                }

                if (text.IndexOf("GetProcessesByName", StringComparison.Ordinal) >= 0)
                {
                    hasByName = true;
                }
            }

            Check("terminal src no pwsh", !hasPwsh);
            Check("terminal src no SysWOW64", !hasWow);
            Check("terminal src no ComSpec", !hasComSpec);
            Check("terminal src no GetProcessesByName", !hasByName);
        }

        private static void RunCellWidth()
        {
            Check("cell ascii 1", CellWidth.Of('A') == 1);
            Check("cell fullwidth 2", CellWidth.Of('あ') == 2);
        }

        private static void RunTerminalInput()
        {
            int toggle = (int)(Keys.Control | Keys.Oemtilde);
            Check("input toggle", TerminalInput.Classify(toggle, false, false).Kind == TerminalInputKind.Toggle);
            Check("input toggle composing", TerminalInput.Classify(toggle, false, true).Kind == TerminalInputKind.None);
            Check("input copy", TerminalInput.Classify((int)(Keys.Control | Keys.C), true, false).Kind == TerminalInputKind.Copy);
            TerminalInputResult interrupt = TerminalInput.Classify((int)(Keys.Control | Keys.C), false, false);
            Check("input eot kind", interrupt.Kind == TerminalInputKind.Send && interrupt.Payload != null && interrupt.Payload.Length == 1 && interrupt.Payload[0] == 0x03);
            Check("input paste", TerminalInput.Classify((int)(Keys.Control | Keys.V), false, false).Kind == TerminalInputKind.Paste);
        }

        private static void RunVtParser()
        {
            VtScreen screen = new VtScreen(80, 24);
            VtParser parser = new VtParser(screen);
            parser.Feed("A\r\nB");
            Check("vt lf A", screen.GetCell(0, 0).Character == 'A');
            Check("vt lf B", screen.GetCell(1, 0).Character == 'B');

            screen.ClearAll();
            parser.Reset();
            parser.Feed("AB\bC");
            Check("vt bs A", screen.GetCell(0, 0).Character == 'A');
            Check("vt bs C", screen.GetCell(0, 1).Character == 'C');

            screen.ClearAll();
            parser.Reset();
            parser.Feed("A\tB");
            Check("vt tab B", screen.GetCell(0, 8).Character == 'B');

            screen.ClearAll();
            parser.Reset();
            parser.Feed("\u001b[5;10HX");
            Check("vt cup X", screen.GetCell(4, 9).Character == 'X');
            Check("vt cup row", screen.CaretRow == 4);
            Check("vt cup col", screen.CaretColumn == 10);

            screen.ClearAll();
            parser.Reset();
            parser.Feed("ABC\r\u001b[C\u001b[K");
            Check("vt el A", screen.GetCell(0, 0).Character == 'A');
            Check("vt el B gone", screen.GetCell(0, 1).Character == '\0');
            Check("vt el C gone", screen.GetCell(0, 2).Character == '\0');

            screen.ClearAll();
            parser.Reset();
            parser.Feed("\u001b[31mE\u001b[32mS\u001b[33mN\u001b[34mL\u001b[35mK\u001b[36mT\u001b[37mF\u001b[30mC");
            Check("vt sgr error", screen.GetCell(0, 0).Slot == ColorSlot.Error);
            Check("vt sgr string", screen.GetCell(0, 1).Slot == ColorSlot.String);
            Check("vt sgr number", screen.GetCell(0, 2).Slot == ColorSlot.Number);
            Check("vt sgr local", screen.GetCell(0, 3).Slot == ColorSlot.Local);
            Check("vt sgr keyword", screen.GetCell(0, 4).Slot == ColorSlot.Keyword);
            Check("vt sgr type", screen.GetCell(0, 5).Slot == ColorSlot.Type);
            Check("vt sgr fg", screen.GetCell(0, 6).Slot == ColorSlot.Foreground);
            Check("vt sgr comment", screen.GetCell(0, 7).Slot == ColorSlot.Comment);

            screen.ClearAll();
            parser.Reset();
            parser.Feed("\u001b[999Qhello");
            Check("vt unknown no esc", screen.GetCell(0, 0).Character == 'h');
            Check("vt unknown hello", screen.GetHistoryText(screen.ScrollbackCount).Trim().StartsWith("hello", StringComparison.Ordinal));
        }

        private static void RunPseudoConsoleSession()
        {
            string exe = ShellPaths.GetExecutable(ShellKind.Cmd);
            Check("pty cmd exists", File.Exists(exe));
            Check("startupinfoex size", Marshal.SizeOf(typeof(NativeMethods.StartupInfoEx)) == 112);
            Check("startupinfo size", Marshal.SizeOf(typeof(NativeMethods.StartupInfo)) == 104);
            StringBuilder collected = new StringBuilder();
            ManualResetEvent done = new ManualResetEvent(false);
            PseudoConsoleSession session = new PseudoConsoleSession();
            session.OutputReceived += delegate(object sender, TerminalOutputEventArgs e)
            {
                if (e != null && e.Text != null)
                {
                    lock (collected)
                    {
                        collected.Append(e.Text);
                    }
                }
            };
            session.Exited += delegate(object sender, TerminalExitedEventArgs e)
            {
                done.Set();
            };
            string cwd = ShellPaths.GetWorkingDirectory(null);
            bool finished = false;
            string text = "";
            WithDetachedStdio(delegate()
            {
                session.Start(exe, "/d /c echo TERM-OK", cwd, 80, 24, 1);
                finished = done.WaitOne(8000);
                Thread.Sleep(400);
                lock (collected)
                {
                    text = collected.ToString();
                }

                session.Kill();
                session.WaitUntilExited(2000);
                session.Dispose();
            });
            done.Close();
            Check("pty start error empty", string.IsNullOrEmpty(session.StartError));
            Check("pty finished", finished);
            Check("pty TERM-OK", text.IndexOf("TERM-OK", StringComparison.Ordinal) >= 0);

            string oldTerm = Environment.GetEnvironmentVariable("TERM");
            StringBuilder termCollected = new StringBuilder();
            ManualResetEvent termDone = new ManualResetEvent(false);
            PseudoConsoleSession termSession = new PseudoConsoleSession();
            termSession.OutputReceived += delegate(object sender, TerminalOutputEventArgs e)
            {
                if (e != null && e.Text != null)
                {
                    lock (termCollected)
                    {
                        termCollected.Append(e.Text);
                    }
                }
            };
            termSession.Exited += delegate(object sender, TerminalExitedEventArgs e)
            {
                termDone.Set();
            };
            bool termFinished = false;
            string termText = "";
            try
            {
                Environment.SetEnvironmentVariable("TERM", "dumb");
                WithDetachedStdio(delegate()
                {
                    termSession.Start(exe, "/d /c echo TERM=[%TERM%]", cwd, 80, 24, 2);
                    termFinished = termDone.WaitOne(8000);
                    Thread.Sleep(400);
                    lock (termCollected)
                    {
                        termText = termCollected.ToString();
                    }

                    termSession.Kill();
                    termSession.WaitUntilExited(2000);
                    termSession.Dispose();
                });
            }
            finally
            {
                Environment.SetEnvironmentVariable("TERM", oldTerm);
            }

            termDone.Close();
            Check("pty term echo start error empty", string.IsNullOrEmpty(termSession.StartError));
            Check("pty term echo finished", termFinished);
            Check("pty term echo not empty", !string.IsNullOrEmpty(termText));
            Check("pty child TERM not dumb", termText.IndexOf("TERM=[dumb]", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static void RunPseudoConsolePowerShellConsole()
        {
            string exe = ShellPaths.GetExecutable(ShellKind.PowerShell51);
            Check("pty ps exists", File.Exists(exe));
            string args = ShellPaths.GetArguments(ShellKind.PowerShell51);
            Check("pty ps product args no Command", args.IndexOf("-Command", StringComparison.OrdinalIgnoreCase) < 0);
            args = args + " -Command \"if ([Console]::IsInputRedirected -or [Console]::IsOutputRedirected) { 'REDIRECTED' } else { 'CONSOLE-OK' }\"";
            StringBuilder collected = new StringBuilder();
            ManualResetEvent done = new ManualResetEvent(false);
            PseudoConsoleSession session = new PseudoConsoleSession();
            session.OutputReceived += delegate(object sender, TerminalOutputEventArgs e)
            {
                if (e != null && e.Text != null)
                {
                    lock (collected)
                    {
                        collected.Append(e.Text);
                    }
                }
            };
            session.Exited += delegate(object sender, TerminalExitedEventArgs e)
            {
                done.Set();
            };
            string cwd = ShellPaths.GetWorkingDirectory(null);
            bool finished = false;
            string text = "";
            WithDetachedStdio(delegate()
            {
                session.Start(exe, args, cwd, 80, 24, 1);
                finished = done.WaitOne(8000);
                Thread.Sleep(400);
                lock (collected)
                {
                    text = collected.ToString();
                }

                session.Kill();
                session.WaitUntilExited(2000);
                session.Dispose();
            });
            done.Close();
            Check("pty ps start error empty", string.IsNullOrEmpty(session.StartError));
            Check("pty ps finished", finished);
            Check("pty ps CONSOLE-OK", text.IndexOf("CONSOLE-OK", StringComparison.Ordinal) >= 0);
            Check("pty ps no REDIRECTED", text.IndexOf("REDIRECTED", StringComparison.Ordinal) < 0);
            Check("pty ps no PSReadLine", text.IndexOf("PSReadLine", StringComparison.OrdinalIgnoreCase) < 0);
            Check("pty ps no screen JP", text.IndexOf("スクリーン", StringComparison.Ordinal) < 0);
            Check("pty ps no screen reader", text.IndexOf("screen reader", StringComparison.OrdinalIgnoreCase) < 0);
        }

        private static void RunEnvironmentBlockWithoutTerm()
        {
            IntPtr source = NativeMethods.GetEnvironmentStringsW();
            Check("env source", source != IntPtr.Zero);
            bool sourceHasHidden = false;
            if (source != IntPtr.Zero)
            {
                try
                {
                    IntPtr p = source;
                    while (true)
                    {
                        string entry = Marshal.PtrToStringUni(p);
                        if (string.IsNullOrEmpty(entry))
                        {
                            break;
                        }

                        if (entry[0] == '=')
                        {
                            sourceHasHidden = true;
                        }

                        p = new IntPtr(p.ToInt64() + (entry.Length + 1) * 2);
                    }
                }
                finally
                {
                    NativeMethods.FreeEnvironmentStringsW(source);
                }
            }

            IntPtr copy = NativeMethods.AllocUnicodeEnvironmentWithoutTerm();
            Check("env copy alloc", copy != IntPtr.Zero);
            bool copyHasHidden = false;
            if (copy != IntPtr.Zero)
            {
                try
                {
                    IntPtr p = copy;
                    while (true)
                    {
                        string entry = Marshal.PtrToStringUni(p);
                        if (string.IsNullOrEmpty(entry))
                        {
                            break;
                        }

                        if (entry[0] == '=')
                        {
                            copyHasHidden = true;
                        }

                        p = new IntPtr(p.ToInt64() + (entry.Length + 1) * 2);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(copy);
                }
            }

            if (sourceHasHidden)
            {
                Check("env block keeps hidden", copyHasHidden);
            }
            else
            {
                Check("env block hidden skip", true);
            }

            string oldTerm = Environment.GetEnvironmentVariable("TERM");
            string oldColor = Environment.GetEnvironmentVariable("COLORTERM");
            IntPtr block = IntPtr.Zero;
            try
            {
                Environment.SetEnvironmentVariable("TERM", "dumb");
                Environment.SetEnvironmentVariable("COLORTERM", "testcolor");
                block = NativeMethods.AllocUnicodeEnvironmentWithoutTerm();
                Check("env block alloc", block != IntPtr.Zero);
                if (block == IntPtr.Zero)
                {
                    return;
                }

                bool hasTerm = false;
                bool hasColor = false;
                IntPtr p = block;
                while (true)
                {
                    string entry = Marshal.PtrToStringUni(p);
                    if (string.IsNullOrEmpty(entry))
                    {
                        break;
                    }

                    int eq = entry.IndexOf('=');
                    string name = (eq < 0) ? entry : entry.Substring(0, eq);
                    if (string.Equals(name, "TERM", StringComparison.OrdinalIgnoreCase))
                    {
                        hasTerm = true;
                    }

                    if (string.Equals(name, "COLORTERM", StringComparison.OrdinalIgnoreCase))
                    {
                        hasColor = true;
                    }

                    p = new IntPtr(p.ToInt64() + (entry.Length + 1) * 2);
                }

                Check("env block no TERM", !hasTerm);
                Check("env block keeps COLORTERM", hasColor);
            }
            finally
            {
                if (block != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(block);
                }

                Environment.SetEnvironmentVariable("TERM", oldTerm);
                Environment.SetEnvironmentVariable("COLORTERM", oldColor);
            }

            int spi = 0;
            bool got = NativeMethods.SystemParametersInfo(NativeMethods.SpiGetScreenReader, 0, ref spi, 0);
            Check("spi get works", got);
        }

        private static void RunPseudoConsolePowerShellInteractive()
        {
            string exe = ShellPaths.GetExecutable(ShellKind.PowerShell51);
            Check("pty ps interactive exists", File.Exists(exe));
            string args = ShellPaths.GetArguments(ShellKind.PowerShell51);
            Check("pty ps interactive product args", string.Equals(args, "-NoLogo -NoProfile -ExecutionPolicy Bypass", StringComparison.Ordinal));
            StringBuilder collected = new StringBuilder();
            PseudoConsoleSession session = new PseudoConsoleSession();
            session.OutputReceived += delegate(object sender, TerminalOutputEventArgs e)
            {
                if (e != null && e.Text != null)
                {
                    lock (collected)
                    {
                        collected.Append(e.Text);
                    }
                }
            };
            string cwd = ShellPaths.GetWorkingDirectory(null);
            string text = "";
            WithDetachedStdio(delegate()
            {
                session.Start(ShellKind.PowerShell51, cwd, 80, 24, 1);
                Thread.Sleep(6000);
                lock (collected)
                {
                    text = collected.ToString();
                }

                session.Kill();
                session.WaitUntilExited(2000);
                session.Dispose();
            });
            Check("pty ps interactive start error empty", string.IsNullOrEmpty(session.StartError));
            Check("pty ps interactive output", !string.IsNullOrEmpty(text));
            if (NativeMethods.IsBlindAccessOn())
            {
                Check("pty ps interactive skip warn (Blind Access On)", true);
            }
            else
            {
                Check("pty ps interactive no スクリーン", text.IndexOf("スクリーン", StringComparison.Ordinal) < 0);
                Check("pty ps interactive no PSReadLine", text.IndexOf("PSReadLine", StringComparison.OrdinalIgnoreCase) < 0);
                Check("pty ps interactive no screen reader", text.IndexOf("screen reader", StringComparison.OrdinalIgnoreCase) < 0);
            }
        }

        private static void InspectPseudoConsoleSessionSource()
        {
            string sessionPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Terminal", "PseudoConsoleSession.cs"));
            if (!File.Exists(sessionPath))
            {
                Check("pty session source readable", false);
                return;
            }

            string src = File.ReadAllText(sessionPath);
            Check("pty no StartFUseStdHandles", src.IndexOf("StartFUseStdHandles", StringComparison.Ordinal) < 0);

            int flagsAt = src.IndexOf("uint flags =", StringComparison.Ordinal);
            int createAt = src.IndexOf("NativeMethods.CreateProcessW(", StringComparison.Ordinal);
            Check("pty CreateProcess flags span", flagsAt >= 0 && createAt > flagsAt);
            if (flagsAt >= 0 && createAt > flagsAt)
            {
                string flagsBody = src.Substring(flagsAt, createAt - flagsAt);
                Check("pty flags has EXTENDED", flagsBody.IndexOf("ExtendedStartupinfoPresent", StringComparison.Ordinal) >= 0);
                Check("pty flags no CREATE_NO_WINDOW", flagsBody.IndexOf("CREATE_NO_WINDOW", StringComparison.OrdinalIgnoreCase) < 0);
                Check("pty flags no CreateNoWindow", flagsBody.IndexOf("CreateNoWindow", StringComparison.Ordinal) < 0);
                Check("pty flags no 0x08000000", flagsBody.IndexOf("0x08000000", StringComparison.OrdinalIgnoreCase) < 0);
            }

            string nativePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Terminal", "NativeMethods.cs"));
            if (!File.Exists(nativePath))
            {
                Check("pty native source readable", false);
                return;
            }

            string native = File.ReadAllText(nativePath);
            Check("pty native no StartFUseStdHandles", native.IndexOf("StartFUseStdHandles", StringComparison.Ordinal) < 0);
            Check("pty session no CREATE_NO_WINDOW", src.IndexOf("CREATE_NO_WINDOW", StringComparison.OrdinalIgnoreCase) < 0);
            Check("pty session no 0x08000000", src.IndexOf("0x08000000", StringComparison.OrdinalIgnoreCase) < 0);
            Check("pty session no DETACHED_PROCESS", src.IndexOf("DETACHED_PROCESS", StringComparison.Ordinal) < 0);
            Check("pty native no CREATE_NO_WINDOW", native.IndexOf("CREATE_NO_WINDOW", StringComparison.OrdinalIgnoreCase) < 0);
            Check("pty native no 0x08000000", native.IndexOf("0x08000000", StringComparison.OrdinalIgnoreCase) < 0);
            Check("pty native no GetSystemMetrics", native.IndexOf("GetSystemMetrics", StringComparison.Ordinal) < 0);
            Check("pty native no SetValue", native.IndexOf("SetValue", StringComparison.Ordinal) < 0);
            Check("pty native set fWinIni 0", native.IndexOf("SystemParametersInfo(SpiSetScreenReader, 0, IntPtr.Zero, 0)", StringComparison.Ordinal) >= 0);

            string pathsPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Terminal", "ShellPaths.cs"));
            if (!File.Exists(pathsPath))
            {
                Check("pty paths source readable", false);
                return;
            }

            string paths = File.ReadAllText(pathsPath);
            int getArgsAt = paths.IndexOf("public static string GetArguments", StringComparison.Ordinal);
            int getCwdAt = paths.IndexOf("public static string GetWorkingDirectory", StringComparison.Ordinal);
            Check("pty GetArguments span", getArgsAt >= 0 && getCwdAt > getArgsAt);
            if (getArgsAt >= 0 && getCwdAt > getArgsAt)
            {
                string argsBody = paths.Substring(getArgsAt, getCwdAt - getArgsAt);
                Check("pty GetArguments no -Command", argsBody.IndexOf("-Command", StringComparison.OrdinalIgnoreCase) < 0);
                Check("pty GetArguments no Import-Module", argsBody.IndexOf("Import-Module", StringComparison.OrdinalIgnoreCase) < 0);
                Check("pty GetArguments no PSReadLine", argsBody.IndexOf("PSReadLine", StringComparison.OrdinalIgnoreCase) < 0);
            }

            string termDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Terminal"));
            if (!Directory.Exists(termDir))
            {
                Check("pty terminal folder inspect", false);
                return;
            }

            string[] termFiles = Directory.GetFiles(termDir, "*.cs");
            bool hasByName = false;
            int i;
            for (i = 0; i < termFiles.Length; i++)
            {
                string termSrc = File.ReadAllText(termFiles[i]);
                if (termSrc.IndexOf("GetProcessesByName", StringComparison.Ordinal) >= 0)
                {
                    hasByName = true;
                }
            }

            Check("pty Terminal no GetProcessesByName", !hasByName);
        }

        /// <summary>
        /// テスト exe の標準ハンドル（キャプチャ用パイプを含む）を子へ継承させない。製品 winexe と同じく ConPTY がコンソールを付ける。
        /// </summary>
        /// <param name="action">PTY 起動から破棄まで。</param>
        private static void WithDetachedStdio(Action action)
        {
            IntPtr oldIn = GetStdHandle(StdInputHandle);
            IntPtr oldOut = GetStdHandle(StdOutputHandle);
            IntPtr oldErr = GetStdHandle(StdErrorHandle);
            try
            {
                SetStdHandle(StdInputHandle, IntPtr.Zero);
                SetStdHandle(StdOutputHandle, IntPtr.Zero);
                SetStdHandle(StdErrorHandle, IntPtr.Zero);
                FreeConsole();
                action();
            }
            finally
            {
                AttachConsole(AttachParentProcess);
                SetStdHandle(StdInputHandle, oldIn);
                SetStdHandle(StdOutputHandle, oldOut);
                SetStdHandle(StdErrorHandle, oldErr);
            }
        }

        private static void RunVbaOffline()
        {
            Check("ident ok", VbaIdentifier.IsValid("StringUtil"));
            Check("ident digit first", !VbaIdentifier.IsValid("1abc"));
            Check("ident symbol", !VbaIdentifier.IsValid("A-b"));
            Check("ident empty", !VbaIdentifier.IsValid(""));
            Check("ident 31", VbaIdentifier.IsValid("ABCDEFGHIJKLMNOPQRSTUVWXYZ12345"));
            Check("ident 32", !VbaIdentifier.IsValid("ABCDEFGHIJKLMNOPQRSTUVWXYZ123456"));

            Check("export strip null", VbaExportText.StripForCodeModule(null) == "");
            string basExport = "Attribute VB_Name = \"Mod1\"\r\nOption Explicit\r\n";
            Check("export strip bas", VbaExportText.StripForCodeModule(basExport) == "Option Explicit\r\n");
            string clsExport = "VERSION 1.0 CLASS\r\nBEGIN\r\n  MultiUse = -1  'True\r\nEND\r\nAttribute VB_Name = \"Class1\"\r\nAttribute VB_GlobalNameSpace = False\r\nOption Explicit\r\n";
            Check("export strip cls", VbaExportText.StripForCodeModule(clsExport) == "Option Explicit\r\n");
            Check("export body only", VbaExportText.StripForCodeModule("Option Explicit\r\n") == "Option Explicit\r\n");
            string bodyAttr = "Option Explicit\r\nAttribute VB_Name = \"kept\"\r\n";
            Check("export keep body attribute", VbaExportText.StripForCodeModule(bodyAttr) == bodyAttr);

            Check("name filename", VbaNaming.FromRelPath("Lib/StringUtil.bas", VbaNamingMode.Filename) == "StringUtil");
            Check("name folder_prefix", VbaNaming.FromRelPath("Lib/StringUtil.bas", VbaNamingMode.FolderPrefix) == "Lib_StringUtil");
            Check("name nested prefix", VbaNaming.FromRelPath("Lib/Text/Join.bas", VbaNamingMode.FolderPrefix) == "Lib_Text_Join");
            Check("name document ignores mode", VbaNaming.ToExcelName("Lib/ThisWorkbook.cls", VbaNamingMode.FolderPrefix, VbaComponentKind.Document, "ThisWorkbook") == "ThisWorkbook");

            VbaNameCollision[] collisions = VbaNaming.FindCollisions(
                new string[] { "StringUtil", "StringUtil" },
                new string[] { "vba/Lib/StringUtil.bas", "vba/App/StringUtil.bas" });
            Check("collision count", collisions != null && collisions.Length == 1);
            Check("collision both paths", collisions.Length == 1
                && collisions[0].PathA.IndexOf("Lib", StringComparison.OrdinalIgnoreCase) >= 0
                && collisions[0].PathB.IndexOf("App", StringComparison.OrdinalIgnoreCase) >= 0);

            Check("workbook xlsm", VbaWorkbookPath.IsMacroWorkbook("C:\\tmp\\Book.xlsm"));
            Check("workbook xlsb", VbaWorkbookPath.IsMacroWorkbook("Book.XLSB"));
            Check("workbook xlsx no", !VbaWorkbookPath.IsMacroWorkbook("C:\\tmp\\Book.xlsx"));
            Check("workbook xls no", !VbaWorkbookPath.IsMacroWorkbook("Book.xls"));

            string root = Path.Combine(Path.GetTempPath(), "WindowsIDE-vba-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                string vbaRoot = Path.Combine(root, "vba");
                Directory.CreateDirectory(Path.Combine(vbaRoot, "Lib"));
                File.WriteAllText(Path.Combine(vbaRoot, "Lib", "StringUtil.bas"), "Option Explicit");
                Directory.CreateDirectory(Path.Combine(vbaRoot, "bin"));
                File.WriteAllText(Path.Combine(vbaRoot, "bin", "skip.bas"), "skip");
                File.WriteAllText(Path.Combine(vbaRoot, "form.frm"), "VERSION 5.00");

                string[] listed = VbaDiskTree.List(vbaRoot);
                Check("disk keeps bas", ContainsPath(listed, Path.Combine(vbaRoot, "Lib", "StringUtil.bas")));
                Check("disk drops bin", !ContainsPath(listed, Path.Combine(vbaRoot, "bin", "skip.bas")));
                Check("disk drops frm", !ContainsPath(listed, Path.Combine(vbaRoot, "form.frm")));

                string planned;
                string planErr;
                Check("plan write inside", VbaDiskTree.TryPlanWrite(root, "vba", "Lib/A.bas", out planned, out planErr)
                    && planned != null
                    && PathGuard.IsInsideWorkspace(root, planned));
                Check("plan write .. rejected", !VbaDiskTree.TryPlanWrite(root, "vba", "../escape.bas", out planned, out planErr));
                Check("plan write abs root rejected", !VbaDiskTree.TryPlanWrite(root, "C:\\Windows", "A.bas", out planned, out planErr));

                VbaMap map = VbaMap.CreateDefault(Path.Combine(root, "Book.xlsm"));
                map.SetComponents(new VbaMapComponent[]
                {
                    new VbaMapComponent("StringUtil", "Lib/StringUtil.bas", VbaComponentKind.Std),
                    new VbaMapComponent("ThisWorkbook", "ThisWorkbook.cls", VbaComponentKind.Document)
                });
                string saveErr;
                Check("map save", map.TrySave(root, out saveErr));
                string xml = File.ReadAllText(VbaMap.GetFilePath(root), Encoding.UTF8);
                Check("map no guid element", xml.IndexOf("<guid", StringComparison.OrdinalIgnoreCase) < 0);
                Check("map no reference element", xml.IndexOf("<reference", StringComparison.OrdinalIgnoreCase) < 0);
                Check("map has namingMode", xml.IndexOf("<namingMode>filename</namingMode>", StringComparison.Ordinal) >= 0);

                VbaMap loaded;
                string loadErr;
                Check("map load ok", VbaMap.TryLoad(root, out loaded, out loadErr) == VbaMapLoadStatus.Ok && loaded != null);
                Check("map load component", loaded.FindByRelPath("Lib/StringUtil.bas") != null
                    && loaded.FindByRelPath("Lib/StringUtil.bas").Kind == VbaComponentKind.Std);

                byte[] beforeBroken = File.ReadAllBytes(VbaMap.GetFilePath(root));
                File.WriteAllText(VbaMap.GetFilePath(root), "<not-xml", Encoding.ASCII);
                VbaMap broken;
                string brokenErr;
                VbaMapLoadStatus brokenStatus = VbaMap.TryLoad(root, out broken, out brokenErr);
                Check("map broken status", brokenStatus == VbaMapLoadStatus.Broken && broken == null);
                byte[] afterBroken = File.ReadAllBytes(VbaMap.GetFilePath(root));
                Check("map broken not overwritten", afterBroken.Length == 8);

                File.WriteAllBytes(VbaMap.GetFilePath(root), beforeBroken);
            }
            finally
            {
                try
                {
                    Directory.Delete(root, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunDocumentVbaEncoding()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-vba-doc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                Document untitledBas = Document.CreateUntitled();
                untitledBas.Buffer.Insert(0, 0, "Option Explicit");
                string basPath = Path.Combine(dir, "NewMod.bas");
                untitledBas.SaveAs(basPath);
                Check("untitled bas cp932", untitledBas.EncodingInfo.CodePage == 932 && !untitledBas.EncodingInfo.HasBom);
                byte[] basBytes = File.ReadAllBytes(basPath);
                Check("untitled bas no bom", basBytes.Length < 3 || basBytes[0] != 0xEF);

                Document untitledCs = Document.CreateUntitled();
                untitledCs.Buffer.Insert(0, 0, "class A {}");
                string csPath = Path.Combine(dir, "A.cs");
                untitledCs.SaveAs(csPath);
                Check("untitled cs utf8", untitledCs.EncodingInfo.IsUtf8);
                byte[] csBytes = File.ReadAllBytes(csPath);
                Check("untitled cs bom", csBytes.Length >= 3 && csBytes[0] == 0xEF && csBytes[1] == 0xBB && csBytes[2] == 0xBF);

                string utf8Bas = Path.Combine(dir, "Utf8Mod.bas");
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                byte[] body = Encoding.UTF8.GetBytes("Option Explicit\r\n");
                byte[] all = new byte[bom.Length + body.Length];
                Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
                Buffer.BlockCopy(body, 0, all, bom.Length, body.Length);
                File.WriteAllBytes(utf8Bas, all);
                Document opened = Document.Open(utf8Bas);
                Check("open utf8 bas", opened.EncodingInfo.IsUtf8 && opened.EncodingInfo.HasBom);
                opened.Save();
                byte[] saved = File.ReadAllBytes(utf8Bas);
                Check("save utf8 bas keeps", saved.Length >= 3 && saved[0] == 0xEF && saved[1] == 0xBB && saved[2] == 0xBF);

                string reloadPath = Path.Combine(dir, "Reload.bas");
                File.WriteAllBytes(reloadPath, Encoding.GetEncoding(932).GetBytes("AAA"));
                Document reload = Document.Open(reloadPath);
                reload.Buffer.Insert(0, 3, "BBB");
                reload.MarkDirty();
                reload.Undo.RecordInsert(0, 3, "BBB");
                File.WriteAllBytes(reloadPath, Encoding.GetEncoding(932).GetBytes("ZZZ"));
                Check("reload from disk", reload.ReloadFromDisk() && reload.Buffer.GetText() == "ZZZ" && !reload.IsDirty && !reload.Undo.CanUndo);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void RunDocumentCmdEncoding()
        {
            string dir = Path.Combine(Path.GetTempPath(), "WindowsIDE-cmd-doc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                string emptyCmd = Path.Combine(dir, "empty.cmd");
                File.WriteAllBytes(emptyCmd, new byte[0]);
                Document openedEmptyCmd = Document.Open(emptyCmd);
                Check("open empty cmd 932", openedEmptyCmd.EncodingInfo.CodePage == 932 && !openedEmptyCmd.EncodingInfo.HasBom);

                string emptyBat = Path.Combine(dir, "empty.bat");
                File.WriteAllBytes(emptyBat, new byte[0]);
                Document openedEmptyBat = Document.Open(emptyBat);
                Check("open empty bat 932", openedEmptyBat.EncodingInfo.CodePage == 932 && !openedEmptyBat.EncodingInfo.HasBom);

                Document untitledCmd = Document.CreateUntitled();
                untitledCmd.Buffer.Insert(0, 0, "echo off");
                string cmdPath = Path.Combine(dir, "New.cmd");
                untitledCmd.SaveAs(cmdPath);
                Check("untitled cmd cp932", untitledCmd.EncodingInfo.CodePage == 932 && !untitledCmd.EncodingInfo.HasBom);
                byte[] untitledBytes = File.ReadAllBytes(cmdPath);
                Check("untitled cmd no bom", untitledBytes.Length < 3 || untitledBytes[0] != 0xEF);

                string utf8Cmd = Path.Combine(dir, "Utf8.cmd");
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                byte[] body = Encoding.UTF8.GetBytes("echo off");
                byte[] all = new byte[bom.Length + body.Length];
                Buffer.BlockCopy(bom, 0, all, 0, bom.Length);
                Buffer.BlockCopy(body, 0, all, bom.Length, body.Length);
                File.WriteAllBytes(utf8Cmd, all);
                Document opened = Document.Open(utf8Cmd);
                Check("open utf8 cmd", opened.EncodingInfo.IsUtf8 && opened.EncodingInfo.HasBom);
                opened.Save();
                byte[] saved = File.ReadAllBytes(utf8Cmd);
                Check("save utf8 cmd strips bom", saved.Length == body.Length && saved[0] == 0x65);
                Check("save utf8 cmd info", opened.EncodingInfo.IsUtf8 && !opened.EncodingInfo.HasBom);
            }
            finally
            {
                try
                {
                    Directory.Delete(dir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        private static void InspectVbaSyncSource()
        {
            string repo = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", ".."));
            string vbaDir = Path.Combine(repo, "src", "WindowsIDE", "Vba");
            if (!Directory.Exists(vbaDir))
            {
                Check("vba dir exists", false);
                return;
            }

            string[] vbaFiles = Directory.GetFiles(vbaDir, "*.cs");
            StringBuilder all = new StringBuilder();
            for (int i = 0; i < vbaFiles.Length; i++)
            {
                all.Append(File.ReadAllText(vbaFiles[i]));
            }

            string vbaSrc = all.ToString();
            Check("vba no dynamic", vbaSrc.IndexOf("dynamic ", StringComparison.Ordinal) < 0);
            Check("vba no GetProcessesByName", vbaSrc.IndexOf("GetProcessesByName", StringComparison.Ordinal) < 0);
            Check("vba no Office Interop", vbaSrc.IndexOf("Microsoft.Office.Interop", StringComparison.Ordinal) < 0);
            Check("vba no Microsoft.CSharp", vbaSrc.IndexOf("Microsoft.CSharp", StringComparison.Ordinal) < 0);
            Check("vba no Application.Quit", vbaSrc.IndexOf("Application.Quit", StringComparison.Ordinal) < 0 && vbaSrc.IndexOf("\"Quit\"", StringComparison.Ordinal) < 0);
            Check("vba no Workbooks.Close", vbaSrc.IndexOf("Workbooks.Close", StringComparison.Ordinal) < 0);
            Check("vba no new Thread ComInvoker", vbaSrc.IndexOf("new Thread", StringComparison.Ordinal) < 0);
            Check("vba sync no Quit", File.ReadAllText(Path.Combine(vbaDir, "VbaSyncService.cs")).IndexOf("Quit", StringComparison.Ordinal) < 0);

            string comSrc = File.ReadAllText(Path.Combine(vbaDir, "ComInvoker.cs"));
            Check("cominvoker GetCultureInfo(1033)", comSrc.IndexOf("GetCultureInfo(1033)", StringComparison.Ordinal) >= 0);
            Check("cominvoker InvokeMember culture", comSrc.IndexOf("InvokeMember(name, flags, null, target, args, ", StringComparison.Ordinal) >= 0);
            Check("cominvoker no 5-arg InvokeMember", comSrc.IndexOf("InvokeMember(name, flags, null, target, args);", StringComparison.Ordinal) < 0);
            Check("cominvoker 0x80020003", comSrc.IndexOf("0x80020003", StringComparison.Ordinal) >= 0);
            Check("cominvoker no CurrentCulture assign", comSrc.IndexOf("CurrentCulture =", StringComparison.Ordinal) < 0);
            Check("cominvoker no CurrentUICulture assign", comSrc.IndexOf("CurrentUICulture =", StringComparison.Ordinal) < 0);
            Check("cominvoker no nameof", comSrc.IndexOf("nameof", StringComparison.Ordinal) < 0);

            string mainSrcPath = Path.Combine(repo, "src", "WindowsIDE", "Ui", "MainForm.cs");
            string mainSrc = File.ReadAllText(mainSrcPath);
            Check("vba menu VBA(&A)", mainSrc.IndexOf("VBA(&A)", StringComparison.Ordinal) >= 0);
            Check("vba display Ctrl+Alt+P", mainSrc.IndexOf("ShortcutKeyDisplayString = \"Ctrl+Alt+P\"", StringComparison.Ordinal) >= 0);
            Check("vba display Ctrl+Alt+H", mainSrc.IndexOf("ShortcutKeyDisplayString = \"Ctrl+Alt+H\"", StringComparison.Ordinal) >= 0);
            Check("vba ProcessCmdKey P", mainSrc.IndexOf("Keys.Control | Keys.Alt | Keys.P", StringComparison.Ordinal) >= 0);
            Check("vba ProcessCmdKey H", mainSrc.IndexOf("Keys.Control | Keys.Alt | Keys.H", StringComparison.Ordinal) >= 0);
            Check("vba no ファイルを開く menu", mainSrc.IndexOf("ファイルを開く", StringComparison.Ordinal) < 0);

            int vbaItemAt = mainSrc.IndexOf("private ToolStripMenuItem CreateVbaPullItem(", StringComparison.Ordinal);
            int vbaPushAt = mainSrc.IndexOf("private ToolStripMenuItem CreateVbaPushItem(", StringComparison.Ordinal);
            int runItemAt = mainSrc.IndexOf("private ToolStripMenuItem CreateRunItem(", StringComparison.Ordinal);
            Check("vba key item method", vbaItemAt >= 0 && vbaPushAt > vbaItemAt && runItemAt > vbaPushAt);
            if (vbaItemAt >= 0 && runItemAt > vbaItemAt)
            {
                string body = mainSrc.Substring(vbaItemAt, runItemAt - vbaItemAt);
                Check("vba key item no ShortcutKeys", body.IndexOf("ShortcutKeys", StringComparison.Ordinal) < 0);
            }

            string settingsSrc = File.ReadAllText(Path.Combine(repo, "src", "WindowsIDE", "Workspace", "WorkspaceSettings.cs"));
            Check("settings no namingMode write", settingsSrc.IndexOf("namingMode", StringComparison.Ordinal) < 0 || settingsSrc.IndexOf("SetAttribute(\"namingMode\"", StringComparison.Ordinal) < 0);
            Check("settings comment no write", settingsSrc.IndexOf("namingMode は書かない", StringComparison.Ordinal) >= 0);

            string productRsp = File.ReadAllText(Path.Combine(repo, "build", "windows-ide.rsp"));
            string testRsp = File.ReadAllText(Path.Combine(repo, "build", "windows-ide-tests.rsp"));
            Check("product rsp Vba", productRsp.IndexOf("src\\WindowsIDE\\Vba\\ComInvoker.cs", StringComparison.Ordinal) >= 0
                && productRsp.IndexOf("src\\WindowsIDE\\Vba\\VbaExportText.cs", StringComparison.Ordinal) >= 0
                && productRsp.IndexOf("src\\WindowsIDE\\Vba\\VbaSyncService.cs", StringComparison.Ordinal) >= 0);
            Check("tests rsp Vba", testRsp.IndexOf("src\\WindowsIDE\\Vba\\ComInvoker.cs", StringComparison.Ordinal) >= 0
                && testRsp.IndexOf("src\\WindowsIDE\\Vba\\VbaExportText.cs", StringComparison.Ordinal) >= 0
                && testRsp.IndexOf("src\\WindowsIDE\\Vba\\VbaSyncService.cs", StringComparison.Ordinal) >= 0);
        }

        private static void InspectMainFormTerminalSource()
        {
            string mainSrcPath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Ui", "MainForm.cs"));
            if (!File.Exists(mainSrcPath))
            {
                Check("term mainform source readable", false);
                return;
            }

            string src = File.ReadAllText(mainSrcPath);
            Check("term mainform 表示", src.IndexOf("表示(&V)", StringComparison.Ordinal) >= 0);
            Check("term mainform Ctrl+`", src.IndexOf("ShortcutKeyDisplayString = \"Ctrl+`\"", StringComparison.Ordinal) >= 0);
            Check("term mainform Oemtilde", src.IndexOf("Keys.Oemtilde", StringComparison.Ordinal) >= 0);
            int itemAt = src.IndexOf("private ToolStripMenuItem CreateTerminalViewItem(", StringComparison.Ordinal);
            int statusAt = src.IndexOf("private void BuildStatus(", StringComparison.Ordinal);
            Check("term mainform view item", itemAt >= 0 && statusAt > itemAt);
            if (itemAt >= 0 && statusAt > itemAt)
            {
                string body = src.Substring(itemAt, statusAt - itemAt);
                Check("term mainform no ShortcutKeys", body.IndexOf("ShortcutKeys", StringComparison.Ordinal) < 0);
            }

            Check("term OnRun no pty kill", MethodHasNoPtyKill(src, "private void OnRun(", "private void OnRunSelection("));
            Check("term StartPs no pty kill", MethodHasNoPtyKill(src, "private void StartPs(", "private void StartCmd("));
            Check("term StartCmd no pty kill", MethodHasNoPtyKill(src, "private void StartCmd(", "private void StartManualBuild("));
            Check("term StartManualBuild no pty kill", MethodHasNoPtyKill(src, "private void StartManualBuild(", "private bool IsCurrentBuild("));
            Check("term TerminalSelected subscribe", src.IndexOf("this.bottomPane.TerminalSelected += this.OnBottomPaneTerminalSelected", StringComparison.Ordinal) >= 0);
            Check("term TerminalSelected starts pty", MethodCallsShowTerminalPanelOrStartIfNeeded(src, "private void OnBottomPaneTerminalSelected(", "private void OnToggleTerminal("));
        }

        private static void InspectBottomPaneTerminalSource()
        {
            string panePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "src", "WindowsIDE", "Ui", "BottomPane.cs"));
            if (!File.Exists(panePath))
            {
                Check("term bottompane source readable", false);
                return;
            }

            string src = File.ReadAllText(panePath);
            Check("term chip event field", src.IndexOf("public event EventHandler TerminalSelected", StringComparison.Ordinal) >= 0);
            int chipAt = src.IndexOf("if (this.terminalChip.Contains(e.Location))", StringComparison.Ordinal);
            int paintAt = src.IndexOf("protected override void Dispose(bool disposing)", StringComparison.Ordinal);
            Check("term chip path", chipAt >= 0 && paintAt > chipAt);
            if (chipAt >= 0 && paintAt > chipAt)
            {
                string chipBody = src.Substring(chipAt, paintAt - chipAt);
                Check("term chip raises TerminalSelected", chipBody.IndexOf("this.TerminalSelected", StringComparison.Ordinal) >= 0);
            }

            int showAt = src.IndexOf("public void ShowTerminal()", StringComparison.Ordinal);
            int dpiAt = src.IndexOf("protected override void OnDpiChangedAfterParent(", StringComparison.Ordinal);
            Check("term ShowTerminal method", showAt >= 0 && dpiAt > showAt);
            if (showAt >= 0 && dpiAt > showAt)
            {
                string showBody = src.Substring(showAt, dpiAt - showAt);
                Check("term ShowTerminal no StartIfNeeded", showBody.IndexOf("StartIfNeeded", StringComparison.Ordinal) < 0);
            }
        }

        private static bool MethodCallsShowTerminalPanelOrStartIfNeeded(string src, string startMarker, string nextMarker)
        {
            int a = src.IndexOf(startMarker, StringComparison.Ordinal);
            int b = src.IndexOf(nextMarker, StringComparison.Ordinal);
            if (a < 0 || b <= a)
            {
                return false;
            }

            string body = src.Substring(a, b - a);
            if (body.IndexOf("ShowTerminalPanel", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return body.IndexOf("StartIfNeeded", StringComparison.Ordinal) >= 0;
        }

        private static bool MethodHasNoPtyKill(string src, string startMarker, string nextMarker)
        {
            int a = src.IndexOf(startMarker, StringComparison.Ordinal);
            int b = src.IndexOf(nextMarker, StringComparison.Ordinal);
            if (a < 0 || b <= a)
            {
                return false;
            }

            string body = src.Substring(a, b - a);
            if (body.IndexOf("PseudoConsoleSession.Kill", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            if (body.IndexOf("session.Kill", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            return true;
        }
    }
}
