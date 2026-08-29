using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace WindowsIDE.Languages.CSharp
{
    /// <summary>
    /// Framework DLL 隣の XML から型の summary を読む。Regex は使わない。例外は外に出さない。
    /// </summary>
    public static class BclXmlDocs
    {
        private static readonly string FrameworkDir = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319";

        private static readonly string[] XmlFiles = new string[]
        {
            "mscorlib.xml",
            "System.xml",
            "System.Core.xml",
            "System.Drawing.xml",
            "System.Windows.Forms.xml",
            "System.Xml.xml"
        };

        private static Dictionary<string, string> summaries;
        private static readonly object Gate = new object();

        /// <summary>
        /// T: 付きでない修飾名の summary プレーンテキスト。無ければ false。
        /// </summary>
        /// <param name="qualifiedName">例: System.String。</param>
        /// <param name="text">本文。</param>
        /// <returns>取れたら true。</returns>
        public static bool TryGetTypeSummary(string qualifiedName, out string text)
        {
            return TryGetTypeSummaryFromDirectory(FrameworkDir, qualifiedName, out text);
        }

        /// <summary>
        /// 指定フォルダの XML から読む。欠如・壊れは false。throw しない。
        /// </summary>
        /// <param name="directory">XML のあるフォルダ。</param>
        /// <param name="qualifiedName">修飾名。</param>
        /// <param name="text">本文。</param>
        /// <returns>取れたら true。</returns>
        public static bool TryGetTypeSummaryFromDirectory(string directory, string qualifiedName, out string text)
        {
            text = null;
            if (string.IsNullOrEmpty(qualifiedName))
            {
                return false;
            }

            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return false;
            }

            try
            {
                if (string.Equals(directory, FrameworkDir, StringComparison.OrdinalIgnoreCase))
                {
                    EnsureLoaded();
                    return Lookup(summaries, qualifiedName, out text);
                }

                Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
                LoadDirectory(directory, map);
                return Lookup(map, qualifiedName, out text);
            }
            catch (Exception)
            {
                text = null;
                return false;
            }
        }

        private static bool Lookup(Dictionary<string, string> map, string qualifiedName, out string text)
        {
            text = null;
            if (map == null)
            {
                return false;
            }

            string key = "T:" + qualifiedName;
            if (map.TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
            {
                return true;
            }

            return false;
        }

        private static void EnsureLoaded()
        {
            if (summaries != null)
            {
                return;
            }

            lock (Gate)
            {
                if (summaries != null)
                {
                    return;
                }

                Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.Ordinal);
                LoadDirectory(FrameworkDir, map);
                summaries = map;
            }
        }

        private static void LoadDirectory(string directory, Dictionary<string, string> map)
        {
            for (int i = 0; i < XmlFiles.Length; i++)
            {
                string path = Path.Combine(directory, XmlFiles[i]);
                LoadFile(path, map);
            }
        }

        /// <summary>
        /// 1 ファイルを読む。無い・壊れていても throw しない。
        /// </summary>
        /// <param name="path">XML パス。</param>
        /// <param name="map">出力。</param>
        public static void LoadFile(string path, Dictionary<string, string> map)
        {
            if (map == null || string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.XmlResolver = null;
                doc.Load(path);
            }
            catch (Exception)
            {
                return;
            }

            XmlNodeList members;
            try
            {
                members = doc.GetElementsByTagName("member");
            }
            catch (Exception)
            {
                return;
            }

            if (members == null)
            {
                return;
            }

            for (int i = 0; i < members.Count; i++)
            {
                XmlElement element = members[i] as XmlElement;
                if (element == null)
                {
                    continue;
                }

                string memberName = element.GetAttribute("name");
                if (string.IsNullOrEmpty(memberName) || memberName.Length < 2 || memberName[0] != 'T' || memberName[1] != ':')
                {
                    continue;
                }

                string summary = ReadSummary(element);
                if (string.IsNullOrEmpty(summary))
                {
                    continue;
                }

                if (!map.ContainsKey(memberName))
                {
                    map[memberName] = summary;
                }

                int tick = memberName.IndexOf('`');
                if (tick > 0)
                {
                    string truncated = memberName.Substring(0, tick);
                    if (!map.ContainsKey(truncated))
                    {
                        map[truncated] = summary;
                    }
                }
            }
        }

        private static string ReadSummary(XmlElement member)
        {
            try
            {
                XmlNodeList nodes = member.GetElementsByTagName("summary");
                if (nodes == null || nodes.Count == 0)
                {
                    return null;
                }

                string raw = nodes[0].InnerText;
                return Normalize(raw);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            string text = raw.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
            if (text.Length == 0)
            {
                return null;
            }

            return text;
        }
    }
}
