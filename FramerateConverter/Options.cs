using CommandLine;

namespace NewFPSConverter;

public class Options
{
    [Option('b', "batch", Required = false, HelpText = "Enable batch behavior: Run once, then terminate automatically.")]
    public bool BatchMode { get; set; }

    [Option('s', "source", Required = false, HelpText = "The input video.")]
    public string? SourceFile { get; set; }

    [Option('d', "source-dir", Required = false, HelpText = "Source directory for batch processing multiple files.")]
    public string? SourceDirectory { get; set; }

    [Option('t', "target", Required = false, HelpText = "The output video.")]
    public string? TargetFile { get; set; }

    [Option('O', "target-dir", Required = false, HelpText = "Target directory for batch output files.")]
    public string? TargetDirectory { get; set; }

    [Option('k', "key-frames-only", Required = false, HelpText = "Enables Key-Frame-Only videos.")]
    public bool KeyFramesOnly { get; set; }

    [Option('f', "frames-per-second", Required = false, HelpText = "The target FPS.")]
    public double? FramesPerSecond { get; set; }

    [Option('n', "no-flicker", Required = false, HelpText = "Reduce flicker by adjusting frame rate.")]
    public bool NoFlicker { get; set; }

    [Option('m', "min-frame-rate", Required = false, HelpText = "The supplied frame rate is a minimum.")]
    public bool MinFrameRate { get; set; }

    [Option('o', "overwrite", Required = false, HelpText = "If output video exists, it will be overwritten.")]
    public bool Overwrite { get; set; }

    [Option('a', "audio", Required = false, HelpText = "Keep audio stream(s) if any.")]
    public bool KeepAudio { get; set; }

    [Option('l', "sl-compatibility", Required = false, HelpText = "Enable Sympholight compatible H.264 output settings.")]
    public bool SlCompatibility { get; set; }

    [Option('p', "gpu-mediapackage", Required = false, HelpText = "Allow GPU encoding for MediaPackage or raw H.264 batch output.")]
    public bool GpuMediaPackage { get; set; }

    [Option('W', "force-width", Required = false, HelpText = "Enforces target width.")]
    public int ForceWidth { get; set; }

    [Option('H', "force-height", Required = false, HelpText = "Enforces target height.")]
    public int ForceHeight { get; set; }

    [Option('r', "bitrate", Required = false, HelpText = "Target bit rate in KBits/sec.")]
    public int BitrateKBits { get; set; }

    [Option('g', "gpu", Required = false, Default = "auto", HelpText = "GPU device or type: 'auto', 'none', vendor ('nvidia', 'intel', 'amd'), GPU index (e.g. '0', '1'), or device name.")]
    public string GpuType { get; set; } = "auto";

    [Option("list-gpus", Required = false, HelpText = "List all detected hardware graphics cards (GPUs) and exit.")]
    public bool ListGpus { get; set; }

    [Option('j', "parallel-jobs", Required = false, Default = 0, HelpText = "Number of parallel ffmpeg processes (0 = auto).")]
    public int ParallelJobs { get; set; } = 0;

    [Option("no-gui", Required = false, HelpText = "Run task in background without displaying the graphical user interface (GUI).")]
    public bool NoGui { get; set; }

    [Option("headless", Required = false, Hidden = true, HelpText = "Alias for --no-gui.")]
    public bool Headless { get => NoGui; set => NoGui = value; }

    [Option("background", Required = false, Hidden = true, HelpText = "Alias for --no-gui.")]
    public bool Background { get => NoGui; set => NoGui = value; }
}
