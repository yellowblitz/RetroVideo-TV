using System.Diagnostics;
using System.Text;

namespace RetroVideo.Win;

public sealed class MainForm : Form
{
    private readonly MpvPlayer _player = new();
    private readonly Panel _video = new() { Dock = DockStyle.Fill, BackColor = Color.Black };
    private readonly TrackBar _seek = new() { Minimum = 0, Maximum = 10000, TickStyle = TickStyle.None, Dock = DockStyle.Fill };
    private readonly TrackBar _volume = new() { Minimum = 0, Maximum = 130, Value = 100, TickStyle = TickStyle.None, Width = 120 };
    private readonly Label _time = new() { Text = "00:00 / 00:00", AutoSize = true, TextAlign = ContentAlignment.MiddleCenter };
    private readonly Label _title = new() { Text = "RetroVideo", AutoEllipsis = true, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _playPause = new() { Text = "▶", Width = 44, Height = 32 };
    private readonly Button _mute = new() { Text = "🔊", Width = 44, Height = 32 };
    private readonly Button _audio = new() { Text = "Audio", AutoSize = true, Height = 32 };
    private readonly Button _subs = new() { Text = "Subtitles", AutoSize = true, Height = 32 };
    private readonly Button _filters = new() { Text = "Filters", AutoSize = true, Height = 32 };
    private readonly Button _fullscreen = new() { Text = "⛶", Width = 44, Height = 32 };
    private readonly Panel _filterPanel = new() { Dock = DockStyle.Right, Width = 280, Padding = new Padding(12), Visible = false };
    private readonly ComboBox _nativeFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly ComboBox _slangPreset = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly List<string?> _slangPaths = new();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 250 };
    private bool _draggingSeek;
    private bool _fullscreenMode;
    private Rectangle _windowedBounds;
    private FormBorderStyle _windowedBorder;
    private string? _currentVideo;

    private string RetroArchDir => Path.Combine(AppContext.BaseDirectory, "retroarch");
    private string RetroArchExe => Path.Combine(RetroArchDir, "retroarch.exe");

