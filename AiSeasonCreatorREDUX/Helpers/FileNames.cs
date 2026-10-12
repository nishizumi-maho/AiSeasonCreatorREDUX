namespace AiSeasonCreatorREDUX.Helpers
{
    public static class FileNames
    {
        /// <summary>Replaces characters Windows doesn't allow in file/folder names.</summary>
        public static string Sanitize(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
            return string.IsNullOrWhiteSpace(cleaned) ? "AI Season" : cleaned;
        }
    }
}
