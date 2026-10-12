using AiSeasonCreator.Data;
using AiSeasonCreator.DefaultUserSettings;
using AiSeasonCreator.Helpers;
using AiSeasonCreator.Repos;

namespace AiSeasonCreator.Views
{
    public partial class MainForm : Form, IMainView
    {
        private static readonly Color StatusBack = Color.FromArgb(18, 18, 27);
        private static readonly Color StatusText = Color.FromArgb(200, 200, 210);
        private static readonly Color Accent = Color.FromArgb(142, 188, 0);
        private static readonly Color Warning = Color.FromArgb(255, 183, 77);

        private SeasonForm _seasonForm;
        private RosterForm _rosterForm;
        private AboutForm _aboutForm;
        private SettingsForm _settingsForm;
        private IJsonRepo _jsonRepo;
        private LoadedData _loadedData;
        private readonly DataSyncCoordinator _sync;
        private Form _selectedForm;
        public static ISeasonView staticSeasonForm { get; set; }

        private Panel statusPanel = null!;
        private Label dataStatusLabel = null!;
        private Label connectionLabel = null!;
        private Button signInButton = null!;
        private Button updateButton = null!;

        public MainForm(
            SeasonForm seasonView,
            RosterForm rosterForm,
            AboutForm aboutForm,
            SettingsForm settingsView,
            IJsonRepo jsonRepo,
            LoadedData loadedData,
            DataSyncCoordinator sync)
        {
            InitializeComponent();
            _seasonForm = seasonView;
            _rosterForm = rosterForm;
            _aboutForm = aboutForm;
            _settingsForm = settingsView;
            _jsonRepo = jsonRepo;
            _loadedData = loadedData;
            _sync = sync;

            Text = $"AiSeasonCreator {Application.ProductVersion.Split('+')[0]}";
            BuildStatusBar();
            _sync.StateChanged += (_, _) => UpdateStatusBar();
            Shown += async (_, _) => await OnShownAsync();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            OpenChildForm(_settingsForm);
            OpenChildForm(_seasonForm);
            SelectedNavButton(seasonButton);
            RestoreWindowLocation();
            AreFolderPathsBad();
            UpdateStatusBar();
        }

        private async Task OnShownAsync()
        {
            new Updater().CheckForUpdatesInBackground(this);
            await _sync.OnStartupAsync(this);
        }

        /// <summary>Bottom bar: which season data is loaded, iRacing connection, and manual update.</summary>
        private void BuildStatusBar()
        {
            const int height = 34;
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + height);

            statusPanel = new Panel
            {
                BackColor = StatusBack,
                Location = new Point(0, ClientSize.Height - height),
                Size = new Size(ClientSize.Width, height),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom,
            };

            dataStatusLabel = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = StatusText,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(12, 0),
                Size = new Size(760, height),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            connectionLabel = new Label
            {
                AutoSize = false,
                AutoEllipsis = true,
                ForeColor = StatusText,
                Font = new Font("Segoe UI", 9F),
                Location = new Point(780, 0),
                Size = new Size(270, height),
                TextAlign = ContentAlignment.MiddleRight,
            };

            signInButton = CreateStatusButton("Sign in to iRacing", new Point(1058, 4), new Size(130, 26));
            signInButton.Click += async (_, _) => await OnSignInClickedAsync();

            updateButton = CreateStatusButton("Update now", new Point(1194, 4), new Size(116, 26));
            updateButton.Click += async (_, _) => await _sync.UpdateNowAsync(this, interactive: true);

            statusPanel.Controls.AddRange(new Control[] { dataStatusLabel, connectionLabel, signInButton, updateButton });
            Controls.Add(statusPanel);
            statusPanel.BringToFront();
        }

