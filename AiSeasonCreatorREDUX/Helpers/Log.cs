using AiSeasonCreatorREDUX.Data;

namespace AiSeasonCreatorREDUX.Helpers
{
    /// <summary>
    /// Tiny file logger so users can send us something useful when a refresh fails.
    /// </summary>
    public static class Log
    {
        private static readonly object _lock = new();
        private const long MaxBytes = 1024 * 1024;

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception? ex = null) =>
            Write("ERROR", ex == null ? message : $"{message}: {ex}");

        private static void Write(string level, string message)
        {
            try
            {
                lock (_lock)
                {
                    var file = AppPaths.LogFile;
                    if (File.Exists(file) && new FileInfo(file).Length > MaxBytes)
                        File.Delete(file);

                    File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Logging must never break the app.
            }
        }
    }
}
