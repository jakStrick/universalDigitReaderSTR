using System.Diagnostics;

namespace DigitAnalyzerSTR
{
    internal partial class MainForm : Form
    {
        private MoondreamEngine? _engine;
        private CancellationTokenSource? _cts;
        private AppSettings _settings = AppSettings.Load();
        private bool _processing;

        private TextBox _videoFolderBox = null!;
        private TextBox _outputFolderBox = null!;
        private ComboBox _intervalCombo = null!;
        private ListBox _queueList = null!;
        private ProgressBar _fileProgress = null!;
        private ProgressBar _overallProgress = null!;
        private Label _fileLabel = null!;
        private Label _overallLabel = null!;
        private RichTextBox _logBox = null!;
        private Button _startBtn = null!;
        private Button _stopBtn = null!;
        private Panel _modelBanner = null!;

        private static readonly Color BgDark = Color.FromArgb(22, 26, 32);
        private static readonly Color BgMid = Color.FromArgb(32, 38, 46);
        private static readonly Color BgPanel = Color.FromArgb(40, 47, 57);
        private static readonly Color Accent = Color.FromArgb(0, 188, 140);
        private static readonly Color AccentDim = Color.FromArgb(0, 120, 90);
        private static readonly Color TextPrimary = Color.FromArgb(220, 225, 230);
        private static readonly Color TextMuted = Color.FromArgb(130, 140, 150);
        private static readonly Color Danger = Color.FromArgb(220, 70, 70);

        public MainForm()
        {
            SuspendLayout();
            Text = "DigitAnalyzerSTR";
            Size = new Size(1340, 860);
            MinimumSize = new Size(1100, 720);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = BgDark;
            ForeColor = TextPrimary;
            Font = new Font("Segoe UI", 10f);

            BuildLayout();
            ResumeLayout(true);

            WireEvents();
            ApplySettings();

            Shown += async (s, e) =>
            {
                await EnsureModelReadyAsync();
                RefreshQueue();
            };
        }

        // ---------------------------------------------------------------
        // Layout
        // ---------------------------------------------------------------
        private void BuildLayout()
        {
            // Title bar
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 102,
                BackColor = BgMid,
                Padding = new Padding(20, 8, 20, 6)
            };
            titleBar.Controls.Add(new Label
            {
                Text = "DigitAnalyzerSTR",
                Font = new Font("Segoe UI", 16f, FontStyle.Bold),
                ForeColor = Accent,
                AutoSize = true,
                Location = new Point(0, 6)
            });
            titleBar.Controls.Add(new Label
            {
                Text = "Universal process display logger",
                Font = new Font("Segoe UI", 9f),
                ForeColor = TextMuted,
                AutoSize = true,
                Location = new Point(2, 34)
            });

            // Model banner — hidden until needed
            _modelBanner = new Panel
            {
                Dock = DockStyle.Top,
                Height = 90,
                BackColor = Color.FromArgb(80, 50, 0),
                Padding = new Padding(16, 0, 16, 0),
                Visible = false
            };
            var bannerLbl = new Label
            {
                Text = "⚠  Moondream2 model not found. Click Download to fetch it (~1.8 GB, one time only).",
                ForeColor = Color.FromArgb(255, 200, 80),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font("Segoe UI", 9.5f)
            };
            var downloadBtn = MakeButton("Download Model", AccentDim);
            downloadBtn.Dock = DockStyle.Right;
            downloadBtn.Width = 60;
            downloadBtn.Click += DownloadModel_Click;
            _modelBanner.Controls.Add(downloadBtn);
            _modelBanner.Controls.Add(bannerLbl);

            // Body — SplitContainer
            var body = new SplitContainer
            {
                Dock = DockStyle.Fill,
                SplitterWidth = 8,
                BackColor = BgDark,
                FixedPanel = FixedPanel.Panel1
            };
            body.Panel1.BackColor = BgDark;
            body.Panel2.BackColor = BgDark;

            // Defer splitter distance until form has real dimensions
            Shown += (s, e) =>
            {
                try
                {
                    body.Panel1MinSize = 560;
                    body.Panel2MinSize = 460;
                    body.SplitterDistance = 680;
                }
                catch { }
            };

            BuildLeftPanel(body.Panel1);
            BuildRightPanel(body.Panel2);

            // Fill added first, then Top panels in reverse visual order
            Controls.Add(body);
            Controls.Add(_modelBanner);
            Controls.Add(titleBar);
        }

