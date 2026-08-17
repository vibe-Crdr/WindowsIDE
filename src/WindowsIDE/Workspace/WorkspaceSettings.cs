using System.IO;
using System.Text;
using System.Xml;

namespace WindowsIDE.Workspace
{
    /// <summary>
    /// ワークスペース XML（fontSize / tabSize）。WinForms に依存しない。
    /// </summary>
    public sealed class WorkspaceSettings
    {
        private int fontSize;
        private int tabSize;

        /// <summary>既定の本文サイズ（96dpi DIP、VS Code 14 相当）。</summary>
        public const int DefaultFontSize = 14;

        /// <summary>既定の Tab スペース数。</summary>
        public const int DefaultTabSize = 4;

        /// <summary>
        /// 既定値（fontSize=DefaultFontSize DIP, tabSize=DefaultTabSize）で作る。
        /// </summary>
        public WorkspaceSettings()
        {
            this.fontSize = DefaultFontSize;
            this.tabSize = DefaultTabSize;
        }

        /// <summary>編集器のフォントサイズ（96dpi DIP）。</summary>
        public int FontSize
        {
            get { return this.fontSize; }
            set { this.fontSize = value; }
        }

        /// <summary>Tab で入れるスペース数。</summary>
        public int TabSize
        {
            get { return this.tabSize; }
            set { this.tabSize = value; }
        }

        /// <summary>
        /// workspace.xml のパスを返す。
        /// </summary>
        public static string GetFilePath(string workspaceRoot)
        {
            return Path.Combine(workspaceRoot, ".windows-ide", "workspace.xml");
        }

        /// <summary>
        /// ファイルが無ければ既定 DefaultFontSize/DefaultTabSize。壊れていても既定に戻す。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <returns>読み込んだ設定。</returns>
        public static WorkspaceSettings Load(string workspaceRoot)
        {
            WorkspaceSettings settings = new WorkspaceSettings();
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                return settings;
            }

            string path = GetFilePath(workspaceRoot);
            if (!File.Exists(path))
            {
                return settings;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlNode editor = doc.SelectSingleNode("/workspace/editor");
                if (editor != null && editor.Attributes != null)
                {
                    int n;
                    XmlAttribute fs = editor.Attributes["fontSize"];
                    if (fs != null && int.TryParse(fs.Value, out n) && n > 0 && n < 96)
                    {
                        settings.fontSize = n;
                    }

                    XmlAttribute ts = editor.Attributes["tabSize"];
                    if (ts != null && int.TryParse(ts.Value, out n) && n > 0 && n < 32)
                    {
                        settings.tabSize = n;
                    }
                }
            }
            catch (XmlException)
            {
            }
            catch (IOException)
            {
            }

            return settings;
        }

        /// <summary>
        /// `.windows-ide/workspace.xml` を書く。フォルダが無ければ作る。namingMode は書かない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        public void Save(string workspaceRoot)
        {
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                return;
            }

            string dir = Path.Combine(workspaceRoot, ".windows-ide");
            Directory.CreateDirectory(dir);
            XmlDocument doc = new XmlDocument();
            XmlDeclaration decl = doc.CreateXmlDeclaration("1.0", "utf-8", null);
            doc.AppendChild(decl);
            XmlElement root = doc.CreateElement("workspace");
            root.SetAttribute("version", "1");
            doc.AppendChild(root);
            XmlElement editor = doc.CreateElement("editor");
            editor.SetAttribute("fontSize", this.fontSize.ToString());
            editor.SetAttribute("tabSize", this.tabSize.ToString());
            root.AppendChild(editor);

            XmlWriterSettings writerSettings = new XmlWriterSettings();
            writerSettings.Indent = true;
            writerSettings.Encoding = new UTF8Encoding(true);
            using (XmlWriter writer = XmlWriter.Create(GetFilePath(workspaceRoot), writerSettings))
            {
                doc.Save(writer);
            }
        }
    }
}
