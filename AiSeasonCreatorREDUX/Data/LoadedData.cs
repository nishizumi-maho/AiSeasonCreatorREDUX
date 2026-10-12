using AiSeasonCreatorREDUX.JsonClasses.CarClasses;
using AiSeasonCreatorREDUX.JsonClasses.CarDetails;
using AiSeasonCreatorREDUX.JsonClasses.FullSchedule;
using AiSeasonCreatorREDUX.JsonClasses.SeriesDetails;
using AiSeasonCreatorREDUX.JsonClasses.TrackDetails;
using AiSeasonCreatorREDUX.JsonClasses.Assets;
using AiSeasonCreatorREDUX.Roster;
using AiSeasonCreatorREDUX.Helpers;
using AiSeasonCreatorREDUX.DefaultUserSettings;

namespace AiSeasonCreatorREDUX.Data
{
    public class LoadedData
    {
        public FullSchedule[] FullSchedule { get; set; } = Array.Empty<FullSchedule>();
        public SeriesDetails[] SeriesDetails { get; set; } = Array.Empty<SeriesDetails>();
        public CarClasses[] CarClasses { get; set; } = Array.Empty<CarClasses>();
        public CarDetails[] CarDetails { get; set; } = Array.Empty<CarDetails>();
        public TrackDetails[] TrackDetails { get; set; } = Array.Empty<TrackDetails>();
        public DataOrigin DataOrigin { get; set; } = DataOrigin.Bundled;
        public FullSchedule? SelectedSeries { get; set; }
        public SeriesDetails? SelectedSeriesDetails { get; set; }
        public FullSchedule? RosterSelectedSeries { get; set; }
        public SeriesDetails? RosterSelectedSeriesDetails { get; set; }
        public DriverRoster SelectedRoster { get; set; }
        public Dictionary<string, CarAssets> CarAssets { get; set; } = new();
        public Dictionary<string, SeriesAssets> SeriesAssets { get; set; } = new();
        public Dictionary<string, TrackAssets> TrackAssets { get; set; } = new();
        public SortableBindingList<DriversDataGrid> DriversDataGrid { get; set; } = [];
        public UserDefaultSettings UserDefaultSettings { get; set; } = new UserDefaultSettings();
    }
}
