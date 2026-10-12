using AiSeasonCreatorREDUX.Helpers;
using Newtonsoft.Json.Linq;

namespace AiSeasonCreatorREDUX
{
    public partial class UpdateForm : Form
    {
        public UpdateForm()
        {
            InitializeComponent();
        }

        private readonly JObject? _latestReleaseInfo;

        public UpdateForm(JObject latestReleaseInfo)
        {
            InitializeComponent();

            _latestReleaseInfo = latestReleaseInfo;
            DisplayReleaseInfo();
        }

        private void DisplayReleaseInfo()
        {
            var releaseName = _latestReleaseInfo?["name"]?.ToString() ?? _latestReleaseInfo?["tag_name"]?.ToString() ?? "";
            var releaseNotes = _latestReleaseInfo?["body"]?.ToString() ?? "";

            updateLabel.Text = $"Update Available: {releaseName}";
            releaseNotesTextBox.Text = releaseNotes;
        }

        private async void updateButton_Click(object sender, EventArgs e)
        {
            if (_latestReleaseInfo == null)
                return;

            var updater = new Updater();
            var installerUrl = updater.FindInstallerAsset(_latestReleaseInfo);

            if (installerUrl == null)
            {
                // No installer attached (e.g. zip-only release): let the user download it manually.
                UrlOpener.Open(_latestReleaseInfo["html_url"]?.ToString() ?? Updater.ReleasesPage);
                Close();
                return;
            }

            updateButton.Enabled = false;
            try
            {
                await updater.DownloadAndRunInstaller(installerUrl);
                DialogResult = DialogResult.Yes;
                Close();
                Application.Exit();
            }
            catch (Exception ex)
            {
                updateButton.Enabled = true;
                MessageBox.Show(this, $"Could not download the update: {ex.Message}", "Update", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
