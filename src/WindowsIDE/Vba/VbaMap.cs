using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using WindowsIDE.Editor;
using WindowsIDE.Workspace;

namespace WindowsIDE.Vba
{
    /// <summary>
    /// vba-map.xml の読み書き結果。壊れた XML は上書きしない。
    /// </summary>
    public enum VbaMapLoadStatus
    {
        /// <summary>ファイルが無い。</summary>
        Missing,

        /// <summary>読めた。</summary>
        Ok,

        /// <summary>壊れているか不正。上書きしてはいけない。</summary>
        Broken
    }

    /// <summary>
    /// マップ 1 コンポーネント。
    /// </summary>
    public sealed class VbaMapComponent
    {
        private string name;
        private string relPath;
        private VbaComponentKind kind;

        /// <summary>
        /// Excel 名と root からの相対パスと種別を覚える。
        /// </summary>
        /// <param name="name">Excel 側名。</param>
        /// <param name="relPath">root からの相対（区切り /）。</param>
        /// <param name="kind">std / class / document。</param>
        public VbaMapComponent(string name, string relPath, VbaComponentKind kind)
        {
            this.name = (name == null) ? "" : name;
            this.relPath = NormalizeRelPath(relPath);
            this.kind = kind;
        }

        /// <summary>Excel 側名。</summary>
        public string Name
        {
            get { return this.name; }
        }

        /// <summary>root からの相対。区切りは /。</summary>
        public string RelPath
        {
            get { return this.relPath; }
        }

        /// <summary>種別。</summary>
        public VbaComponentKind Kind
        {
            get { return this.kind; }
        }

        /// <summary>
        /// 相対パスを / 区切りにし、先頭の / を落とす。
        /// </summary>
        /// <param name="relPath">入力。</param>
        /// <returns>正規化。</returns>
        public static string NormalizeRelPath(string relPath)
        {
            if (string.IsNullOrEmpty(relPath))
            {
                return "";
            }

            return relPath.Replace('\\', '/').Trim('/');
        }
    }

    /// <summary>
    /// `{workspace}/.windows-ide/vba-map.xml`。namingMode の正。WinForms 非依存。
    /// </summary>
    public sealed class VbaMap
    {
        private string workbookPath;
        private string rootRelative;
        private VbaNamingMode namingMode;
        private int encodingCodePage;
        private bool encodingHasBom;
        private VbaMapComponent[] components;

        private VbaMap()
        {
            this.workbookPath = "";
            this.rootRelative = "vba";
            this.namingMode = VbaNamingMode.Filename;
            this.encodingCodePage = 932;
            this.encodingHasBom = false;
            this.components = new VbaMapComponent[0];
        }

        /// <summary>ブックパス（ワークスペース内なら相対、外ならフル）。</summary>
        public string WorkbookPath
        {
            get { return this.workbookPath; }
        }

        /// <summary>VBA ルートのワークスペース相対。既定 vba。</summary>
        public string RootRelative
        {
            get { return this.rootRelative; }
        }

        /// <summary>Excel 側名の付け方。</summary>
        public VbaNamingMode NamingMode
        {
            get { return this.namingMode; }
        }

        /// <summary>同期書き込みのコードページ。既定 932。</summary>
        public int EncodingCodePage
        {
            get { return this.encodingCodePage; }
        }

        /// <summary>同期書き込みで BOM を付けるなら true。</summary>
        public bool EncodingHasBom
        {
            get { return this.encodingHasBom; }
        }

        /// <summary>コンポーネント一覧。</summary>
        public VbaMapComponent[] Components
        {
            get { return this.components; }
        }

        /// <summary>
        /// マップ XML のフルパス。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <returns>vba-map.xml のパス。</returns>
        public static string GetFilePath(string workspaceRoot)
        {
            return Path.Combine(workspaceRoot, ".windows-ide", "vba-map.xml");
        }

        /// <summary>
        /// 初回用の既定マップ（root=vba、filename、CP932 BOM なし、コンポーネント無し）。
        /// </summary>
        /// <param name="workbookPath">保存するブックパス。</param>
        /// <returns>新しいマップ。</returns>
        public static VbaMap CreateDefault(string workbookPath)
        {
            VbaMap map = new VbaMap();
            map.workbookPath = (workbookPath == null) ? "" : workbookPath;
            return map;
        }

