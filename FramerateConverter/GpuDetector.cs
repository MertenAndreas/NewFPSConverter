using System.Diagnostics;

namespace NewFPSConverter;

/// <summary>
/// Detects available GPU encoders (NVIDIA NVENC, AMD VCE, Intel QuickSync)
/// </summary>
public static class GpuDetector
{
    public enum GpuType
    {
        None,
        Nvidia,
        Amd,
        Intel
    }

    /// <summary>
    /// Detects the best available GPU or CPU based on user preference
    /// </summary>
    public static GpuType DetectGpu(string gpuPreference)
    {
        return DetectGpu(gpuPreference, "ffmpeg");
    }

    public static GpuType DetectGpu(string gpuPreference, string ffmpegPath)
    {
        if (gpuPreference.Equals("none", StringComparison.OrdinalIgnoreCase))
            return GpuType.None;

        if (gpuPreference.Equals("nvidia", StringComparison.OrdinalIgnoreCase))
            return HasNvidiaGpu() ? GpuType.Nvidia : GpuType.None;

        if (gpuPreference.Equals("amd", StringComparison.OrdinalIgnoreCase))
            return HasAmdGpu(ffmpegPath) ? GpuType.Amd : GpuType.None;

        if (gpuPreference.Equals("intel", StringComparison.OrdinalIgnoreCase))
            return HasIntelGpu(ffmpegPath) ? GpuType.Intel : GpuType.None;

        // Auto-detect: try in order of performance
        if (HasNvidiaGpu()) return GpuType.Nvidia;
        if (HasAmdGpu(ffmpegPath)) return GpuType.Amd;
        if (HasIntelGpu(ffmpegPath)) return GpuType.Intel;

        return GpuType.None;
    }

    private static bool HasNvidiaGpu()
    {
        try
        {
            // Try to run nvidia-smi
            var process = new ProcessStartInfo("nvidia-smi")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(process))
            {
                if (proc == null) return false;
                proc.WaitForExit(3000);
                return proc.ExitCode == 0;
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool HasAmdGpu(string ffmpegPath)
    {
        try
        {
            // Try to find AMD-related DLLs or VCE support via ffmpeg
            var process = new ProcessStartInfo(ffmpegPath, "-encoders")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(process))
            {
                if (proc == null) return false;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);
                return output.Contains("hevc_amf") || output.Contains("h264_amf");
            }
        }
        catch
        {
            return false;
        }
    }

    private static bool HasIntelGpu(string ffmpegPath)
    {
        try
        {
            // Check for Intel QSV support in ffmpeg
            var process = new ProcessStartInfo(ffmpegPath, "-encoders")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var proc = Process.Start(process))
            {
                if (proc == null) return false;
                var output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit(5000);
                return output.Contains("hevc_qsv") || output.Contains("h264_qsv");
            }
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Returns the ffmpeg encoder name for the detected GPU
    /// </summary>
    public static string GetEncoderName(GpuType gpuType, string defaultCodec = "libx264")
    {
        return gpuType switch
        {
            GpuType.Nvidia => defaultCodec.Contains("h264") ? "h264_nvenc" : "hevc_nvenc",
            GpuType.Amd => defaultCodec.Contains("h264") ? "h264_amf" : "hevc_amf",
            GpuType.Intel => defaultCodec.Contains("h264") ? "h264_qsv" : "hevc_qsv",
            _ => defaultCodec
        };
    }

    /// <summary>
    /// Gets GPU-specific ffmpeg parameters
    /// </summary>
    public static string GetGpuParams(GpuType gpuType)
    {
        return gpuType switch
        {
            GpuType.Nvidia => "-hwaccel cuda -hwaccel_output_format cuda",
            GpuType.Amd => "-hwaccel auto",
            GpuType.Intel => "-hwaccel auto -hwaccel_device 0",
            _ => ""
        };
    }
}
