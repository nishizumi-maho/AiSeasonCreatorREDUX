using AiSeasonCreatorREDUX.Helpers;

namespace AiSeasonCreatorREDUX.Views
{
    public interface ISeasonView
    {
        event EventHandler ViewLoaded;
        event EventHandler SeriesIndexChanged;
        event EventHandler RosterClicked;
        event EventHandler QualiAloneChecked;
        event EventHandler CreateSeasonClicked;

        IEnumerable<NameAndAsset> SeriesList { set; }
        IEnumerable<NameAndAsset> CarList { set; }
        IEnumerable<NameAndAsset> TrackList { set; }
        /// <summary>Schedule indexes (race weeks) the user kept selected.</summary>
        IReadOnlyCollection<int> SelectedTracks { get; }
        IEnumerable<string> RosterList { set; }

        int SelectedSeasonId { get; }
        string SeasonName { get; }
        int SelectedCarId { get; }
        bool UseAdaptiveAi { get; set; }
        string AdaptiveAiDifficulty { get; set; }
        int AiMin { get; set; }
        int AiMax { get; set; }
        bool DisableDamage { get; set; }
        bool AiAvoids { get; set; }
        bool StaticWeather { get; set; }
        bool AfternoonRaces { get; set; }
        bool NeverRain { get; set; }
        bool QualiAlone { get; set; }
        bool ShortParade { get; set; }
        string RosterName { get; set; }
        int CarCount { get; set; }
        int PracticeLength { get; set; }
        int QualiLength { get; set; }
        int RaceLength { get; set; }
        bool RaceIsLapPercentage { set; }
        bool IsBusy { set; }

        void ShowMessage(string message, bool isError);
    }
}