        /// <summary>
        /// 同期書き込み用のエンコーディング。無ければ CP932 BOM なし。
        /// </summary>
        /// <returns>FileEncodingInfo。</returns>
        public FileEncodingInfo GetEncodingInfo()
        {
            int cp = (this.encodingCodePage > 0) ? this.encodingCodePage : 932;
            return new FileEncodingInfo(cp, this.encodingHasBom, false, "\r\n");
        }

        /// <summary>
        /// ブックのフルパス。相対ならワークスペースと結合する。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <returns>フルパス。無ければ null。</returns>
        public string ResolveWorkbookFullPath(string workspaceRoot)
        {
            if (string.IsNullOrEmpty(this.workbookPath))
            {
                return null;
            }

            try
            {
                if (Path.IsPathRooted(this.workbookPath))
                {
                    return Path.GetFullPath(this.workbookPath);
                }

                if (string.IsNullOrEmpty(workspaceRoot))
                {
                    return null;
                }

                return Path.GetFullPath(Path.Combine(workspaceRoot, this.workbookPath));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// VBA ルートのフルパス。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <returns>フルパス。不正なら null。</returns>
        public string ResolveRootFullPath(string workspaceRoot)
        {
            if (string.IsNullOrEmpty(workspaceRoot) || !IsSafeRootRelative(this.rootRelative))
            {
                return null;
            }

            try
            {
                string full = Path.GetFullPath(Path.Combine(workspaceRoot, this.rootRelative.Replace('/', Path.DirectorySeparatorChar)));
                if (!PathGuard.IsInsideWorkspace(workspaceRoot, full))
                {
                    return null;
                }

                return full;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// ブックパスを差し替える（相対化は呼び出し側）。
        /// </summary>
        /// <param name="path">保存するパス。</param>
        public void SetWorkbookPath(string path)
        {
            this.workbookPath = (path == null) ? "" : path;
        }

        /// <summary>
        /// namingMode だけ更新する。Excel はリネームしない。
        /// </summary>
        /// <param name="mode">新しいモード。</param>
        public void SetNamingMode(VbaNamingMode mode)
        {
            this.namingMode = mode;
        }

        /// <summary>
        /// コンポーネント一覧を差し替える。
        /// </summary>
        /// <param name="list">新しい一覧。null なら空。</param>
        public void SetComponents(VbaMapComponent[] list)
        {
            this.components = (list == null) ? new VbaMapComponent[0] : list;
        }

        /// <summary>
        /// 相対パスが一致するコンポーネントを返す。
        /// </summary>
        /// <param name="relPath">root からの相対。</param>
        /// <returns>無ければ null。</returns>
        public VbaMapComponent FindByRelPath(string relPath)
        {
            string n = VbaMapComponent.NormalizeRelPath(relPath);
            if (string.IsNullOrEmpty(n))
            {
                return null;
            }

            for (int i = 0; i < this.components.Length; i++)
            {
                if (string.Equals(this.components[i].RelPath, n, StringComparison.OrdinalIgnoreCase))
                {
                    return this.components[i];
                }
            }

            return null;
        }

        /// <summary>
        /// Excel 名が一致するコンポーネントを返す。
        /// </summary>
        /// <param name="name">Excel 名。</param>
        /// <returns>無ければ null。</returns>
        public VbaMapComponent FindByName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            for (int i = 0; i < this.components.Length; i++)
            {
                if (string.Equals(this.components[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return this.components[i];
                }
            }

            return null;
        }

        /// <summary>
        /// マップを読む。無い・壊れている・不正を区別する。壊れていたら上書きしないこと。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="map">成功時のマップ。</param>
        /// <param name="error">Broken の理由。</param>
        /// <returns>状態。</returns>
        public static VbaMapLoadStatus TryLoad(string workspaceRoot, out VbaMap map, out string error)
        {
            map = null;
            error = null;
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                return VbaMapLoadStatus.Missing;
            }

            string path = GetFilePath(workspaceRoot);
            if (!File.Exists(path))
            {
                return VbaMapLoadStatus.Missing;
            }

            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(path);
                XmlElement root = doc.DocumentElement;
                if (root == null || !string.Equals(root.Name, "vbaMap", StringComparison.Ordinal))
                {
                    error = "vba-map.xml のルートが vbaMap ではありません。";
                    return VbaMapLoadStatus.Broken;
                }

                VbaMap loaded = new VbaMap();
                XmlNode wb = root.SelectSingleNode("workbook");
                if (wb != null && wb.Attributes != null)
                {
                    XmlAttribute p = wb.Attributes["path"];
                    loaded.workbookPath = (p == null || p.Value == null) ? "" : p.Value;
                }

                XmlNode rootNode = root.SelectSingleNode("root");
                if (rootNode != null && rootNode.Attributes != null)
                {
                    XmlAttribute rel = rootNode.Attributes["relative"];
                    if (rel != null && !string.IsNullOrEmpty(rel.Value))
                    {
                        loaded.rootRelative = rel.Value.Replace('\\', '/').Trim('/');
                    }
                }

                if (!IsSafeRootRelative(loaded.rootRelative))
                {
                    error = "vba-map.xml の root がワークスペース内の相対パスではありません。";
                    return VbaMapLoadStatus.Broken;
                }

                XmlNode modeNode = root.SelectSingleNode("namingMode");
                if (modeNode != null)
                {
                    loaded.namingMode = VbaNaming.ParseMode(modeNode.InnerText);
                }

                XmlNode encNode = root.SelectSingleNode("encoding");
                if (encNode != null && encNode.Attributes != null)
                {
                    XmlAttribute cp = encNode.Attributes["codePage"];
                    int n;
                    if (cp != null && int.TryParse(cp.Value, out n) && n > 0)
                    {
                        loaded.encodingCodePage = n;
                    }

                    XmlAttribute bom = encNode.Attributes["hasBom"];
                    if (bom != null)
                    {
                        loaded.encodingHasBom = string.Equals(bom.Value, "true", StringComparison.OrdinalIgnoreCase)
                            || bom.Value == "1";
                    }
                }

                List<VbaMapComponent> list = new List<VbaMapComponent>();
                XmlNode comps = root.SelectSingleNode("components");
                if (comps != null)
                {
                    foreach (XmlNode node in comps.ChildNodes)
                    {
                        if (node == null || node.NodeType != XmlNodeType.Element || !string.Equals(node.Name, "component", StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (node.Attributes == null)
                        {
                            error = "vba-map.xml の component に属性がありません。";
                            return VbaMapLoadStatus.Broken;
                        }

                        XmlAttribute nameAt = node.Attributes["name"];
                        XmlAttribute relAt = node.Attributes["relpath"];
                        XmlAttribute typeAt = node.Attributes["type"];
                        string name = (nameAt == null) ? "" : nameAt.Value;
                        string relPath = (relAt == null) ? "" : relAt.Value;
                        VbaComponentKind kind;
                        if (!TryParseKind(typeAt == null ? "" : typeAt.Value, out kind))
                        {
                            error = "vba-map.xml の type は std / class / document だけです。";
                            return VbaMapLoadStatus.Broken;
                        }

                        if (!IsSafeRelPath(relPath))
                        {
                            error = "vba-map.xml の relpath が不正です。";
                            return VbaMapLoadStatus.Broken;
                        }

                        list.Add(new VbaMapComponent(name, relPath, kind));
                    }
                }

                loaded.components = list.ToArray();
                map = loaded;
                return VbaMapLoadStatus.Ok;
            }
            catch (XmlException ex)
            {
                error = "vba-map.xml が壊れています: " + ex.Message;
                return VbaMapLoadStatus.Broken;
            }
            catch (IOException ex)
            {
                error = "vba-map.xml を読めません: " + ex.Message;
                return VbaMapLoadStatus.Broken;
            }
        }

        /// <summary>
        /// マップを UTF-8 BOM で書く。.windows-ide が無ければ作る。参照 GUID 要素は書かない。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="error">失敗理由。</param>
        /// <returns>書けたら true。</returns>
        public bool TrySave(string workspaceRoot, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(workspaceRoot))
            {
                error = "ワークスペースがありません。";
                return false;
            }

            if (!IsSafeRootRelative(this.rootRelative))
            {
                error = "root がワークスペース内の相対パスではありません。";
                return false;
            }

            try
            {
                string dir = Path.Combine(workspaceRoot, ".windows-ide");
                Directory.CreateDirectory(dir);

                XmlDocument doc = new XmlDocument();
                XmlDeclaration decl = doc.CreateXmlDeclaration("1.0", "utf-8", null);
                doc.AppendChild(decl);
                XmlElement root = doc.CreateElement("vbaMap");
                root.SetAttribute("version", "1");
                doc.AppendChild(root);

                XmlElement wb = doc.CreateElement("workbook");
                wb.SetAttribute("path", (this.workbookPath == null) ? "" : this.workbookPath);
                root.AppendChild(wb);

                XmlElement rootEl = doc.CreateElement("root");
                rootEl.SetAttribute("relative", this.rootRelative);
                root.AppendChild(rootEl);

                XmlElement modeEl = doc.CreateElement("namingMode");
                modeEl.InnerText = VbaNaming.ToXml(this.namingMode);
                root.AppendChild(modeEl);

                XmlElement encEl = doc.CreateElement("encoding");
                encEl.SetAttribute("codePage", this.encodingCodePage.ToString());
                encEl.SetAttribute("hasBom", this.encodingHasBom ? "true" : "false");
                root.AppendChild(encEl);

                XmlElement comps = doc.CreateElement("components");
                for (int i = 0; i < this.components.Length; i++)
                {
                    VbaMapComponent c = this.components[i];
                    XmlElement el = doc.CreateElement("component");
                    el.SetAttribute("name", c.Name);
                    el.SetAttribute("relpath", c.RelPath);
                    el.SetAttribute("type", KindToXml(c.Kind));
                    comps.AppendChild(el);
                }

                root.AppendChild(comps);

                XmlWriterSettings writerSettings = new XmlWriterSettings();
                writerSettings.Indent = true;
                writerSettings.Encoding = new UTF8Encoding(true);
                using (XmlWriter writer = XmlWriter.Create(GetFilePath(workspaceRoot), writerSettings))
                {
                    doc.Save(writer);
                }

                return true;
            }
            catch (IOException ex)
            {
                error = ex.Message;
                return false;
            }
            catch (XmlException ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// ワークスペース内なら相対、外ならフルパスにする。
        /// </summary>
        /// <param name="workspaceRoot">ワークスペースルート。</param>
        /// <param name="workbookFull">ブックのフルパス。</param>
        /// <returns>マップに書く path。</returns>
        public static string StoreWorkbookPath(string workspaceRoot, string workbookFull)
        {
            if (string.IsNullOrEmpty(workbookFull))
            {
                return "";
            }

            string full;
            try
            {
                full = Path.GetFullPath(workbookFull);
            }
            catch (Exception)
            {
                return workbookFull;
            }

            if (!string.IsNullOrEmpty(workspaceRoot) && PathGuard.IsInsideWorkspace(workspaceRoot, full))
            {
                string rootFull = Path.GetFullPath(workspaceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (full.Length <= rootFull.Length)
                {
                    return Path.GetFileName(full);
                }

                return full.Substring(rootFull.Length + 1);
            }

            return full;
        }

        private static bool IsSafeRootRelative(string relative)
        {
            if (string.IsNullOrEmpty(relative))
            {
                return false;
            }

            string n = relative.Replace('\\', '/').Trim('/');
            if (n.Length == 0 || Path.IsPathRooted(relative))
            {
                return false;
            }

            string[] parts = n.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == ".." || parts[i] == ".")
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSafeRelPath(string relPath)
        {
            if (string.IsNullOrEmpty(relPath))
            {
                return true;
            }

            string n = relPath.Replace('\\', '/');
            if (Path.IsPathRooted(relPath))
            {
                return false;
            }

            string[] parts = n.Split('/');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseKind(string text, out VbaComponentKind kind)
        {
            kind = VbaComponentKind.Std;
            if (string.Equals(text, "std", StringComparison.OrdinalIgnoreCase))
            {
                kind = VbaComponentKind.Std;
                return true;
            }

            if (string.Equals(text, "class", StringComparison.OrdinalIgnoreCase))
            {
                kind = VbaComponentKind.Class;
                return true;
            }

            if (string.Equals(text, "document", StringComparison.OrdinalIgnoreCase))
            {
                kind = VbaComponentKind.Document;
                return true;
            }

            return false;
        }

        private static string KindToXml(VbaComponentKind kind)
        {
            if (kind == VbaComponentKind.Class)
            {
                return "class";
            }

            if (kind == VbaComponentKind.Document)
            {
                return "document";
            }

            return "std";
        }
    }
}
