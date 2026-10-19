using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;

namespace GameLog
{
    /// <summary>
    /// 单实例通信：右键菜单再次启动程序时，把 exe 路径交给已运行的实例。
    /// 优先使用命名管道，失败时退回“待处理文件”（某些安全软件会拦截管道）。
    /// </summary>
    public static class Ipc
    {
        const string PipeName = "GameLog.Pipe.v1";
        const string PendingName = "pending.txt";

        public static string PendingFile(string dataDir)
        {
            return Path.Combine(dataDir, PendingName);
        }

        public static bool TrySend(string message)
        {
            try
            {
                using (var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out, PipeOptions.None))
                {
                    client.Connect(1200);
                    using (var writer = new StreamWriter(client, new UTF8Encoding(false)))
                    {
                        writer.WriteLine(message);
                        writer.Flush();
                    }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>管道不可用时写进待处理文件，由已运行的实例轮询读取。</summary>
        public static bool WritePending(string dataDir, string message)
        {
            try
            {
                Directory.CreateDirectory(dataDir);
                File.AppendAllText(PendingFile(dataDir), message.Replace("\r", " ").Replace("\n", " ") + "\r\n", new UTF8Encoding(false));
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void StartServer(Action<string> onMessage)
        {
            var thread = new Thread(delegate ()
            {
                while (true)
                {
                    try
                    {
                        using (var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None))
                        {
                            server.WaitForConnection();
                            using (var reader = new StreamReader(server, new UTF8Encoding(false)))
                            {
                                string line = reader.ReadLine();
                                if (!string.IsNullOrEmpty(line) && onMessage != null) onMessage(line);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Warn("IPC 管道不可用，改用待处理文件：" + ex.Message);
                        return;
                    }
                }
            });
            thread.IsBackground = true;
            thread.Name = "GameLog-IPC";
            thread.Start();
        }
    }
}
