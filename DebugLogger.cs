using System;
using System.IO;

namespace NVIDIA_Profil_Toogler
{
    public static class DebugLogger
    {
        private static readonly string logPath = Path.Combine(AppContext.BaseDirectory, "debug_log.txt");
        private static readonly object lockObj = new object();

        public static void Log(string message)
        {
            try
            {
                lock (lockObj)
                {
                    string logMessage = string.Format("{0:yyyy-MM-dd HH:mm:ss.fff} - {1}\n", DateTime.Now, message);
                    File.AppendAllText(logPath, logMessage);
                }
            }
            catch { /* Ignore logging errors to prevent recursive issues */ }
        }
    }
}
