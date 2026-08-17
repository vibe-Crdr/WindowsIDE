using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using WindowsIDE.Ui;
using WindowsIDE.Ui.Fonts;

namespace WindowsIDE
{
    /// <summary>
    /// 単一 WinExe の入口。STA。P0 では Excel を起動しない。
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// 未処理例外をログと MessageBox に出してから UI を起動する。
        /// </summary>
        /// <param name="args">起動引数。実行ファイルパスは含まない。</param>
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            try
            {
                FontLoadResult fonts = FontLoader.Load();
                Application.Run(new MainForm(fonts, args));
            }
            catch (Exception ex)
            {
                HandleException(ex);
            }
        }

        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            HandleException(e.Exception);
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Exception ex = e.ExceptionObject as Exception;
            if (ex == null)
            {
                ex = new Exception(e.ExceptionObject == null ? "unknown" : e.ExceptionObject.ToString());
            }

            HandleException(ex);
        }

        /// <summary>
        /// %TEMP%\WindowsIDE.log へ追記し、MessageBox を出す。
        /// </summary>
        /// <param name="ex">例外。</param>
        public static void HandleException(Exception ex)
        {
            if (ex == null)
            {
                return;
            }

            try
            {
                string path = Path.Combine(Path.GetTempPath(), "WindowsIDE.log");
                string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + ex.ToString() + Environment.NewLine;
                File.AppendAllText(path, line);
            }
            catch (IOException)
            {
            }

            try
            {
                MessageBox.Show(ex.Message, "WindowsIDE", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (InvalidOperationException)
            {
            }
        }
    }
}
