using AiSeasonCreator.FormOptions;
using AiSeasonCreator.Services;
using AiSeasonCreator.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AiSeasonCreator.Presenters
{
    class SettingsPresenter : ISettingsPresenter
    {
        private ISettingsView _view;
        private ISettingsService _settingsService;
        private AppSettings _appSettings;
        public SettingsPresenter(ISettingsView view, ISettingsService settingsService, AppSettings appSettings)
        {
            _view = view;
            _settingsService = settingsService;
            _appSettings = appSettings;

            _view.ViewLoaded += OnViewLoaded;
            _view.SeasonFolderClicked += OnSeasonFolderClicked;
            _view.RosterFolderClicked += OnRosterFolderClicked;
        }

        public void OnViewLoaded(object sender, EventArgs e)
        {
            // User settings are loaded in Program.Main, before any view is created.

            _view.SeasonFolderPath = _settingsService.GetSeasonsFolderPathAuto("aiseasons");
            _appSettings.SeasonFolderPath = _view.SeasonFolderPath;

            _view.RosterFolderPath = _settingsService.GetRostersFolderPathAuto("airosters");
            _appSettings.RosterFolderPath = _view.RosterFolderPath;
        }

        public void OnSeasonFolderClicked(object sender, EventArgs e)
        {
            // Keep the current path when the dialog is cancelled.
            var picked = _settingsService.GetSeasonsFolderPath("aiseasons");
            if (string.IsNullOrEmpty(picked))
                return;

            _view.SeasonFolderPath = picked;
            _appSettings.SeasonFolderPath = picked;
        }

        public void OnRosterFolderClicked(object sender, EventArgs e)
        {
            // Keep the current path when the dialog is cancelled.
            var picked = _settingsService.GetRostersFolderPath("airosters");
            if (string.IsNullOrEmpty(picked))
                return;

            _view.RosterFolderPath = picked;
            _appSettings.RosterFolderPath = picked;
        }
    }
}
