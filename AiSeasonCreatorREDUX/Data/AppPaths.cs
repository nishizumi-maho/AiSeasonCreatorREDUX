namespace AiSeasonCreatorREDUX.Data
{
    /// <summary>
    /// Central place for every folder the app reads from or writes to.
    /// </summary>
    public static class AppPaths
    {
        public const string AppFolderName = "AiSeasonCreatorREDUX";

        /// <summary>Folder name used by releases before the rename; migrated once on first start.</summary>
        public const string LegacyAppFolderName = "AiSeasonCreator";

        /// <summary>
        /// Copies settings and downloaded caches from the pre-rename folders so existing users
        /// keep their configuration and don't re-download everything. Old folders are left in place.
        /// </summary>
        public static void MigrateFromLegacy()
        {
            try
            {
                var legacySettings = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), LegacyAppFolderName);
                var legacySettingsFile = Path.Combine(legacySettings, "UserDefaultSettings.json");
                if (!File.Exists(UserSettingsFile) && File.Exists(legacySettingsFile))
                {
                    File.Copy(legacySettingsFile, UserSettingsFile);
                    Helpers.Log.Info($"Migrated settings from {legacySettingsFile}");
                }

                var legacyLocal = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), LegacyAppFolderName);
                foreach (var sub in new[] { "SeasonData", "Weather", "Images" })
                {
                    var source = Path.Combine(legacyLocal, sub);
                    var target = Path.Combine(LocalFolder, sub);
                    if (Directory.Exists(source) && !Directory.Exists(target))
                    {
                        CopyDirectory(source, target);
                        Helpers.Log.Info($"Migrated cache folder {source}");
                    }
                }
            }
            catch (Exception ex)
            {
                Helpers.Log.Warn($"Legacy data migration failed: {ex.Message}");
            }
        }

        private static void CopyDirectory(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (var file in Directory.GetFiles(source))
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)), overwrite: true);
            foreach (var dir in Directory.GetDirectories(source))
                CopyDirectory(dir, Path.Combine(target, Path.GetFileName(dir)));
        }

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
        public static string LogFile => Path.Combine(LocalFolder, "AiSeasonCreatorREDUX.log");

        private static string EnsureExists(string path)
        {
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
