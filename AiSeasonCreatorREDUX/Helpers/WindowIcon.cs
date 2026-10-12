namespace AiSeasonCreatorREDUX.Helpers
{
    /// <summary>Applies the application icon (embedded in the exe) to top-level windows.</summary>
    public static class WindowIcon
    {
        public static void Apply(Form form)
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon != null)
                    form.Icon = icon;
            }
            catch
            {
                // Cosmetic only; never break a window over its icon.
            }
        }
    }
}