        // ---------------------------------------------------------------
        // Left panel
        // ---------------------------------------------------------------
        private void BuildLeftPanel(SplitterPanel parent)
        {
            parent.Padding = new Padding(20, 20, 16, 20);

            // Gap is embedded in section label rows: tall row + large top margin
            // = guaranteed visible gap regardless of empty-row collapsing.
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 10,
                BackColor = BgDark
            };

            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));   // 0  first label (no top gap)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 1  video folder input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 2  label (16px gap baked in)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 3  output folder input
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 4  label (16px gap baked in)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 5  interval combo
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 6  label (16px gap baked in)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 130F));   // 7  queue list
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));   // 8  refresh btn
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116F));   // 9  start/stop

            layout.Controls.Add(TinyLabel("VIDEO FOLDER"), 0, 0);
            layout.Controls.Add(FolderRow(out _videoFolderBox, _settings.VideoFolder,
                () => BrowseFolder(_videoFolderBox)), 0, 1);

            layout.Controls.Add(GappedLabel("OUTPUT FOLDER"), 0, 2);
            layout.Controls.Add(FolderRow(out _outputFolderBox, _settings.OutputFolder,
                () => BrowseFolder(_outputFolderBox)), 0, 3);

            layout.Controls.Add(GappedLabel("SAMPLE INTERVAL"), 0, 4);
            _intervalCombo = new ComboBox
            {
                Dock = DockStyle.Fill,
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = BgPanel,
                ForeColor = TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10f)
            };
            foreach (int sec in new[] { 1, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 })
                _intervalCombo.Items.Add($"Every {sec} seconds");
            _intervalCombo.SelectedIndex = 1;
            layout.Controls.Add(_intervalCombo, 0, 5);

            layout.Controls.Add(GappedLabel("VIDEO QUEUE"), 0, 6);
            _queueList = new ListBox
            {
                Dock = DockStyle.Fill,
                BackColor = BgPanel,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 9f)
            };
            layout.Controls.Add(_queueList, 0, 7);

            var refreshBtn = MakeButton("↻  Refresh Queue", BgPanel);
            refreshBtn.Dock = DockStyle.Fill;
            refreshBtn.Margin = new Padding(0, 8, 0, 8);
            refreshBtn.Click += RefreshQueue_Click;
            layout.Controls.Add(refreshBtn, 0, 8);

            // Start / Stop
            var btnStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BgDark,
                Margin = new Padding(0)
            };
            btnStack.RowStyles.Add(new RowStyle(SizeType.Percent, 58F));
            btnStack.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));

            _startBtn = MakeButton("▶  Start Processing", Accent);
            _startBtn.Dock = DockStyle.Fill;
            _startBtn.ForeColor = Color.Black;
            _startBtn.Font = new Font("Segoe UI", 11f, FontStyle.Bold);
            _startBtn.Margin = new Padding(0, 0, 0, 6);

            _stopBtn = MakeButton("■  Stop", Danger);
            _stopBtn.Dock = DockStyle.Fill;
            _stopBtn.Enabled = false;
            _stopBtn.Font = new Font("Segoe UI", 10f);
            _stopBtn.Margin = new Padding(0);

            btnStack.Controls.Add(_startBtn, 0, 0);
            btnStack.Controls.Add(_stopBtn, 0, 1);
            layout.Controls.Add(btnStack, 0, 9);

            parent.Controls.Add(layout);
        }

        // ---------------------------------------------------------------
        // Right panel
        // ---------------------------------------------------------------
        private void BuildRightPanel(SplitterPanel parent)
        {
            parent.Padding = new Padding(16, 20, 20, 20);

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                BackColor = BgDark
            };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));   // 0 file label
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));   // 1 file bar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));   // 2 overall label (16px gap baked in)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));   // 3 overall bar
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 66F));   // 4 log header (16px gap baked in)
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 110F));   // 5 log + btn

            _fileLabel = SmallLabel("Current file: —");
            layout.Controls.Add(_fileLabel, 0, 0);

            _fileProgress = MakeProgressBar();
            layout.Controls.Add(_fileProgress, 0, 1);

            _overallLabel = new Label
            {
                Text = "Overall: —",
                ForeColor = Color.FromArgb(130, 140, 150),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Font = new Font("Segoe UI", 9f),
                Margin = new Padding(0, 16, 0, 4)
            };
            layout.Controls.Add(_overallLabel, 0, 2);

            _overallProgress = MakeProgressBar();
            layout.Controls.Add(_overallProgress, 0, 3);

            var logHeader = new Label
            {
                Text = "ACTIVITY LOG",
                Font = new Font("Segoe UI", 8f, FontStyle.Bold),
                ForeColor = Color.FromArgb(0, 188, 140),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.BottomLeft,
                Margin = new Padding(0, 16, 0, 4)
            };
            layout.Controls.Add(logHeader, 0, 4);

            var logStack = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                BackColor = BgDark
            };
            logStack.RowStyles.Add(new RowStyle(SizeType.Percent, 110F));
            logStack.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));

            _logBox = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = BgPanel,
                ForeColor = TextPrimary,
                BorderStyle = BorderStyle.None,
                ReadOnly = true,
                Font = new Font("Cascadia Code", 9f),
                ScrollBars = RichTextBoxScrollBars.Vertical
            };
            logStack.Controls.Add(_logBox, 0, 0);

            var openBtn = MakeButton("📂  Open Output Folder", BgMid);
            openBtn.Dock = DockStyle.Fill;
            openBtn.Margin = new Padding(0, 6, 0, 0);
            openBtn.Click += (s, e) =>
            {
                string p = _outputFolderBox.Text;
                if (Directory.Exists(p)) Process.Start("explorer.exe", p);
            };
            logStack.Controls.Add(openBtn, 0, 1);

            layout.Controls.Add(logStack, 0, 5);
            parent.Controls.Add(layout);
        }

        // ---------------------------------------------------------------
        // Control factories
        // ---------------------------------------------------------------
        private static Label TinyLabel(string text) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 188, 140),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 4, 0, 4)
        };

        // Section label with 16px gap baked into its top margin.
        // The parent row must be 40px tall (16 top + ~20 text + 4 bottom).
        private static Label GappedLabel(string text) => new()
        {
            Text = text,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 188, 140),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.BottomLeft,
            Margin = new Padding(0, 16, 0, 4)
        };

        private static Label SmallLabel(string text) => new()
        {
            Text = text,
            ForeColor = Color.FromArgb(130, 140, 150),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 9f)
        };

        private static Panel FolderRow(out TextBox tb, string initial, Action onBrowse)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            tb = new TextBox
            {
                Text = initial,
                BackColor = Color.FromArgb(40, 47, 57),
                ForeColor = Color.FromArgb(220, 225, 230),
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5f)
            };
            var btn = new Button
            {
                Text = "...",
                Dock = DockStyle.Right,
                Width = 46,
                BackColor = Color.FromArgb(50, 58, 70),
                ForeColor = Color.FromArgb(220, 225, 230),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5f)
            };
            btn.FlatAppearance.BorderColor = Color.FromArgb(70, 80, 95);
            btn.Click += (s, e) => onBrowse();
            panel.Controls.Add(btn);
            panel.Controls.Add(tb);
            return panel;
        }

        private static Button MakeButton(string text, Color bg) => new()
        {
            Text = text,
            BackColor = bg,
            ForeColor = Color.FromArgb(220, 225, 230),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 9.5f)
        };

        private static ProgressBar MakeProgressBar() => new()
        {
            Dock = DockStyle.Fill,
            Style = ProgressBarStyle.Continuous,
            BackColor = Color.FromArgb(40, 47, 57),
            ForeColor = Color.FromArgb(0, 188, 140),
            Margin = new Padding(0, 0, 0, 2)
        };

        // ---------------------------------------------------------------
        // Events
        // ---------------------------------------------------------------
        private void WireEvents()
        {
            _startBtn.Click += StartProcessing_Click;
            _stopBtn.Click += (s, e) => _cts?.Cancel();
            _videoFolderBox.TextChanged += (s, e) =>
            {
                _settings.VideoFolder = _videoFolderBox.Text;
                RefreshQueue();
            };
        }

        private void ApplySettings()
        {
            _videoFolderBox.Text = _settings.VideoFolder;
            _outputFolderBox.Text = _settings.OutputFolder;
            var intervals = new[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55, 60 };
            _intervalCombo.SelectedIndex = Math.Max(0, Array.IndexOf(intervals, _settings.IntervalSeconds));
        }

        // ---------------------------------------------------------------
        // Model
        // ---------------------------------------------------------------
        private async Task EnsureModelReadyAsync()
        {
            if (MoondreamEngine.IsModelReady())
            {
                Log("Moondream2 model ready.", accent: true);
                InitEngine();
            }
            else
            {
                _modelBanner.Visible = true;
                _startBtn.Enabled = false;
                Log("Model not found. Download required before processing.", warn: true);
            }
            await Task.CompletedTask;
        }

        private void InitEngine()
        {
            try { _engine = new MoondreamEngine(); Log("Inference engine initialised."); }
            catch (Exception ex) { Log($"Failed to load model: {ex.Message}", warn: true); }
        }

        private async void DownloadModel_Click(object? sender, EventArgs e)
        {
            _modelBanner.Visible = false;
            SetBusy(true);
            Log("Downloading Moondream2 model (~1.8 GB)...");
            try
            {
                _cts = new CancellationTokenSource();
                var prog = new Progress<DownloadProgress>(p =>
                {
                    _fileLabel.Text = p.Message;
                    _fileProgress.Value = p.Percent;
                    _overallProgress.Value = p.Overall;
                    _overallLabel.Text = $"File {p.FileIndex} of {p.FileCount}";
                });
                await ModelDownloader.DownloadAsync(prog, _cts.Token);
                Log("Download complete.", accent: true);
                InitEngine();
                _startBtn.Enabled = true;
            }
            catch (OperationCanceledException) { Log("Download cancelled.", warn: true); _modelBanner.Visible = true; }
            catch (Exception ex) { Log($"Download failed: {ex.Message}", warn: true); _modelBanner.Visible = true; }
            finally { SetBusy(false); }
        }

        // ---------------------------------------------------------------
        // Queue
        // ---------------------------------------------------------------
        private void RefreshQueue_Click(object? sender, EventArgs e) => RefreshQueue();

        private void RefreshQueue()
        {
            _queueList.Items.Clear();
            string folder = _videoFolderBox.Text;
            if (!Directory.Exists(folder)) return;

            var files = Directory.GetFiles(folder, "*.mp4")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastWriteTime)
                .ToList();

            foreach (var f in files)
                _queueList.Items.Add(f.Name);

            Log($"Queue refreshed — {files.Count} video(s) found.");
        }

        // ---------------------------------------------------------------
        // Processing
        // ---------------------------------------------------------------
        private async void StartProcessing_Click(object? sender, EventArgs e)
        {
            if (_engine == null) { Log("Model not ready.", warn: true); return; }
            if (_queueList.Items.Count == 0) { Log("Queue is empty. Select a video folder.", warn: true); return; }
            if (string.IsNullOrWhiteSpace(_outputFolderBox.Text)) { Log("Please select an output folder.", warn: true); return; }

            _settings.VideoFolder = _videoFolderBox.Text;
            _settings.OutputFolder = _outputFolderBox.Text;
            _settings.IntervalSeconds = int.Parse(_intervalCombo.Text.Split(' ')[1]);
            _settings.Save();

            SetBusy(true);
            _cts = new CancellationTokenSource();

            var files = Directory.GetFiles(_videoFolderBox.Text, "*.mp4")
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastWriteTime)
                .Select(f => f.FullName)
                .ToList();

            Log($"Starting batch — {files.Count} video(s), {_settings.IntervalSeconds}s interval.");

            int done = 0;
            foreach (var file in files)
            {
                if (_cts.Token.IsCancellationRequested) break;

                _overallLabel.Text = $"Video {done + 1} of {files.Count}: {Path.GetFileName(file)}";
                _overallProgress.Value = files.Count > 0 ? done * 100 / files.Count : 0;
                Log($"Processing: {Path.GetFileName(file)}");

                var prog = new Progress<VideoProcessorProgress>(p =>
                {
                    _fileLabel.Text = p.Message;
                    _fileProgress.Value = Math.Min(100, p.Percent);
                });

                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string csv = Path.Combine(_settings.OutputFolder,
                    $"digitReader_{timestamp}.csv");

                var result = await Task.Run(() =>
                    new VideoProcessor(_engine, file, csv, _settings.IntervalSeconds, prog, _cts.Token)
                        .RunAsync());

                Log(result.ToString(), accent: result.Success, warn: !result.Success);
                done++;
            }

            _overallProgress.Value = 100;
            _overallLabel.Text = _cts.Token.IsCancellationRequested
                ? "Stopped." : $"Done — {done} video(s) processed.";
            Log(_cts.Token.IsCancellationRequested ? "Processing stopped by user." : "Batch complete.");
            SetBusy(false);
            await Task.Delay(2000);
            _fileProgress.Value = 0;
            _overallProgress.Value = 0;
            _fileLabel.Text = "Current file: —";
            _overallLabel.Text = "Overall: —";
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private void BrowseFolder(TextBox target)
        {
            using var dlg = new FolderBrowserDialog
            {
                SelectedPath = Directory.Exists(target.Text) ? target.Text
                    : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            };
            if (dlg.ShowDialog() == DialogResult.OK) target.Text = dlg.SelectedPath;
        }

        private void SetBusy(bool busy)
        {
            _processing = busy;
            _startBtn.Enabled = !busy && _engine != null;
            _stopBtn.Enabled = busy;
            _intervalCombo.Enabled = !busy;
        }

        private void Log(string message, bool accent = false, bool warn = false)
        {
            if (_logBox.InvokeRequired) { _logBox.Invoke(() => Log(message, accent, warn)); return; }
            _logBox.SelectionStart = _logBox.TextLength;
            _logBox.SelectionLength = 0;
            _logBox.SelectionColor = warn ? Color.FromArgb(220, 120, 80)
                                    : accent ? Color.FromArgb(0, 200, 150)
                                    : TextPrimary;
            _logBox.AppendText($"[{DateTime.Now:HH:mm:ss}]  {message}\n");
            _logBox.ScrollToCaret();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (_processing)
            {
                if (MessageBox.Show("Processing is running. Stop and exit?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.No)
                { e.Cancel = true; return; }
                _cts?.Cancel();
            }
            _engine?.Dispose();
            _settings.Save();
            base.OnFormClosing(e);
        }
    }
}