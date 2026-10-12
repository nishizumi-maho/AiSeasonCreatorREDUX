using AiSeasonCreatorREDUX.ScheduleClasses;
using AiSeasonCreatorREDUX.FormOptions;
using AiSeasonCreatorREDUX.Data;
using AiSeasonCreatorREDUX.Views;
using ReaLTaiizor.Extension;

namespace AiSeasonCreatorREDUX.Mappers
{
    public class EventsMapper : IMapper<List<Events>>
    {
        private readonly IMapper<PaceCar> _paceCarMapper;
        private readonly IMapper<Weather> _weather;
        private readonly LoadedData _loadedData;
        private readonly ISeasonView _seasonView;
        public EventsMapper(IMapper<PaceCar> paceCarMapper, IMapper<Weather> weather, LoadedData loadedData, ISeasonView seasonView)
        {
            _paceCarMapper = paceCarMapper;
            _weather = weather;
            _loadedData = loadedData;
            _seasonView = seasonView;
        }
        public List<Events> Map(int eventIndex, string eventGuid)
        {
            var events = new List<Events>();
            var s = _loadedData.SelectedSeries;
            var tracks = _loadedData.TrackDetails;
            var selectedTracks = _seasonView.SelectedTracks;

            for (var i = 0; i < s.Schedules.Count; i++)
            {
                // Match by race week (schedule index): the same track can appear in several weeks.
                if (selectedTracks.Contains(i))
                {
                    var loopEvent = new Events();
                    loopEvent.TrackId = s.Schedules[i].Track.TrackId;
                    loopEvent.NumOptLaps = 0;
                    loopEvent.PaceCar = _paceCarMapper.Map(i, "");
                    loopEvent.ShortParadeLap = _seasonView.ShortParade ? true : false;

                    loopEvent.MustUseDiffTireTypesInRace = s.MustUseDiffTireTypesInRace;

                    SetSubsessions(loopEvent);

                    eventGuid = Guid.NewGuid().ToString();
                    loopEvent.EventId = eventGuid;

                    var (laps, minutes) = RaceLengthFor(s, i);
                    if (laps == null)
                    {
                        loopEvent.RaceLaps = 0;
                        loopEvent.RaceLength = minutes;
                        loopEvent.RaceLengthType = 2;
                    }
                    else
                    {
                        loopEvent.RaceLaps = laps;
                        loopEvent.RaceLength = 0;
                        loopEvent.RaceLengthType = 3;
                    }

                    loopEvent.Weather = _weather.Map(i, eventGuid);
                    loopEvent.StartZone = s.Schedules[i].HasStartZone;
                    loopEvent.FullCourseCautions = s.Schedules[i].HasFullCourseCautions;
                    loopEvent.TimeOfDay = _seasonView.AfternoonRaces ? 0 : s.Schedules[i].Weather.TimeOfDay;

                    if (s.Schedules[i].Track.TrackName.Contains("Combined") || s.Schedules[i].Track.TrackName.Contains("Nordschleife"))
                    {
                        loopEvent.QualifyLength = 30;
                    }
                    else if (s.Schedules[i].Track.Category == "oval")
                    {
                        loopEvent.QualifyLength = 5;
                    }
                    else
                    {
                        loopEvent.QualifyLength = 8;
                    }

                    events.Add(loopEvent);
                }
            }

            if (events.Count <= 0)
            {
                throw new Exception("No tracks selected");
            }

            return events;
        }

        /// <summary>
        /// The race slider is a percentage of the official length when week 1 is a lap race, or minutes
        /// when week 1 is a timed race. Weeks of the other kind are scaled by the same ratio.
        /// </summary>
        private (int? Laps, int Minutes) RaceLengthFor(JsonClasses.FullSchedule.FullSchedule s, int i)
        {
            var first = s.Schedules[0];
            var week = s.Schedules[i];
            var slider = _seasonView.RaceLength;

            double ratio = first.RaceLapLimit != null
                ? slider / 100.0
                : first.RaceTimeLimit is > 0 ? (double)slider / first.RaceTimeLimit.Value : 1.0;

            if (week.RaceLapLimit != null)
                return (Math.Max(1, (int)Math.Round(week.RaceLapLimit.Value * ratio)), 0);

            if (first.RaceLapLimit == null && week.RaceTimeLimit == first.RaceTimeLimit)
                return (null, slider);

            return (null, Math.Clamp((int)Math.Round((week.RaceTimeLimit ?? slider) * ratio), 1, 1440));
        }

        private void SetSubsessions(Events loopEvent)
        {
            if (_seasonView.PracticeLength == 0 && _seasonView.QualiLength == 0)
            {
                loopEvent.Subsessions = new List<int> { 6 };
            }
            else if (_seasonView.PracticeLength == 0)
            {
                if (_seasonView.QualiAlone)
                {
                    loopEvent.Subsessions = new List<int> { 4, 6 };
                }
                else
                {
                    loopEvent.Subsessions = new List<int> { 5, 6 };
                }
            }
            else if (_seasonView.QualiLength == 0)
            {
                loopEvent.Subsessions = new List<int> { 3, 6 };
            }
            else if (_seasonView.QualiAlone)
            {
                loopEvent.Subsessions = new List<int> { 3, 4, 6 };
            }
            else
            {
                loopEvent.Subsessions = new List<int> { 3, 5, 6 };
            }
        }
    }
}
