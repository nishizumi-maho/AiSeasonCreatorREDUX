namespace AiSeasonCreator.Data
{
    /// <summary>
    /// Central place for every folder the app reads from or writes to.
    /// </summary>
    public static class AppPaths
    {
        public const string AppFolderName = "AiSeasonCreator";

        /// <summary>Folder that contains the executable (bundled JSON + logos live here).</summary>
        public static string InstallFolder => AppContext.BaseDirectory;

        public static string BundledJsonFolder => Path.Combine(InstallFolder, "JsonFiles");
        public static string BundledAssetsFolder => Path.Combine(InstallFolder, "Assets");

        /// <summary>Roaming folder for small user settings (kept for backwards compatibility).</summary>
        public static string SettingsFolder => EnsureExists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppFolderName));

        public static string UserSettingsFile => Path.Combine(SettingsFolder, "UserDefaultSettings.json");

        /// <summary>Local (non-roaming) folder for downloaded data, logos, weather and the browser profile.</summary>
        public static string LocalFolder => EnsureExists(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppFolderName));

        public static string DataCacheFolder => EnsureExists(Path.Combine(LocalFolder, "SeasonData"));
        public static string WeatherCacheFolder => EnsureExists(Path.Combine(LocalFolder, "Weather"));
        public static string ImageCacheFolder => EnsureExists(Path.Combine(LocalFolder, "Images"));
        public static string WebViewProfileFolder => EnsureExists(Path.Combine(LocalFolder, "WebView2"));
        public static string LogFile => Path.Combine(LocalFolder, "AiSeasonCreator.log");

        private static string EnsureExists(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
