using System.Globalization;
using System.Runtime.InteropServices;

namespace RetroVideo.Win;

internal sealed class MpvPlayer : IDisposable
{
    private IntPtr _handle;
    private bool _initialized;

    public bool IsReady => _initialized && _handle != IntPtr.Zero;

    public void Initialize(IntPtr windowHandle)
    {
        if (IsReady)
            return;

        _handle = Native.mpv_create();
        if (_handle == IntPtr.Zero)
            throw new InvalidOperationException("Could not create the MPV playback engine.");

        var hwnd = unchecked((uint)windowHandle.ToInt64());
        SetOption("wid", hwnd.ToString(CultureInfo.InvariantCulture));
        SetOption("vo", "gpu-next");
        SetOption("gpu-api", "d3d11");
        SetOption("hwdec", "auto-safe");
        SetOption("keep-open", "yes");
        SetOption("osc", "no");
        SetOption("input-default-bindings", "no");
        SetOption("input-vo-keyboard", "no");
        SetOption("sub-auto", "fuzzy");
        SetOption("audio-file-auto", "fuzzy");
        SetOption("volume-max", "130");
        SetOption("audio-channels", "auto-safe");

        var result = Native.mpv_initialize(_handle);
        if (result < 0)
        {
            Dispose();
            throw new InvalidOperationException($"MPV initialization failed ({result}).");
        }

        _initialized = true;
    }

    public void LoadFile(string path)
    {
        EnsureReady();
        Command("loadfile", path, "replace");
    }

    public void TogglePause() => Set("pause", GetBool("pause") ? "no" : "yes");
    public void SetPause(bool paused) => Set("pause", paused ? "yes" : "no");
    public void SeekRelative(double seconds) => Command("seek", seconds.ToString(CultureInfo.InvariantCulture), "relative+exact");
    public void SeekAbsolute(double seconds) => Command("seek", Math.Max(0, seconds).ToString(CultureInfo.InvariantCulture), "absolute+exact");
    public void SetVolume(int value) => Set("volume", Math.Clamp(value, 0, 130).ToString(CultureInfo.InvariantCulture));
    public void SetMute(bool muted) => Set("mute", muted ? "yes" : "no");
    public void SelectAudio(long id) => Set("aid", id.ToString(CultureInfo.InvariantCulture));
    public void SelectSubtitle(long? id) => Set("sid", id.HasValue ? id.Value.ToString(CultureInfo.InvariantCulture) : "no");
    public void AddSubtitle(string path) => Command("sub-add", path, "select");

    public double Position => GetDouble("time-pos");
    public double Duration => GetDouble("duration");
    public bool Paused => GetBool("pause");
    public bool Muted => GetBool("mute");
    public int Volume => (int)Math.Round(GetDouble("volume"));

    public IReadOnlyList<TrackInfo> GetTracks()
    {
        var tracks = new List<TrackInfo>();
        if (!IsReady)
            return tracks;

        var count = GetInt64("track-list/count");
        for (var i = 0L; i < count; i++)
        {
            var prefix = $"track-list/{i}";
            var type = GetString($"{prefix}/type");
            if (string.IsNullOrWhiteSpace(type))
                continue;

            var id = GetInt64($"{prefix}/id");
            var title = GetString($"{prefix}/title");
            var language = GetString($"{prefix}/lang");
            var selected = GetBool($"{prefix}/selected");
            var external = GetBool($"{prefix}/external");
            tracks.Add(new TrackInfo(id, type, title, language, selected, external));
        }

        return tracks;
    }

    public void ApplyNativeFilter(string preset)
    {
        EnsureReady();

        // Reset our native filter controls first so presets are deterministic.
        Set("deband", "no");
        Set("deinterlace", "no");
        Set("sigmoid-upscaling", "no");
        Set("correct-downscaling", "yes");
        Set("scale", "bilinear");
        Set("cscale", "bilinear");
        Set("dscale", "bilinear");

        switch (preset)
        {
            case "Soft upscale":
                Set("scale", "spline36");
                Set("cscale", "spline36");
                Set("dscale", "mitchell");
                break;
            case "Sharp upscale":
                Set("scale", "ewa_lanczossharp");
                Set("cscale", "ewa_lanczossoft");
                Set("dscale", "mitchell");
                Set("sigmoid-upscaling", "yes");
                break;
            case "Clean / deband":
                Set("scale", "spline36");
                Set("cscale", "spline36");
                Set("deband", "yes");
                break;
            case "Deinterlace":
                Set("deinterlace", "yes");
                break;
            case "Pixel / nearest":
                Set("scale", "nearest");
                Set("cscale", "nearest");
                Set("dscale", "nearest");
                break;
        }
    }

