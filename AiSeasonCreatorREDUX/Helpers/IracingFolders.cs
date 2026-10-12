namespace AiSeasonCreatorREDUX.Helpers
{
    /// <summary>
    /// iRacing only reads AI seasons from Documents\iRacing\aiseasons and rosters from
    /// Documents\iRacing\airosters. Anything saved elsewhere never shows up in the sim.
    /// </summary>
    public static class IracingFolders
    {
        /// <summary>True when the path exists and is actually the expected iRacing folder.</summary>
        public static bool IsExpectedFolder(string? path, string expectedFolderName)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
                return false;

            return string.Equals(
                Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)),
                expectedFolderName,
                StringComparison.OrdinalIgnoreCase);
        }

        public static string DefaultFolder(string folderName) =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "iRacing", folderName);
    }
}
