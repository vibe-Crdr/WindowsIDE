using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace WindowsIDE.Languages.CSharp
{
    /// <summary>
    /// 製品が参照する Framework DLL の公開型単純名。Roslyn は使わない。
    /// </summary>
    public static class BclTypeCache
    {
        private static readonly string FrameworkDir = @"C:\Windows\Microsoft.NET\Framework64\v4.0.30319";

        private static readonly string[] AssemblyFiles = new string[]
        {
            "mscorlib.dll",
            "System.dll",
            "System.Core.dll",
            "System.Drawing.dll",
            "System.Windows.Forms.dll",
            "System.Xml.dll"
        };

        private static HashSet<string> names;
        private static readonly object Gate = new object();

        /// <summary>
        /// 単純名が公開型なら true。初回だけ LoadFrom する。
        /// </summary>
        /// <param name="simpleName">型の単純名。null は false。</param>
        /// <returns>載っていれば true。</returns>
        public static bool Contains(string simpleName)
        {
            if (string.IsNullOrEmpty(simpleName))
            {
                return false;
            }

            EnsureLoaded();
            return names.Contains(simpleName);
        }

        /// <summary>
        /// キャッシュ済みの単純名集合。テスト用。
        /// </summary>
        /// <returns>読み取り用集合。</returns>
        public static HashSet<string> Names()
        {
            EnsureLoaded();
            return names;
        }

        private static void EnsureLoaded()
        {
            if (names != null)
            {
                return;
            }

            lock (Gate)
            {
                if (names != null)
                {
                    return;
                }

                HashSet<string> set = new HashSet<string>(StringComparer.Ordinal);
                for (int i = 0; i < AssemblyFiles.Length; i++)
                {
                    string path = Path.Combine(FrameworkDir, AssemblyFiles[i]);
                    LoadAssemblyTypes(path, set);
                }

                names = set;
            }
        }

        private static void LoadAssemblyTypes(string path, HashSet<string> set)
        {
            if (!File.Exists(path))
            {
                return;
            }

            Assembly assembly;
            try
            {
                assembly = Assembly.LoadFrom(path);
            }
            catch (Exception)
            {
                return;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null)
            {
                return;
            }

            for (int i = 0; i < types.Length; i++)
            {
                Type type = types[i];
                if (type == null)
                {
                    continue;
                }

                if (!type.IsPublic && !type.IsNestedPublic)
                {
                    continue;
                }

                string name = type.Name;
                if (string.IsNullOrEmpty(name))
                {
                    continue;
                }

                int tick = name.IndexOf('`');
                if (tick > 0)
                {
                    name = name.Substring(0, tick);
                }

                set.Add(name);
            }
        }
    }
}
