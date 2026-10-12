using AiSeasonCreator.Helpers;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace AiSeasonCreator.Online
{
    /// <summary>
    /// Shows iRacing's own web UI so the user can sign in. The app never sees the password:
    /// it only waits until the web UI reports a valid session, then closes itself.
    /// </summary>
    public class IRacingConnectForm : Form
    {
        private static readonly Color Accent = Color.FromArgb(142, 188, 0);
        private static readonly Color Dark = Color.FromArgb(18, 18, 27);

        private readonly IRacingWebSession _session;
        private readonly WebView2 _webView;
        private readonly Label _statusLabel;
        private readonly System.Windows.Forms.Timer _probeTimer;
        private WebViewFetcher? _fetcher;
        private bool _probing;
        private bool _autoClickedLogin;

        public IRacingConnectForm(IRacingWebSession session)
        {
            _session = session;

            Text = "Connect to iRacing";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(1000, 780);
            MinimumSize = new Size(700, 560);
            BackColor = Dark;
            ShowInTaskbar = false;
            Icon = Application.OpenForms.Count > 0 ? Application.OpenForms[0]!.Icon : null;

            var header = new Panel { Dock = DockStyle.Top, Height = 74, BackColor = Dark, Padding = new Padding(16, 10, 16, 8) };
            var title = new Label
            {
                Text = "Sign in with your iRacing account",
                ForeColor = Color.White,
                Font = new Font("Segoe UI Semibold", 13F),
                AutoSize = true,
                Location = new Point(16, 10),
            };
            var subtitle = new Label
            {
                Text = "You are on iRacing's official website. AiSeasonCreator never sees your password — it only uses the session " +
                       "to read the current series, schedules and weather, exactly like the iRacing web UI does.",
                ForeColor = Color.FromArgb(190, 190, 200),
                Font = new Font("Segoe UI", 9F),
                AutoSize = false,
                Location = new Point(16, 38),
                Size = new Size(940, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            };
            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 46, BackColor = Dark };
            _statusLabel = new Label
            {
                Text = "Loading iRacing...",
                ForeColor = Accent,
                Font = new Font("Segoe UI", 9.5F),
                AutoSize = true,
                Location = new Point(16, 14),
            };
            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 52),
                Size = new Size(100, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
            };
            cancelButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 85);
            cancelButton.Location = new Point(footer.Width - cancelButton.Width - 16, 8);
            footer.Controls.Add(_statusLabel);
            footer.Controls.Add(cancelButton);
            CancelButton = cancelButton;

            _webView = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Dark };

            Controls.Add(_webView);
            Controls.Add(footer);
            Controls.Add(header);

            _probeTimer = new System.Windows.Forms.Timer { Interval = 2000 };
            _probeTimer.Tick += async (_, _) => await ProbeAsync();

            Shown += async (_, _) => await InitializeBrowserAsync();
            FormClosed += (_, _) =>
            {
                _probeTimer.Stop();
                _fetcher?.Dispose();
            };
        }

        private async Task InitializeBrowserAsync()
        {
            try
            {
                var environment = await _session.GetEnvironmentAsync();
                await _webView.EnsureCoreWebView2Async(environment);

                var core = _webView.CoreWebView2;
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.IsStatusBarEnabled = false;
                // Keep popups (e.g. "forgot password") inside this window.
                core.NewWindowRequested += (_, e) =>
                {
                    e.Handled = true;
                    core.Navigate(e.Uri);
                };
                core.NavigationCompleted += async (_, _) => await ProbeAsync();

                _fetcher = new WebViewFetcher(core);
                core.Navigate(IRacingWebSession.WebUiUrl);
                _probeTimer.Start();
            }
            catch (WebView2RuntimeNotFoundException)
            {
                ShowRuntimeMissing();
            }
            catch (Exception ex)
            {
                Log.Error("Could not start the embedded browser", ex);
                _statusLabel.ForeColor = Color.IndianRed;
                _statusLabel.Text = $"Could not start the embedded browser: {ex.Message}";
            }
        }

        private async Task ProbeAsync()
        {
            if (_probing || _fetcher == null || IsDisposed)
                return;

            if (!_fetcher.IsOnWebUiOrigin)
            {
                _statusLabel.Text = "Waiting for you to sign in...";
                return;
            }

            _probing = true;
            try
            {
                var response = await _fetcher.GetAsync(IRacingWebSession.BuildProxyDataUrl("member/info"), TimeSpan.FromSeconds(20), CancellationToken.None);
                if (IsDisposed)
                    return;

                if (response.IsSuccess)
                {
                    _probeTimer.Stop();
                    _statusLabel.Text = "Signed in! Downloading season data...";
                    DialogResult = DialogResult.OK;
                    Close();
                    return;
                }

                if (response.IsUnauthorized && !_autoClickedLogin)
                {
                    // Skip the "You are not logged in" splash and go straight to iRacing's login page.
                    _autoClickedLogin = await _fetcher.ClickLoginButtonAsync();
                }

                _statusLabel.Text = "Waiting for you to sign in...";
            }
            catch (Exception ex)
            {
                Log.Warn($"Sign-in probe failed: {ex.Message}");
            }
            finally
            {
                _probing = false;
            }
        }

        private void ShowRuntimeMissing()
        {
            _probeTimer.Stop();
            _statusLabel.ForeColor = Color.IndianRed;
            _statusLabel.Text = "Microsoft Edge WebView2 Runtime is missing.";

            var result = MessageBox.Show(this,
                "Signing in to iRacing needs the Microsoft Edge WebView2 Runtime, which is normally included with Windows 10/11.\n\n" +
                "Open the download page now?",
                "WebView2 Runtime required", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
                UrlOpener.Open("https://developer.microsoft.com/microsoft-edge/webview2/");

            DialogResult = DialogResult.Cancel;
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _probeTimer.Dispose();
                _webView.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
