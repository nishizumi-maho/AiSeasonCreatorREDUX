using AiSeasonCreator.JsonClasses.CarClasses;
using AiSeasonCreator.JsonClasses.CarDetails;
using AiSeasonCreator.JsonClasses.FullSchedule;
using AiSeasonCreator.JsonClasses.SeriesDetails;
using AiSeasonCreator.JsonClasses.TrackDetails;
using AiSeasonCreator.JsonClasses.Assets;
using AiSeasonCreator.Roster;
using AiSeasonCreator.Helpers;
using AiSeasonCreator.DefaultUserSettings;

namespace AiSeasonCreator.Data
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
