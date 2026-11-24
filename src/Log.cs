using System;
using System.IO;
using System.Text;

namespace NonActivatingOperate
{
    internal static class Log
    {
        private static readonly object Sync = new object();
        private static string _path;
        private static bool _failed;

        public static string DataDirectory
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Non Activating Operate");
                try
                {
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                }
                catch { }
                return dir;
            }
        }

        public static string LogPath
        {
            get
            {
                if (_path == null) _path = Path.Combine(DataDirectory, "log.txt");
                return _path;
            }
        }

        public static void Write(string message)
        {
            if (_failed) return;
            lock (Sync)
            {
                try
                {
                    string path = LogPath;
                    FileInfo fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 512 * 1024)
                    {
                        try { File.Delete(path); }
                        catch { }
                    }
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
                catch
                {
                    _failed = true;
                }
            }
        }

        public static void Error(string context, Exception ex)
        {
            Write(context + " FAILED: " + ex.GetType().Name + ": " + ex.Message);
        }
    }
}
