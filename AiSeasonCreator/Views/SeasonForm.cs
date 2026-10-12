using AiSeasonCreator.Helpers;
using AiSeasonCreator.Presenters;
using Microsoft.VisualBasic.ApplicationServices;
using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AiSeasonCreator.Views
{


    public partial class SeasonForm : Form, ISeasonView
    {
        private static readonly Color SuccessColor = Color.FromArgb(128, 187, 0);
        private static readonly Color ErrorColor = Color.FromArgb(235, 87, 87);

        private readonly TextBox seriesSearchTextBox;
        private int _messageVersion;
        private string _autoSeasonName = "";

        public SeasonForm()
        {
            InitializeComponent();

            // Series search: there are well over 100 series, scrolling for one is painful.
            seriesSearchTextBox = new TextBox
            {
                PlaceholderText = "Search series...",
                Location = new Point(172, 9),
                Size = new Size(240, 24),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(18, 18, 27),
                ForeColor = Color.Gainsboro,
                Font = new Font("Segoe UI", 10F),
            };
            seriesSearchTextBox.TextChanged += (_, _) => ApplySeriesFilter();
            Controls.Add(seriesSearchTextBox);
            seriesSearchTextBox.BringToFront();

            messageFormLabel.AutoSize = false;
            messageFormLabel.Location = new Point(753, 850);
            messageFormLabel.Size = new Size(495, 74);
            messageFormLabel.TextAlign = ContentAlignment.MiddleLeft;
            messageFormLabel.Font = new Font("Segoe UI", 10F);

            toolTip1.AutoPopDelay = 15000;
        }

        private void SeasonForm_Load(object sender, EventArgs e)
        {
            ViewLoaded?.Invoke(this, e);
            messageFormLabel.Text = "";
        }

        private void seriesListCombo_SelectedIndexChanged(object sender, EventArgs e)
        {
            SeriesIndexChanged?.Invoke(this, e);
        }

        private void carCountTrackBar_ValueChanged(object sender, EventArgs e)
        {
            carCountValueLabel.Text = carCountTrackBar.Value.ToString();
        }

        private void rosterNameComboBox_Click(object sender, EventArgs e)
        {
            RosterClicked.Invoke(this, e);
        }

        private void SeriesSelectionButton_Click(object sender, EventArgs e)
        {
            SelectedCarId = 0;

            if (sender is Button clickedButton && clickedButton.Tag is ButtonInfo info)
            {
                foreach (Button button in seriesFlowLayoutPanel.Controls)
                {
                    if (button.Tag is ButtonInfo inf)
                        inf.IsSelected = false;
                    SetStandardButtonFont(button);
                }

                SetSelectedButtonFont(clickedButton);
                info.IsSelected = true;
                SelectedSeasonId = info.Id;

                // Suggest a season name until the user types their own.
                if (string.IsNullOrWhiteSpace(seasonNameTextBox.Text) || seasonNameTextBox.Text == _autoSeasonName)
                {
                    _autoSeasonName = info.OriginalName;
                    seasonNameTextBox.Text = _autoSeasonName;
                }

                SeriesIndexChanged?.Invoke(this, e);
            }
        }

        private void CarsSelectionButton_Click(object sender, EventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag is ButtonInfo info)
            {
                foreach (Button button in carsFlowLayoutPanel.Controls)
                {
                    if (button.Tag is ButtonInfo inf)
                        inf.IsSelected = false;
                    SetStandardButtonFont(button);
                }

                SetSelectedButtonFont(clickedButton);
                info.IsSelected = true;
                SelectedCarId = info.Id;
            }
        }

        private void TrackSelectionButton_Click(object sender, EventArgs e)
        {
            if (sender is Button button && button.Tag is ButtonInfo info)
            {
                info.IsSelected = !info.IsSelected;

                if (info.IsSelected)
                {
                    SetUnselectedButtonFont(button);
                }
                else
                {
                    SetStandardButtonFont(button);
                }
            }
        }

        private void createSeasonButton_Click(object sender, EventArgs e)
        {
            createSeasonButton.Visible = false;
            createSeasonButton.Visible = true;
            createSeasonButton.Refresh();

            var missing = MissingSelection();
            if (missing != null)
            {
                ShowMessage(missing, true);
                return;
            }

            messageFormLabel.Text = "";
            CreateSeasonClicked?.Invoke(this, e);
        }

        private string? MissingSelection()
        {
            if (SelectedSeasonId == 0)
                return "Please select a series.";
            if (SelectedCarId == 0)
                return "Please select your car.";
            if (SelectedTracks.Count == 0)
                return "Please keep at least one track selected.";
            if (string.IsNullOrWhiteSpace(SeasonName))
                return "Please give your season a name.";

            return null;
        }

        public void ShowMessage(string message, bool isError)
        {
            var version = ++_messageVersion;
            messageFormLabel.ForeColor = isError ? ErrorColor : SuccessColor;
            messageFormLabel.Text = message;
            BlinkLabel(messageFormLabel, version);

            if (!isError)
                ClearMessageLater(version);
        }

        private async void ClearMessageLater(int version)
        {
            await Task.Delay(10000);
            if (version == _messageVersion)
                messageFormLabel.Text = "";
        }

        private async void BlinkLabel(Label label, int version)
        {
            var color = label.ForeColor;
            for (var i = 0; i < 6 && version == _messageVersion; i++)
            {
                await Task.Delay(100);
                label.ForeColor = label.ForeColor == color ? Color.Black : color;
            }
            label.ForeColor = color;
        }

        private void ApplySeriesFilter()
        {
            var filter = seriesSearchTextBox.Text.Trim();
            seriesFlowLayoutPanel.SuspendLayout();
            foreach (Button button in seriesFlowLayoutPanel.Controls)
            {
                if (button.Tag is ButtonInfo info)
                    button.Visible = filter.Length == 0 || info.OriginalName.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
            }
            seriesFlowLayoutPanel.ResumeLayout();
        }

        private void PopulateSeriesButtons(IEnumerable<NameAndAsset> series)
        {
            SelectedSeasonId = 0;
            SelectedCarId = 0;
            ClearButtons(seriesFlowLayoutPanel);
            seriesFlowLayoutPanel.SuspendLayout();

            foreach (var item in series)
            {
                var button = new Button();

                SetFlowPanelButton(button, item);
                button.Click += SeriesSelectionButton_Click;
                seriesFlowLayoutPanel.Controls.Add(button);
            }

            seriesFlowLayoutPanel.ResumeLayout();
            ApplySeriesFilter();
        }

        private void ClearButtons(FlowLayoutPanel panel)
        {
            foreach (Control control in panel.Controls)
                toolTip1.SetToolTip(control, null);
            panel.Controls.Clear();
        }

        private void PopulateCarButtons(IEnumerable<NameAndAsset> cars)
        {
            SelectedCarId = 0;
            ClearButtons(carsFlowLayoutPanel);
            var carList = cars.ToList();

            foreach (var car in carList)
            {
                var button = new Button();

                SetCarFlowPanelButton(button, car);
                button.Click += CarsSelectionButton_Click;
                carsFlowLayoutPanel.Controls.Add(button);

                if (carList.Count == 1)
                    CarsSelectionButton_Click(button, EventArgs.Empty);
            }
        }

        private void PopulateTrackButtons(IEnumerable<NameAndAsset> tracks)
        {
            ClearButtons(availableTracksFlowLayoutPanel);

            foreach (var track in tracks)
            {
                var button = new Button();

                SetFlowPanelButton(button, track);
                button.TextImageRelation = TextImageRelation.Overlay;
                button.TextAlign = ContentAlignment.TopRight;
                button.Click += TrackSelectionButton_Click;
                availableTracksFlowLayoutPanel.Controls.Add(button);
            }
        }

        private void SetCarFlowPanelButton(Button button, NameAndAsset item)
        {
            var info = new ButtonInfo { Id = item.Id, OriginalName = item.Name, IsSelected = false };
            button.Tag = info;
            button.Text = item.Name;
            button.TextAlign = ContentAlignment.BottomCenter;
            button.AutoEllipsis = true;
            button.Image = item.Asset;
            toolTip1.SetToolTip(button, item.Tooltip ?? item.Name);
            button.TextImageRelation = TextImageRelation.ImageAboveText;
            button.ImageAlign = ContentAlignment.TopCenter;
            button.Height = 100;
            button.Width = 180;
            button.FlatAppearance.BorderSize = 0;
            SetStandardButtonFont(button);
            button.FlatStyle = FlatStyle.Flat;
        }

        private void SetFlowPanelButton(Button button, NameAndAsset item)
        {
            var info = new ButtonInfo { Id = item.Id, OriginalName = item.Name, IsSelected = false };
            button.Tag = info;
            button.Text = $"  {item.Name}";
            button.AutoEllipsis = true;
            button.Image = item.Asset;
            toolTip1.SetToolTip(button, item.Tooltip ?? item.Name);
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.ImageAlign = ContentAlignment.MiddleLeft;
            button.Height = 40;
            button.Width = 360;
            button.FlatAppearance.BorderSize = 0;
            SetStandardButtonFont(button);
            button.TextAlign = ContentAlignment.MiddleRight;
            button.FlatStyle = FlatStyle.Flat;
        }

        private void practiceLengthTrackBar_ValueChanged(object sender, EventArgs e)
        {
            practiceLengthValueLabel.Text = practiceLengthTrackBar.Value.ToString();

            if (practiceLengthTrackBar.Value == 1)
                practiceMinutesLabel.Text = "Minute";
            else
                practiceMinutesLabel.Text = "Minutes";
        }

        private void qualiLengthTrackBar_ValueChanged(object sender, EventArgs e)
        {
            qualiLengthValueLabel.Text = qualiLengthTrackBar.Value.ToString();

            if (qualiAloneCheckBox.Checked)
            {
                if (qualiLengthTrackBar.Value == 1)
                    qualiLapsOrMinutesLabel.Text = "Lap";
                else
                    qualiLapsOrMinutesLabel.Text = "Laps";
            }
            else
            {
                if (qualiLengthTrackBar.Value == 1)
                    qualiLapsOrMinutesLabel.Text = "Minute";
                else
                    qualiLapsOrMinutesLabel.Text = "Minutes";
            }
        }

        private void raceLengthTrackBar_ValueChanged(object sender, EventArgs e)
        {
            raceLengthValueLabel.Text = raceLengthTrackBar.Value.ToString();
        }

        private void qualiAloneCheckBox_CheckedChanged(object sender, EventArgs e)
        {

            if (qualiAloneCheckBox.Checked)
            {
                qualiLapsOrMinutesLabel.Text = "Laps";
                qualiLengthTrackBar.Maximum = 5;
            }
            else
            {
                qualiLapsOrMinutesLabel.Text = "Minutes";
                qualiLengthTrackBar.Maximum = 30;
            }

            QualiAloneChecked.Invoke(this, e);
        }

        private void adaptiveAiCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (adaptiveAiCheckBox.Checked)
                adaptiveAiComboBox.Enabled = true;
            else
                adaptiveAiComboBox.Enabled = false;
        }

        private void SetStandardButtonFont(Button button)
        {
            button.Font = new Font("Microsoft Sans Serif", 10, FontStyle.Regular);
            button.ForeColor = Color.FromArgb(220, 220, 220);
            button.BackColor = Color.FromArgb(60, 30, 30, 30);
            button.FlatAppearance.BorderSize = 0;
        }

        private void SetSelectedButtonFont(Button button)
        {
            button.ForeColor = Color.White;
            button.BackColor = Color.FromArgb(200, 36, 36, 54);
            button.FlatAppearance.BorderSize = 1;
        }

        private void SetUnselectedButtonFont(Button button)
        {
            button.Font = new Font("Microsoft Sans Serif", 10, FontStyle.Italic);
            button.ForeColor = Color.Gray;
            button.BackColor = Color.FromArgb(240, 99, 99, 99);
        }

        private void aiSkillRangeTrackBar_RangeChanged(object sender, EventArgs e)
        {
            aiSkillPerLabel.Text = $"{aiSkillRangeTrackBar.LowerValue.ToString()}%-{aiSkillRangeTrackBar.UpperValue.ToString()}%";
        }

        public event EventHandler ViewLoaded;
        public event EventHandler SeriesIndexChanged;
        public event EventHandler RosterClicked;
        public event EventHandler QualiAloneChecked;
        public event EventHandler CreateSeasonClicked;

        public ISeasonPresenter Presenter { get; set; }
        public IEnumerable<NameAndAsset> SeriesList
        {
            set { PopulateSeriesButtons(value); }
        }
        public IEnumerable<NameAndAsset> CarList
        {
            set { PopulateCarButtons(value); }
        }
        public IEnumerable<NameAndAsset> TrackList
        {
            set { PopulateTrackButtons(value); }
        }
        // Track buttons start included; clicking one toggles it off (ButtonInfo.IsSelected means "excluded").
        public IReadOnlyCollection<int> SelectedTracks
        {
            get
            {
                return availableTracksFlowLayoutPanel.Controls
                    .OfType<Button>()
                    .Where(btn => btn.Tag is ButtonInfo info && !info.IsSelected)
                    .Select(btn => ((ButtonInfo)btn.Tag).Id)
                    .ToList();
            }
        }

        public IEnumerable<string> RosterList
        {
            set { rosterNameComboBox.DataSource = value.ToList(); }
        }
        public int SelectedSeasonId { get; private set; }

        public bool RaceIsLapPercentage
        {
            set { raceMinutesOrPercentageLabel.Text = value ? "% of laps" : "Minutes"; }
        }

        public bool IsBusy
        {
            set
            {
                createSeasonButton.Enabled = !value;
                createSeasonButton.Text = value ? "Creating..." : "Create Season";
                UseWaitCursor = value;
            }
        }
        public string SeasonName
        {
            get { return seasonNameTextBox.Text.Trim(); }
        }

        public int SelectedCarId { get; private set; }

        public bool UseAdaptiveAi
        {
            get { return adaptiveAiCheckBox.Checked; }
            set { adaptiveAiCheckBox.Checked = value; }
        }

        public string AdaptiveAiDifficulty
        {
            get { return adaptiveAiComboBox.Text; }
            set { adaptiveAiComboBox.Text = value; }
        }

        public int AiMin
        {
            get { return aiSkillRangeTrackBar.LowerValue; }
            set { aiSkillRangeTrackBar.LowerValue = value; }
        }

        public int AiMax
        {
            get { return aiSkillRangeTrackBar.UpperValue; }
            set { aiSkillRangeTrackBar.UpperValue = value; }
        }

        public bool DisableDamage
        {
            get { return disableCarDamageCheckBox.Checked; }
            set { disableCarDamageCheckBox.Checked = value; }
        }

        public bool AiAvoids
        {
            get { return aiAvoidPlayerCheckBox.Checked; }
            set { aiAvoidPlayerCheckBox.Checked = value; }
        }

        public bool StaticWeather
        {
            get { return staticWeatherCheckBox.Checked; }
            set { staticWeatherCheckBox.Checked = value; }
        }

        public bool AfternoonRaces
        {
            get { return afternoonRacesCheckBox.Checked; }
            set { afternoonRacesCheckBox.Checked = value; }
        }

        public bool NeverRain
        {
            get { return neverRainsCheckBox.Checked; }
            set { neverRainsCheckBox.Checked = value; }
        }

        public bool QualiAlone
        {
            get { return qualiAloneCheckBox.Checked; }
            set { qualiAloneCheckBox.Checked = value; }
        }

        public bool ShortParade
        {
            get { return shortParadeCheckBox.Checked; }
            set { shortParadeCheckBox.Checked = value; }
        }

        public string RosterName
        {
            get { return rosterNameComboBox.Text; }
            set { rosterNameComboBox.Text = value; }
        }

        public int CarCount
        {
            get { return carCountTrackBar.Value; }
            set { carCountTrackBar.Value = Math.Clamp(value, carCountTrackBar.Minimum, carCountTrackBar.Maximum); }
        }

        public int PracticeLength
        {
            get { return practiceLengthTrackBar.Value; }
            set { practiceLengthTrackBar.Value = value; }
        }

        public int QualiLength
        {
            get { return qualiLengthTrackBar.Value; }
            set { qualiLengthTrackBar.Value = Math.Clamp(value, qualiLengthTrackBar.Minimum, qualiLengthTrackBar.Maximum); }
        }

        public int RaceLength
        {
            get { return raceLengthTrackBar.Value; }
            set { raceLengthTrackBar.Value = Math.Clamp(value, raceLengthTrackBar.Minimum, raceLengthTrackBar.Maximum); }
        }
    }
}
