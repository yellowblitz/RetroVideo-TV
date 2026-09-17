using System.Diagnostics;
using System.Text;

namespace RetroVideo.Win;

public sealed class MainForm : Form
{
    private readonly TextBox _videoPath = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _shaderBox = new() { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly CheckBox _fullscreen = new() { Text = "Start fullscreen", Checked = true, AutoSize = true };
    private readonly Label _status = new() { Text = "Ready", AutoSize = true };
    private readonly List<string?> _shaderPaths = new();

    private string BaseDir => AppContext.BaseDirectory;
    private string RuntimeDir => Path.Combine(BaseDir, "runtime");
    private string RetroArchExe => Path.Combine(RuntimeDir, "retroarch.exe");

    public MainForm(string? initialVideo)
    {
        Text = "RetroVideo for Windows";
        Width = 820;
        Height = 360;
        MinimumSize = new Size(680, 300);
        StartPosition = FormStartPosition.CenterScreen;
        AllowDrop = true;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(18),
            ColumnCount = 3,
            RowCount = 7
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        root.Controls.Add(new Label { Text = "Video", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        root.Controls.Add(_videoPath, 1, 0);
        var browseVideo = new Button { Text = "Browse...", Dock = DockStyle.Fill };
        browseVideo.Click += (_, _) => BrowseVideo();
        root.Controls.Add(browseVideo, 2, 0);

        root.Controls.Add(new Label { Text = "Shader preset", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        root.Controls.Add(_shaderBox, 1, 1);
        var browseShader = new Button { Text = "Custom...", Dock = DockStyle.Fill };
        browseShader.Click += (_, _) => BrowseShader();
        root.Controls.Add(browseShader, 2, 1);

        root.Controls.Add(_fullscreen, 1, 2);

        var hint = new Label
        {
            Text = "Common formats: MP4, MKV, AVI, WMV, MOV, WebM, MPEG/MPG, TS/MTS/M2TS, FLV, OGV. Drag a video onto this window to select it.",
            AutoSize = true,
            MaximumSize = new Size(650, 0)
        };
        root.SetColumnSpan(hint, 3);
        root.Controls.Add(hint, 0, 3);

        var controlsHint = new Label
        {
            Text = "Playback: P = pause/resume, F1 = RetroArch quick menu/shader controls, Esc = exit player.",
            AutoSize = true,
            MaximumSize = new Size(650, 0)
        };
        root.SetColumnSpan(controlsHint, 3);
        root.Controls.Add(controlsHint, 0, 4);

        var play = new Button
        {
            Text = "PLAY VIDEO",
            Height = 42,
            Dock = DockStyle.Fill
        };
        play.Click += (_, _) => PlayVideo();
        root.SetColumnSpan(play, 3);
        root.Controls.Add(play, 0, 5);

        root.SetColumnSpan(_status, 3);
        root.Controls.Add(_status, 0, 6);

        Controls.Add(root);

        DragEnter += (_, e) =>
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        };
        DragDrop += (_, e) =>
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                _videoPath.Text = files[0];
        };

        LoadShaders();

        if (!string.IsNullOrWhiteSpace(initialVideo) && File.Exists(initialVideo))
            _videoPath.Text = initialVideo;
    }

    private void BrowseVideo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Open video",
            Filter = "Video files|*.mp4;*.mkv;*.avi;*.wmv;*.mov;*.webm;*.m4v;*.mpg;*.mpeg;*.ts;*.mts;*.m2ts;*.flv;*.ogv|All files|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            _videoPath.Text = dialog.FileName;
    }

    private void BrowseShader()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose RetroArch Slang preset",
            Filter = "Slang presets|*.slangp|All files|*.*"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        _shaderPaths.Add(dialog.FileName);
        _shaderBox.Items.Add("Custom: " + Path.GetFileName(dialog.FileName));
        _shaderBox.SelectedIndex = _shaderBox.Items.Count - 1;
    }

    private void LoadShaders()
    {
        _shaderBox.Items.Clear();
        _shaderPaths.Clear();
        _shaderBox.Items.Add("None");
        _shaderPaths.Add(null);

        var shaderRoot = Path.Combine(RuntimeDir, "shaders_slang");
        if (!Directory.Exists(shaderRoot))
        {
            _shaderBox.SelectedIndex = 0;
            return;
        }

        try
        {
            foreach (var file in Directory.EnumerateFiles(shaderRoot, "*.slangp", SearchOption.AllDirectories)
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var relative = Path.GetRelativePath(shaderRoot, file);
                _shaderBox.Items.Add(relative);
                _shaderPaths.Add(file);
            }
        }
        catch (Exception ex)
        {
            _status.Text = "Shader scan warning: " + ex.Message;
        }

        _shaderBox.SelectedIndex = 0;
    }

    private void PlayVideo()
    {
        var video = _videoPath.Text.Trim();
        if (!File.Exists(video))
        {
            MessageBox.Show(this, "Choose a valid video file first.", "RetroVideo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(RetroArchExe))
        {
            MessageBox.Show(this,
                "The bundled RetroArch runtime is missing. Please run RetroVideo from the complete extracted ZIP.",
                "Runtime missing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        string? shader = null;
        if (_shaderBox.SelectedIndex >= 0 && _shaderBox.SelectedIndex < _shaderPaths.Count)
            shader = _shaderPaths[_shaderBox.SelectedIndex];

        var configDir = Path.Combine(BaseDir, "settings");
        Directory.CreateDirectory(configDir);
        var configPath = Path.Combine(configDir, "retrovideo.cfg");
        File.WriteAllText(configPath, BuildConfig(shader), Encoding.UTF8);

        var psi = new ProcessStartInfo
        {
            FileName = RetroArchExe,
            WorkingDirectory = RuntimeDir,
            UseShellExecute = false,
            Arguments = $"--config {Quote(configPath)} {Quote(video)}"
        };

        try
        {
            Process.Start(psi);
            _status.Text = "Playing: " + Path.GetFileName(video);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not start player", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private string BuildConfig(string? shader)
    {
        var sb = new StringBuilder();
        sb.AppendLine("builtin_mediaplayer_enable = \"true\"");
        sb.AppendLine("video_driver = \"vulkan\"");
        sb.AppendLine("menu_driver = \"ozone\"");
        sb.AppendLine($"video_fullscreen = \"{(_fullscreen.Checked ? "true" : "false")}\"");
        sb.AppendLine("config_save_on_exit = \"false\"");
        sb.AppendLine("pause_nonactive = \"false\"");
        sb.AppendLine("input_pause_toggle = \"p\"");

        if (!string.IsNullOrWhiteSpace(shader))
        {
            var normalized = shader.Replace('\\', '/');
            sb.AppendLine("video_shader_enable = \"true\"");
            sb.AppendLine($"video_shader = \"{normalized.Replace("\"", "\\\"")}\"");
        }
        else
        {
            sb.AppendLine("video_shader_enable = \"false\"");
        }

        return sb.ToString();
    }

    private static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";
}