    public MainForm(string? initialVideo)
    {
        Text = "RetroVideo v0.2";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(840, 520);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;
        AllowDrop = true;

        BuildUi();
        WireEvents();
        LoadFilterChoices();

        Shown += (_, _) =>
        {
            try
            {
                _player.Initialize(_video.Handle);
                _player.SetVolume(_volume.Value);
                _timer.Start();
                if (!string.IsNullOrWhiteSpace(initialVideo) && File.Exists(initialVideo))
                    OpenVideo(initialVideo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message + "\n\nMake sure libmpv-2.dll is beside RetroVideo.exe.", "Playback engine error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }

    private void BuildUi()
    {
        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 46, ColumnCount = 4, Padding = new Padding(8) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        var open = new Button { Text = "Open", Dock = DockStyle.Fill };
        open.Click += (_, _) => BrowseVideo();
        top.Controls.Add(open, 0, 0);
        top.Controls.Add(_title, 1, 0);
        top.Controls.Add(_filters, 2, 0);
        var more = new Button { Text = "Slang", Dock = DockStyle.Fill };
        more.Click += (_, _) => { _filterPanel.Visible = true; };
        top.Controls.Add(more, 3, 0);

        var bottom = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 92, RowCount = 2, ColumnCount = 1, Padding = new Padding(8, 2, 8, 8) };
        bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
        bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        bottom.Controls.Add(_seek, 0, 0);

        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, FlowDirection = FlowDirection.LeftToRight, AutoScroll = true };
        var back = new Button { Text = "-10s", Width = 54, Height = 32 };
        var forward = new Button { Text = "+10s", Width = 54, Height = 32 };
        back.Click += (_, _) => Safe(() => _player.SeekRelative(-10));
        forward.Click += (_, _) => Safe(() => _player.SeekRelative(10));
        controls.Controls.AddRange(new Control[] { _playPause, back, forward, _time, _audio, _subs, _filters, _mute, _volume, _fullscreen });
        bottom.Controls.Add(controls, 0, 1);

        BuildFilterPanel();

        Controls.Add(_video);
        Controls.Add(_filterPanel);
        Controls.Add(bottom);
        Controls.Add(top);
    }

    private void BuildFilterPanel()
    {
        _filterPanel.BackColor = SystemColors.Control;
        var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
        stack.Controls.Add(new Label { Text = "FILTERS", Font = new Font(Font, FontStyle.Bold), AutoSize = true });
        stack.Controls.Add(new Label { Text = "Native GPU preset", AutoSize = true, Margin = new Padding(3, 14, 3, 3) });
        _nativeFilter.Width = 240;
        stack.Controls.Add(_nativeFilter);

        stack.Controls.Add(new Label { Text = "Brightness", AutoSize = true, Margin = new Padding(3, 14, 3, 0) });
        stack.Controls.Add(MakePictureSlider("brightness"));
        stack.Controls.Add(new Label { Text = "Contrast", AutoSize = true });
        stack.Controls.Add(MakePictureSlider("contrast"));
        stack.Controls.Add(new Label { Text = "Saturation", AutoSize = true });
        stack.Controls.Add(MakePictureSlider("saturation"));
        stack.Controls.Add(new Label { Text = "Gamma", AutoSize = true });
        stack.Controls.Add(MakePictureSlider("gamma"));

        stack.Controls.Add(new Label { Text = "RetroArch Slang compatibility", Font = new Font(Font, FontStyle.Bold), AutoSize = true, Margin = new Padding(3, 18, 3, 4) });
        stack.Controls.Add(new Label { Text = "Real .slangp presets are kept here. This temporarily hands playback to RetroArch's shader renderer.", MaximumSize = new Size(240, 0), AutoSize = true });
        _slangPreset.Width = 240;
        stack.Controls.Add(_slangPreset);
        var custom = new Button { Text = "Choose custom .slangp...", Width = 240 };
        custom.Click += (_, _) => BrowseSlang();
        stack.Controls.Add(custom);
        var slangPlay = new Button { Text = "Play current video with Slang", Width = 240, Height = 36 };
        slangPlay.Click += (_, _) => PlayWithSlang();
        stack.Controls.Add(slangPlay);

        var close = new Button { Text = "Close filters", Width = 240, Margin = new Padding(3, 18, 3, 3) };
        close.Click += (_, _) => _filterPanel.Visible = false;
        stack.Controls.Add(close);
        _filterPanel.Controls.Add(stack);
    }

    private TrackBar MakePictureSlider(string property)
    {
        var slider = new TrackBar { Minimum = -100, Maximum = 100, Value = 0, TickStyle = TickStyle.None, Width = 240 };
        slider.Scroll += (_, _) => Safe(() => _player.SetPicture(property, slider.Value));
        return slider;
    }

    private void WireEvents()
    {
        _playPause.Click += (_, _) => Safe(() => _player.TogglePause());
        _mute.Click += (_, _) => Safe(() => _player.SetMute(!_player.Muted));
        _volume.Scroll += (_, _) => Safe(() => _player.SetVolume(_volume.Value));
        _audio.Click += (_, _) => ShowAudioMenu();
        _subs.Click += (_, _) => ShowSubtitleMenu();
        _filters.Click += (_, _) => _filterPanel.Visible = !_filterPanel.Visible;
        _fullscreen.Click += (_, _) => ToggleFullscreen();
        _video.DoubleClick += (_, _) => ToggleFullscreen();

        _seek.MouseDown += (_, _) => _draggingSeek = true;
        _seek.MouseUp += (_, _) =>
        {
            var duration = _player.Duration;
            if (duration > 0)
                Safe(() => _player.SeekAbsolute(duration * _seek.Value / _seek.Maximum));
            _draggingSeek = false;
        };

        _nativeFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_nativeFilter.SelectedItem is string preset)
                Safe(() => _player.ApplyNativeFilter(preset));
        };

        _timer.Tick += (_, _) => RefreshPlaybackUi();

        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                OpenVideo(files[0]);
        };

        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.O) { BrowseVideo(); e.Handled = true; }
            else if (e.KeyCode == Keys.Space) { Safe(() => _player.TogglePause()); e.Handled = true; }
            else if (e.KeyCode == Keys.Left) { Safe(() => _player.SeekRelative(-5)); e.Handled = true; }
            else if (e.KeyCode == Keys.Right) { Safe(() => _player.SeekRelative(5)); e.Handled = true; }
            else if (e.KeyCode == Keys.F) { ToggleFullscreen(); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape && _fullscreenMode) { ToggleFullscreen(); e.Handled = true; }
        };

        FormClosing += (_, _) => _player.Dispose();
    }

    private void BrowseVideo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open video",
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.wmv;*.asf;*.mov;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.flv;*.ogv;*.vob;*.divx|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            OpenVideo(dialog.FileName);
    }

    private void OpenVideo(string path)
    {
        if (!File.Exists(path)) return;
        _currentVideo = path;
        _title.Text = Path.GetFileName(path);
        Safe(() =>
        {
            _player.LoadFile(path);
            _player.SetVolume(_volume.Value);
        });
    }

    private void ShowAudioMenu()
    {
        var menu = new ContextMenuStrip();
        var tracks = _player.GetTracks().Where(t => t.Type == "audio").ToList();
        if (tracks.Count == 0)
            menu.Items.Add("No audio tracks").Enabled = false;
        foreach (var track in tracks)
        {
            var item = new ToolStripMenuItem(track.DisplayName) { Checked = track.Selected };
            item.Click += (_, _) => Safe(() => _player.SelectAudio(track.Id));
            menu.Items.Add(item);
        }
        menu.Show(_audio, new Point(0, _audio.Height));
    }

    private void ShowSubtitleMenu()
    {
        var menu = new ContextMenuStrip();
        var off = new ToolStripMenuItem("Off");
        off.Click += (_, _) => Safe(() => _player.SelectSubtitle(null));
        menu.Items.Add(off);
        menu.Items.Add(new ToolStripSeparator());

        var tracks = _player.GetTracks().Where(t => t.Type == "sub").ToList();
        foreach (var track in tracks)
        {
            var item = new ToolStripMenuItem(track.DisplayName) { Checked = track.Selected };
            item.Click += (_, _) => Safe(() => _player.SelectSubtitle(track.Id));
            menu.Items.Add(item);
        }
        if (tracks.Count == 0)
            menu.Items.Add("No embedded subtitles").Enabled = false;

        menu.Items.Add(new ToolStripSeparator());
        var load = new ToolStripMenuItem("Load external subtitle...");
        load.Click += (_, _) => LoadExternalSubtitle();
        menu.Items.Add(load);
        menu.Show(_subs, new Point(0, _subs.Height));
    }

    private void LoadExternalSubtitle()
    {
        using var dialog = new OpenFileDialog { Filter = "Subtitle files|*.srt;*.ass;*.ssa;*.vtt;*.sub|All files|*.*", Title = "Load subtitle" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Safe(() => _player.AddSubtitle(dialog.FileName));
    }

    private void LoadFilterChoices()
    {
        _nativeFilter.Items.AddRange(new object[] { "None", "Soft upscale", "Sharp upscale", "Clean / deband", "Deinterlace", "Pixel / nearest" });
        _nativeFilter.SelectedIndex = 0;

        _slangPreset.Items.Add("None");
        _slangPaths.Add(null);
        var root = Path.Combine(RetroArchDir, "shaders_slang");
        if (Directory.Exists(root))
        {
            foreach (var file in Directory.EnumerateFiles(root, "*.slangp", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                _slangPreset.Items.Add(Path.GetRelativePath(root, file));
                _slangPaths.Add(file);
            }
        }
        _slangPreset.SelectedIndex = 0;
    }

    private void BrowseSlang()
    {
        using var dialog = new OpenFileDialog { Filter = "Slang preset|*.slangp|All files|*.*", Title = "Choose RetroArch Slang preset" };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        _slangPreset.Items.Add("Custom: " + Path.GetFileName(dialog.FileName));
        _slangPaths.Add(dialog.FileName);
        _slangPreset.SelectedIndex = _slangPreset.Items.Count - 1;
    }

    private void PlayWithSlang()
    {
        if (string.IsNullOrWhiteSpace(_currentVideo) || !File.Exists(_currentVideo))
        {
            MessageBox.Show(this, "Open a video first.", "RetroVideo");
            return;
        }
        if (!File.Exists(RetroArchExe))
        {
            MessageBox.Show(this, "The RetroArch shader compatibility runtime is not installed in this build.", "Slang runtime missing");
            return;
        }

        string? shader = null;
        if (_slangPreset.SelectedIndex >= 0 && _slangPreset.SelectedIndex < _slangPaths.Count)
            shader = _slangPaths[_slangPreset.SelectedIndex];

        var settingsDir = Path.Combine(AppContext.BaseDirectory, "settings");
        Directory.CreateDirectory(settingsDir);
        var cfg = Path.Combine(settingsDir, "slang-compat.cfg");
        var sb = new StringBuilder();
        sb.AppendLine("builtin_mediaplayer_enable = \"true\"");
        sb.AppendLine("video_driver = \"vulkan\"");
        sb.AppendLine("pause_nonactive = \"false\"");
        if (!string.IsNullOrWhiteSpace(shader))
        {
            sb.AppendLine("video_shader_enable = \"true\"");
            sb.AppendLine($"video_shader = \"{shader.Replace('\\', '/').Replace("\"", "\\\"")}\"");
        }
        else sb.AppendLine("video_shader_enable = \"false\"");
        File.WriteAllText(cfg, sb.ToString(), new UTF8Encoding(false));

        _player.SetPause(true);
        var psi = new ProcessStartInfo { FileName = RetroArchExe, WorkingDirectory = RetroArchDir, UseShellExecute = false };
        psi.ArgumentList.Add("--config");
        psi.ArgumentList.Add(cfg);
        psi.ArgumentList.Add(_currentVideo);
        Process.Start(psi);
    }

    private void RefreshPlaybackUi()
    {
        if (!_player.IsReady) return;
        var position = _player.Position;
        var duration = _player.Duration;
        if (!_draggingSeek && duration > 0)
            _seek.Value = Math.Clamp((int)Math.Round(position / duration * _seek.Maximum), _seek.Minimum, _seek.Maximum);
        _time.Text = $"{FormatTime(position)} / {FormatTime(duration)}";
        _playPause.Text = _player.Paused ? "▶" : "⏸";
        _mute.Text = _player.Muted ? "🔇" : "🔊";
    }

    private void ToggleFullscreen()
    {
        if (!_fullscreenMode)
        {
            _windowedBounds = Bounds;
            _windowedBorder = FormBorderStyle;
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            _fullscreenMode = true;
        }
        else
        {
            WindowState = FormWindowState.Normal;
            FormBorderStyle = _windowedBorder;
            Bounds = _windowedBounds;
            _fullscreenMode = false;
        }
    }

    private static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) seconds = 0;
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1 ? ts.ToString(@"h\:mm\:ss") : ts.ToString(@"mm\:ss");
    }

    private void Safe(Action action)
    {
        try { action(); }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "RetroVideo", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }
}
