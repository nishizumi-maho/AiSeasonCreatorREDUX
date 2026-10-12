using AiSeasonCreatorREDUX.Data;
using AiSeasonCreatorREDUX.DefaultUserSettings;
using AiSeasonCreatorREDUX.Helpers;
using AiSeasonCreatorREDUX.Repos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSeasonCreatorREDUX.Services
{
    public class SettingsService : ISettingsService
    {
        private LoadedData _loadedData;
        private IJsonRepo _jsonRepo;

        public SettingsService(LoadedData loadedData, IJsonRepo jsonRepo)
        {
            _loadedData = loadedData;
            _jsonRepo = jsonRepo;
        }
        public string GetSeasonsFolderPathAuto(string name)
        {
            var ud = _loadedData.UserDefaultSettings;
            if (ud != null && IracingFolders.IsExpectedFolder(ud.FolderLocations.SeasonsFolder, name))
            {
                return ud.FolderLocations.SeasonsFolder;
            }

            // iRacing only reads AI seasons from Documents\iRacing\aiseasons, so a saved path
            // pointing anywhere else (e.g. picked by accident) would silently "lose" seasons.
            var defaultPath = EnsureDefaultFolder(name);
            if (ud != null) { ud.FolderLocations.SeasonsFolder = defaultPath; }
            return defaultPath;
        }

        public string GetRostersFolderPathAuto(string name)
        {
            var ud = _loadedData.UserDefaultSettings;
            if (ud != null && IracingFolders.IsExpectedFolder(ud.FolderLocations.RostersFolder, name))
            {
                return ud.FolderLocations.RostersFolder;
            }

            var defaultPath = EnsureDefaultFolder(name);
            if (ud != null) { ud.FolderLocations.RostersFolder = defaultPath; }
            return defaultPath;
        }

        private static string EnsureDefaultFolder(string name)
        {
            var defaultPath = IracingFolders.DefaultFolder(name);

            try
            {
                // iRacing creates these on first AI use; creating them early avoids an
                // empty-path state on fresh installs.
                Directory.CreateDirectory(defaultPath);
            }
            catch (Exception ex)
            {
                Log.Error($"Could not create default folder {defaultPath}", ex);
            }

            return defaultPath;
        }

        public string? GetSeasonsFolderPath(string name)
        {
            var current = _loadedData.UserDefaultSettings?.FolderLocations.SeasonsFolder;
            var picked = PickFolder(
                $"Select your iRacing seasons folder (expected: ...\\iRacing\\{name})",
                current);
            if (picked == null)
                return null;

            if (_loadedData.UserDefaultSettings != null)
                _loadedData.UserDefaultSettings.FolderLocations.SeasonsFolder = picked;
            return picked;
        }

        public string? GetRostersFolderPath(string name)
        {
            var current = _loadedData.UserDefaultSettings?.FolderLocations.RostersFolder;
            var picked = PickFolder(
                $"Select your iRacing rosters folder (expected: ...\\iRacing\\{name})",
                current);
            if (picked == null)
                return null;

            if (_loadedData.UserDefaultSettings != null)
                _loadedData.UserDefaultSettings.FolderLocations.RostersFolder = picked;
            return picked;
        }

        /// <summary>Shows the folder picker starting at the current folder. Null when cancelled.</summary>
        private static string? PickFolder(string description, string? currentPath)
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
            {
                folderBrowserDialog.Description = description;
                folderBrowserDialog.UseDescriptionForTitle = true;
                folderBrowserDialog.ShowNewFolderButton = true;
                if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
                    folderBrowserDialog.SelectedPath = currentPath;

                return folderBrowserDialog.ShowDialog() == DialogResult.OK
                    ? folderBrowserDialog.SelectedPath
                    : null;
            }
        }

        public void LoadUserDefaultSettings()
        {
            string userDefaultSettingsPath = AppPaths.UserSettingsFile;

            if (File.Exists(userDefaultSettingsPath))
            {
                try
                {
                    _loadedData.UserDefaultSettings = _jsonRepo.Load<UserDefaultSettings>(userDefaultSettingsPath) ?? new UserDefaultSettings();
                }
                catch (Exception)
                {
                    // A corrupt settings file shouldn't stop the app from starting.
                    _loadedData.UserDefaultSettings = new UserDefaultSettings();
                }
            }
        }
    }
}
