using System;
using System.IO;
using System.Text;

namespace GameLog
{
    /// <summary>极简日志：写入 %APPDATA%\GameLog\logs\app.log，超过 2MB 自动轮转。</summary>
    public static class Log
    {
        static readonly object Gate = new object();
        public static string Dir = "";

        public static void Init(string dir)
        {
            Dir = dir;
            try { Directory.CreateDirectory(dir); }
            catch { }
        }

        public static string FilePath
        {
            get { return string.IsNullOrEmpty(Dir) ? "" : Path.Combine(Dir, "app.log"); }
        }

        public static void Info(string msg) { Write("INFO ", msg); }
        public static void Warn(string msg) { Write("WARN ", msg); }
        public static void Error(string msg) { Write("ERROR", msg); }

        public static void Error(string msg, Exception ex)
        {
            if (ex == null) { Error(msg); return; }
            Error(msg + " :: " + ex.GetType().Name + ": " + ex.Message);
        }

        static void Write(string level, string msg)
        {
            if (string.IsNullOrEmpty(Dir)) return;
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(Dir);
                    string f = FilePath;
                    var fi = new FileInfo(f);
                    if (fi.Exists && fi.Length > 2 * 1024 * 1024)
                    {
                        try
                        {
                            string old = f + ".1";
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(f, old);
                        }
                        catch { }
                    }
                    File.AppendAllText(f, string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] {2}\r\n", DateTime.Now, level, msg), Encoding.UTF8);
                }
                catch { }
            }
        }
    }
}
