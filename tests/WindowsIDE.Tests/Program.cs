using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using WindowsIDE.Build;
using WindowsIDE.Editor;
using WindowsIDE.Languages;
using WindowsIDE.Languages.CSharp;
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
            RunIndentRules();
            RunFindRules();
            RunFileEncoding();
            RunDocumentOpen();
            RunGlyphClassifier();
            RunPathGuard();
            RunWorkspaceSettings();
            RunFontLoaderFallback();
            RunTabSwitchScroll();
            RunDpiUtil();
            RunDualFontPainter();
            RunUiMnemonic();
            RunNativeCaption();
            RunImeLayout();
            RunStartupArgs();
            RunLanguageDetector();
            RunLexers();
            RunCSharpBind();
            RunHighlightSession();
            RunDiagnosticParser();
            RunCsFileEnumerator();
            RunCompileUnit();
            RunCscArgumentBuilder();
            RunCscBrokenSource();
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
            Check("display Plain", LanguageDetector.GetDisplayName(LanguageKind.Plain) == "プレーン");

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
    }
}
