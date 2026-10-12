using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSeasonCreator.Services
{
    public interface ISettingsService
    {
        void LoadUserDefaultSettings();
        string GetSeasonsFolderPathAuto(string name);
        string GetRostersFolderPathAuto(string name);

        /// <summary>Opens the folder picker. Returns null when the user cancels.</summary>
        string? GetSeasonsFolderPath(string name);
        /// <summary>Opens the folder picker. Returns null when the user cancels.</summary>
        string? GetRostersFolderPath(string name);
    }
}
