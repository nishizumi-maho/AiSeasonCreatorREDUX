using AiSeasonCreatorREDUX.Helpers;

namespace AiSeasonCreatorREDUX.Services
{
    public record SeasonCreationResult(bool Success, string Message, string? FilePath = null);

    public interface ISeasonService
    {
        void SetSelectedSeason(int seasonId);
        IEnumerable<NameAndAsset> GetAvailableSeries();
        IEnumerable<NameAndAsset> GetAvailableCars();
        IEnumerable<NameAndAsset> GetAvailableTracks();
        int GetDriverCount();
        int GetPracticeLength();
        int GetQualiLength(bool qualiAlone);
        int GetRaceLength();
        bool IsLapLimitedRace();
        IEnumerable<string> GetAvailableRosters();
        Task<SeasonCreationResult> CreateSeasonAsync(string seasonName, IReadOnlyCollection<int> scheduleIndexes);
    }
}
