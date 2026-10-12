using AiSeasonCreatorREDUX.Data;
using AiSeasonCreatorREDUX.Helpers;
using AiSeasonCreatorREDUX.Presenters;

namespace AiSeasonCreatorREDUX.Views
{
    public partial class SettingsForm : Form, ISettingsView
    {
        private DataSyncCoordinator? _sync;
        private Label? _connectionValue;
        private Label? _dataValue;
        private Button? _connectButton;
        private Button? _updateButton;
        private CheckBox? _autoUpdateCheckBox;

        public SettingsForm()
        {
            InitializeComponent();
        }
        private void SettingsForm_Load(object sender, EventArgs e)
        {
            ViewLoaded?.Invoke(this, e);
        }

        private void seasonFolderPathTextBox_Click(object sender, EventArgs e)
        {
            SeasonFolderClicked.Invoke(this, e);
        }

        private void rosterFolderPathTextBox_Click(object sender, EventArgs e)
        {
            RosterFolderClicked.Invoke(this, e);
        }

        /// <summary>Adds the "iRacing season data" section (sign in, update, auto-update).</summary>
        public void AttachDataSync(DataSyncCoordinator sync)
        {
            _sync = sync;

            var group = new GroupBox
            {
                Text = "iRacing season data",
                ForeColor = Color.Gainsboro,
                BackColor = Color.FromArgb(150, 18, 18, 27),
                Font = new Font("Segoe UI", 9.5F),
                Location = new Point(12, 165),
                Size = new Size(620, 250),
            };

            var intro = new Label
            {
                Text = "Series, schedules, cars, tracks, logos and weather are downloaded from iRacing with your own account " +
                       "and saved on this PC. New seasons show up automatically — no app update needed.",
                Location = new Point(14, 24),
                Size = new Size(590, 40),
                ForeColor = Color.FromArgb(190, 190, 200),
            };

            var connectionLabel = new Label { Text = "iRacing account:", Location = new Point(14, 74), AutoSize = true };
            _connectionValue = new Label { Location = new Point(140, 74), Size = new Size(460, 20), AutoEllipsis = true };

            var dataLabel = new Label { Text = "Data in use:", Location = new Point(14, 100), AutoSize = true };
            _dataValue = new Label { Location = new Point(140, 100), Size = new Size(460, 40), AutoEllipsis = true };

            _connectButton = CreateButton("Sign in", new Point(14, 150));
            _connectButton.Click += async (_, _) =>
            {
                if (_sync.Client.IsConnected)
                    await _sync.DisconnectAsync();
                else
                    await _sync.SignInAndUpdateAsync(TopLevelControl as IWin32Window ?? this);
            };

            _updateButton = CreateButton("Update now", new Point(164, 150));
            _updateButton.Click += async (_, _) => await _sync.UpdateNowAsync(TopLevelControl as IWin32Window ?? this, interactive: true);

            var folderButton = CreateButton("Open data folder", new Point(314, 150));
            folderButton.Click += (_, _) => UrlOpener.Open(AppPaths.LocalFolder);

            _autoUpdateCheckBox = new CheckBox
            {
                Text = "Check for new season data when the app starts (recommended)",
                Location = new Point(14, 200),
                AutoSize = true,
                Checked = _sync.AutoUpdate,
            };
            _autoUpdateCheckBox.CheckedChanged += (_, _) => _sync.AutoUpdate = _autoUpdateCheckBox.Checked;

            group.Controls.AddRange(new Control[]
            {
                intro, connectionLabel, _connectionValue, dataLabel, _dataValue,
                _connectButton, _updateButton, folderButton, _autoUpdateCheckBox,
            });
            Controls.Add(group);
            group.BringToFront();

            _sync.StateChanged += (_, _) => RefreshDataSyncSection();
            RefreshDataSyncSection();
        }

        private static Button CreateButton(string text, Point location)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(140, 32),
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 52),
                Cursor = Cursors.Hand,
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 85);
            return button;
        }

        private void RefreshDataSyncSection()
        {
            if (_sync == null || _connectionValue == null || IsDisposed)
                return;

            if (InvokeRequired)
            {
                BeginInvoke(RefreshDataSyncSection);
                return;
            }

            var client = _sync.Client;
            var manager = _sync.DataManager;

            _connectionValue.Text = client.IsConnected ? $"Signed in as {client.ConnectedDisplayName}" : "Not signed in";
            _dataValue!.Text = manager.IsRefreshing ? manager.StatusText : _sync.DescribeData();
            _connectButton!.Text = client.IsConnected ? "Sign out" : "Sign in";
            _connectButton.Enabled = !manager.IsRefreshing;
            _updateButton!.Enabled = !manager.IsRefreshing;
            _autoUpdateCheckBox!.Checked = _sync.AutoUpdate;
        }

        public event EventHandler ViewLoaded;
        public event EventHandler SeasonFolderClicked;
        public event EventHandler RosterFolderClicked;

        public ISettingsPresenter Presenter { get; set; }

        public string SeasonFolderPath
        {
            get { return seasonFolderPathTextBox.Text; }
            set { seasonFolderPathTextBox.Text = value; }
        }

        public string RosterFolderPath
        {
            get { return rosterFolderPathTextBox.Text; }
            set { rosterFolderPathTextBox.Text = value; }
        }

        public string SetFolderPathLabel
        {
            get { return checkFolderPathLabel.Text; }
            set { checkFolderPathLabel.Text = value; }
        }
    }
}
