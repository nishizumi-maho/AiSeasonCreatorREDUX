using AiSeasonCreatorREDUX.Data;
using AiSeasonCreatorREDUX.Services;
using AiSeasonCreatorREDUX.Views;

namespace AiSeasonCreatorREDUX.Presenters
{
    public class SeasonPresenter : ISeasonPresenter
    {
        private readonly ISeasonView _view;
        private readonly ISeasonService _seasonService;
        private readonly LoadedData _loadedData;
        private bool _viewLoaded;

        public SeasonPresenter(ISeasonView view, ISeasonService seasonService, LoadedData loadedData, SeasonDataManager dataManager)
        {
            _view = view;
            _seasonService = seasonService;
            _loadedData = loadedData;

            _view.ViewLoaded += OnViewLoaded;
            _view.SeriesIndexChanged += OnSeriesIndexChanged;
            _view.RosterClicked += OnRosterClicked;
            _view.QualiAloneChecked += OnQualiAloneChecked;
            _view.CreateSeasonClicked += OnCreateSeasonClicked;

            // New season data (e.g. downloaded in the background) -> rebuild the lists.
            dataManager.DataChanged += (_, _) =>
            {
                if (_viewLoaded)
                    PopulateSeries();
            };
        }

        public void OnViewLoaded(object sender, EventArgs e)
        {
            _viewLoaded = true;
            PopulateSeries();
            _view.RosterList = _seasonService.GetAvailableRosters();
            SetUserDefaultSettings();
        }

        public void OnSeriesIndexChanged(object sender, EventArgs e)
        {
            _seasonService.SetSelectedSeason(_view.SelectedSeasonId);
            _view.CarList = _seasonService.GetAvailableCars();
            _view.TrackList = _seasonService.GetAvailableTracks();
            _view.CarCount = _seasonService.GetDriverCount();
            _view.QualiLength = _seasonService.GetQualiLength(_view.QualiAlone);
            _view.RaceIsLapPercentage = _seasonService.IsLapLimitedRace();
            _view.RaceLength = _seasonService.GetRaceLength();
        }

        public void OnRosterClicked(object sender, EventArgs e)
        {
            _view.RosterList = _seasonService.GetAvailableRosters();
        }

        public void OnQualiAloneChecked(object sender, EventArgs e)
        {
            _view.QualiLength = _seasonService.GetQualiLength(_view.QualiAlone);
        }

        public async void OnCreateSeasonClicked(object sender, EventArgs e)
        {
            _view.IsBusy = true;
            try
            {
                var result = await _seasonService.CreateSeasonAsync(_view.SeasonName, _view.SelectedTracks);
                _view.ShowMessage(result.Message, !result.Success);

                if (result.Success)
                    _view.RosterList = _seasonService.GetAvailableRosters();
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Could not create the season: {ex.Message}", true);
            }
            finally
            {
                _view.IsBusy = false;
            }
        }

        private void PopulateSeries()
        {
            _view.CarList = Enumerable.Empty<Helpers.NameAndAsset>();
            _view.TrackList = Enumerable.Empty<Helpers.NameAndAsset>();
            _view.SeriesList = _seasonService.GetAvailableSeries();
        }

        private void SetUserDefaultSettings()
        {
            var ud = _loadedData.UserDefaultSettings;

            _view.AiMin = ud.TrackBarUserValues.AiMinSkill;
            _view.AiMax = ud.TrackBarUserValues.AiMaxSkill;

            _view.UseAdaptiveAi = ud.CheckBoxesUserValues.UseAdaptiveAi;
            _view.DisableDamage = ud.CheckBoxesUserValues.DisableCarDamage;
            _view.AiAvoids = ud.CheckBoxesUserValues.AiAvoidsPlayer;
            _view.StaticWeather = ud.CheckBoxesUserValues.StaticWeather;
            _view.AfternoonRaces = ud.CheckBoxesUserValues.AfternoonRaces;
            _view.NeverRain = ud.CheckBoxesUserValues.NeverRain;
            _view.QualiAlone = ud.CheckBoxesUserValues.QualifyAlone;
            _view.ShortParade = ud.CheckBoxesUserValues.ShortParade;
            _view.StartZones = ud.CheckBoxesUserValues.StartZones;

            _view.AdaptiveAiDifficulty = ud.ComboBoxesUserValues.AdaptiveAiSkillLevel;
        }
    }
}
