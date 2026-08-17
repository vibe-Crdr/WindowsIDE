using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using WindowsIDE.Editor;
using WindowsIDE.Languages;
using WindowsIDE.Ui;
using WindowsIDE.Ui.Fonts;
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

        /// <summary>
        /// 単体テストを順に実行する。
        /// </summary>
        [STAThread]
        public static int Main()
        {
            passed = 0;
            failed = 0;
            RunTextBuffer();
            RunFileEncoding();
            RunDocumentOpen();
            RunGlyphClassifier();
            RunPathGuard();
            RunWorkspaceSettings();
            RunFontLoaderFallback();
            RunTabSwitchScroll();
            RunDpiUtil();
            RunDualFontPainter();
            RunNativeCaption();
            RunImeLayout();
            RunStartupArgs();
            RunLanguageDetector();
            RunLexers();
            RunHighlightSession();
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
            Check("display Plain", LanguageDetector.GetDisplayName(LanguageKind.Plain) == "プレーン");

            Document untitled = Document.CreateUntitled();
            Check("untitled Language Plain", untitled.Language == LanguageKind.Plain);
        }

        private static void RunLexers()
        {
            CSharpLexer cs = new CSharpLexer();
            Check("cs class keyword", KindAt(cs, "class Foo", 0, 0) == TokenKind.Keyword);
            Check("cs classic text", KindAt(cs, "classic", 0, 0) == TokenKind.Text);
            Check("cs line comment", KindAt(cs, "int x; // hi", 0, 8) == TokenKind.Comment);
            Check("cs string", KindAt(cs, "x = \"str\";", 0, 5) == TokenKind.String);
            Check("cs char", KindAt(cs, "c = 'x';", 0, 5) == TokenKind.String);
            Check("cs decimal", KindAt(cs, "n = 123;", 0, 4) == TokenKind.Number);
            Check("cs hex", KindAt(cs, "n = 0xFF;", 0, 4) == TokenKind.Number);
            Check("cs at ident", KindAt(cs, "@class", 0, 0) == TokenKind.Text);

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
            Check("vba remote text", KindAt(vba, "Remote", 0, 0) == TokenKind.Text);

            PowerShellLexer ps = new PowerShellLexer();
            Check("ps hash comment", KindAt(ps, "# hi", 0, 0) == TokenKind.Comment);
            Check("ps if keyword", KindAt(ps, "if ($true)", 0, 0) == TokenKind.Keyword);
            Check("ps cmdlet text", KindAt(ps, "Get-ChildItem", 0, 0) == TokenKind.Text);
            Check("ps ForEach-Object text", KindAt(ps, "ForEach-Object", 0, 0) == TokenKind.Text);
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
            HighlightSession replaceSession = new HighlightSession();
            replaceSession.Reset(LanguageKind.CSharp, sameCount.LineCount);
            replaceSession.SyncAfterEdit(sameCount, 0);
            sameCount.Delete(new BufferPoint(1, 0), new BufferPoint(3, 0));
            BufferPoint replaced = sameCount.Insert(1, 0, "{\n    /*");
            replaceSession.SyncAfterEdit(sameCount, 1, replaced.Line);
            Check("same-count replace last line", replaced.Line == 2);
            Check("same-count replace opens block", replaceSession.GetStartState(3) == CSharpLexer.BlockComment);

            BufferPoint inserted = sameCount.Insert(2, sameCount.GetLine(2).Length, "\n    still");
            replaceSession.SyncAfterEdit(sameCount, 2, inserted.Line);
            Check("insert after block start line3", replaceSession.GetStartState(3) == CSharpLexer.BlockComment);
            Check("insert after block start line4", replaceSession.GetStartState(4) == CSharpLexer.BlockComment);

            sameCount.Delete(new BufferPoint(3, 0), new BufferPoint(4, 0));
            replaceSession.SyncAfterEdit(sameCount, 3, 3);
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
    }
}
