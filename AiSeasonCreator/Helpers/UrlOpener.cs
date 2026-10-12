using System.Diagnostics;

namespace AiSeasonCreator.Helpers
{
    public static class UrlOpener
    {
        public static void Open(string urlOrPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = urlOrPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open: " + urlOrPath + "\n\n" + ex.Message, "AiSeasonCreator", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