        private static Button CreateStatusButton(string text, Point location, Size size)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 52),
                Font = new Font("Segoe UI", 8.5F),
                Cursor = Cursors.Hand,
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 85);
            return button;
        }

        private async Task OnSignInClickedAsync()
        {
            if (_sync.Client.IsConnected)
            {
                var answer = MessageBox.Show(this, "Sign out of iRacing in AiSeasonCreator?", "Sign out",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                    await _sync.DisconnectAsync();
                return;
            }

            await _sync.UpdateNowAsync(this, interactive: true);
        }

        private void UpdateStatusBar()
        {
            if (InvokeRequired)
            {
                BeginInvoke(UpdateStatusBar);
                return;
            }

            var manager = _sync.DataManager;

            if (manager.IsRefreshing)
            {
                dataStatusLabel.ForeColor = Accent;
                dataStatusLabel.Text = string.IsNullOrEmpty(manager.StatusText) ? "Updating season data..." : manager.StatusText;
            }
            else if (!string.IsNullOrEmpty(manager.StatusText))
            {
                dataStatusLabel.ForeColor = Warning;
                dataStatusLabel.Text = $"{manager.StatusText}   ({_sync.DescribeData()})";
            }
            else
            {
                dataStatusLabel.ForeColor = manager.HasLiveData && !manager.IsSeasonOver ? StatusText : Warning;
                dataStatusLabel.Text = _sync.DescribeData();
            }

            connectionLabel.Text = _sync.Client.IsConnected ? $"iRacing: {_sync.Client.ConnectedDisplayName}" : "";
            signInButton.Text = _sync.Client.IsConnected ? "Sign out" : "Sign in to iRacing";
            signInButton.Enabled = !manager.IsRefreshing;
            updateButton.Enabled = !manager.IsRefreshing;
            updateButton.Text = manager.IsRefreshing ? "Updating..." : "Update now";
        }

        private void RestoreWindowLocation()
        {
            var udl = _loadedData.UserDefaultSettings.WindowLocation;
            var saved = new Rectangle(udl.X, udl.Y, Width, Height);

            // Only restore when the title bar is on a screen that still exists (e.g. after unplugging a monitor).
            var titleBar = new Rectangle(saved.X, saved.Y, saved.Width, 40);
            if (Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(titleBar)))
                Location = saved.Location;
            else
                CenterToScreen();
        }

        private void OpenChildForm(Form childForm)
        {
            if (_selectedForm != childForm)
            {
                childForm.TopLevel = false;
                mainPanel.Controls.Add(childForm);
                childForm.FormBorderStyle = FormBorderStyle.None;
                childForm.Dock = DockStyle.Fill;
                mainPanel.Tag = childForm;
                childForm.BringToFront();
                childForm.Show();
                _selectedForm = childForm;
                ClearNavButtons();
            }
        }

        private void seasonButton_Click(object sender, EventArgs e)
        {
            if (AreFolderPathsBad()) { return; }
            OpenChildForm(_seasonForm);
            SelectedNavButton(sender as Button);
        }

        private void rosterButton_Click(object sender, EventArgs e)
        {
            if (AreFolderPathsBad()) { return; }
            OpenChildForm(_rosterForm);
            SelectedNavButton(sender as Button);
        }

        private void aboutButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(_aboutForm);
            SelectedNavButton(sender as Button);
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            OpenChildForm(_settingsForm);
            SelectedNavButton(sender as Button);
        }

        private void SelectedNavButton(Button button)
        {
            button.BackColor = Color.FromArgb(142, 188, 0);
        }

        private void ClearNavButtons()
        {
            seasonButton.BackColor = Color.Transparent;
            rosterButton.BackColor = Color.Transparent;
            aboutButton.BackColor = Color.Transparent;
            settingsButton.BackColor = Color.Transparent;
        }

        private bool AreFolderPathsBad()
        {
            var sp = _settingsForm.SeasonFolderPath;
            var rp = _settingsForm.RosterFolderPath;

            if (!Directory.Exists(sp) || !Directory.Exists(rp))
            {
                OpenChildForm(_settingsForm);
                SelectedNavButton(settingsButton);
                _settingsForm.SetFolderPathLabel = "Please select a folder for Seasons and Rosters";
                return true;
            }
            _settingsForm.SetFolderPathLabel = "";
            return false;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            var ud = _loadedData.UserDefaultSettings;

            ud.FolderLocations.SeasonsFolder = _settingsForm.SeasonFolderPath;
            ud.FolderLocations.RostersFolder = _settingsForm.RosterFolderPath;

            var udc = ud.CheckBoxesUserValues;
            udc.UseAdaptiveAi = _seasonForm.UseAdaptiveAi;
            udc.DisableCarDamage = _seasonForm.DisableDamage;
            udc.AiAvoidsPlayer = _seasonForm.AiAvoids;
            udc.StaticWeather = _seasonForm.StaticWeather;
            udc.AfternoonRaces = _seasonForm.AfternoonRaces;
            udc.NeverRain = _seasonForm.NeverRain;
            udc.QualifyAlone = _seasonForm.QualiAlone;
            udc.ShortParade = _seasonForm.ShortParade;

            ud.TrackBarUserValues.AiMinSkill = _seasonForm.AiMin;
            ud.TrackBarUserValues.AiMaxSkill = _seasonForm.AiMax;
            ud.ComboBoxesUserValues.AdaptiveAiSkillLevel = _seasonForm.AdaptiveAiDifficulty;

            if (WindowState == FormWindowState.Normal)
            {
                ud.WindowLocation.X = Location.X;
                ud.WindowLocation.Y = Location.Y;
            }

            try
            {
                _jsonRepo.Save<UserDefaultSettings>(AppPaths.UserSettingsFile, ud);
            }
            catch (Exception ex)
            {
                Log.Error("Could not save user settings", ex);
            }
        }
    }
}