    public void SetPicture(string property, int value)
    {
        if (property is not ("brightness" or "contrast" or "saturation" or "gamma"))
            throw new ArgumentOutOfRangeException(nameof(property));
        Set(property, Math.Clamp(value, -100, 100).ToString(CultureInfo.InvariantCulture));
    }

    private void SetOption(string name, string value)
    {
        var result = Native.mpv_set_option_string(_handle, name, value);
        if (result < 0)
            throw new InvalidOperationException($"Could not set MPV option '{name}' ({result}).");
    }

    private void Set(string property, string value) => Command("set", property, value);

    private void Command(params string[] args)
    {
        EnsureReady();
        var ptrs = new IntPtr[args.Length + 1];
        var argv = IntPtr.Zero;
        try
        {
            for (var i = 0; i < args.Length; i++)
                ptrs[i] = Marshal.StringToCoTaskMemUTF8(args[i]);

            argv = Marshal.AllocHGlobal(IntPtr.Size * ptrs.Length);
            for (var i = 0; i < ptrs.Length; i++)
                Marshal.WriteIntPtr(argv, i * IntPtr.Size, ptrs[i]);

            var result = Native.mpv_command(_handle, argv);
            if (result < 0)
                throw new InvalidOperationException($"MPV command failed ({result}): {string.Join(' ', args)}");
        }
        finally
        {
            if (argv != IntPtr.Zero)
                Marshal.FreeHGlobal(argv);
            foreach (var ptr in ptrs)
            {
                if (ptr != IntPtr.Zero)
                    Marshal.FreeCoTaskMem(ptr);
            }
        }
    }

    private string GetString(string property)
    {
        if (!IsReady)
            return string.Empty;
        var ptr = Native.mpv_get_property_string(_handle, property);
        if (ptr == IntPtr.Zero)
            return string.Empty;
        try
        {
            return Marshal.PtrToStringUTF8(ptr) ?? string.Empty;
        }
        finally
        {
            Native.mpv_free(ptr);
        }
    }

    private double GetDouble(string property)
    {
        if (!IsReady)
            return 0;
        var mem = Marshal.AllocHGlobal(sizeof(double));
        try
        {
            var result = Native.mpv_get_property(_handle, property, MpvFormat.Double, mem);
            return result < 0 ? 0 : Marshal.PtrToStructure<double>(mem);
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }

    private long GetInt64(string property)
    {
        if (!IsReady)
            return 0;
        var mem = Marshal.AllocHGlobal(sizeof(long));
        try
        {
            var result = Native.mpv_get_property(_handle, property, MpvFormat.Int64, mem);
            return result < 0 ? 0 : Marshal.ReadInt64(mem);
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }

    private bool GetBool(string property)
    {
        if (!IsReady)
            return false;
        var mem = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            var result = Native.mpv_get_property(_handle, property, MpvFormat.Flag, mem);
            return result >= 0 && Marshal.ReadInt32(mem) != 0;
        }
        finally
        {
            Marshal.FreeHGlobal(mem);
        }
    }

    private void EnsureReady()
    {
        if (!IsReady)
            throw new InvalidOperationException("The MPV engine is not initialized.");
    }

    public void Dispose()
    {
        _initialized = false;
        if (_handle != IntPtr.Zero)
        {
            Native.mpv_terminate_destroy(_handle);
            _handle = IntPtr.Zero;
        }
    }

    internal sealed record TrackInfo(long Id, string Type, string Title, string Language, bool Selected, bool External)
    {
        public string DisplayName
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(Language)) parts.Add(Language);
                if (!string.IsNullOrWhiteSpace(Title)) parts.Add(Title);
                if (External) parts.Add("external");
                var suffix = parts.Count == 0 ? string.Empty : " - " + string.Join(" / ", parts);
                return $"Track {Id}{suffix}";
            }
        }
    }

    private enum MpvFormat
    {
        None = 0,
        String = 1,
        OsdString = 2,
        Flag = 3,
        Int64 = 4,
        Double = 5,
        Node = 6,
        NodeArray = 7,
        NodeMap = 8,
        ByteArray = 9
    }

    private static class Native
    {
        private const string Dll = "libmpv-2.dll";

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr mpv_create();

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_initialize(IntPtr ctx);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int mpv_set_option_string(IntPtr ctx, string name, string data);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int mpv_command(IntPtr ctx, IntPtr args);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern IntPtr mpv_get_property_string(IntPtr ctx, string name);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        internal static extern int mpv_get_property(IntPtr ctx, string name, MpvFormat format, IntPtr data);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_free(IntPtr data);

        [DllImport(Dll, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void mpv_terminate_destroy(IntPtr ctx);
    }
}
