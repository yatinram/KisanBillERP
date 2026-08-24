using System;
using System.IO;

namespace KrushiBillERP.Data
{
    public static class Logger
    {
        private static readonly string LogFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFile, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
            }
            catch { }
        }

        public static void Log(Exception ex)
        {
            try
            {
                var txt = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Exception: {ex.Message}\nStack: {ex.StackTrace}\n";
                if (ex.InnerException != null) txt += $"Inner: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}\n";
                File.AppendAllText(LogFile, txt);
            }
            catch { }
        }
    }
}
