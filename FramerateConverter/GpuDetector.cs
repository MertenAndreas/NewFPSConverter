using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace NewFPSConverter;

/// <summary>
/// Information about a detected physical or hardware graphics card.
/// </summary>
public class GpuAdapterInfo
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
    public GpuDetector.GpuType Type { get; set; }
    public uint VendorId { get; set; }
    public ulong DedicatedVideoMemory { get; set; }
    public int VendorIndex { get; set; }

    public string FormattedMemory
    {
        get
        {
            if (DedicatedVideoMemory == 0) return "Shared memory";
            var mb = DedicatedVideoMemory / (1024 * 1024);
            return mb >= 1024
                ? $"{(mb / 1024.0).ToString("0.#", CultureInfo.InvariantCulture)} GB"
                : $"{mb} MB";
        }
    }

    public string VendorName => Type switch
    {
        GpuDetector.GpuType.Nvidia => "NVIDIA",
        GpuDetector.GpuType.Intel => "Intel",
        GpuDetector.GpuType.Amd => "AMD",
        _ => "Unknown"
    };

    public string EncoderTechnology => Type switch
    {
        GpuDetector.GpuType.Nvidia => "NVIDIA NVENC",
        GpuDetector.GpuType.Intel => "Intel Quick Sync",
        GpuDetector.GpuType.Amd => "AMD AMF",
        _ => "CPU"
    };

    public override string ToString() => $"[{Index}] {Name} ({VendorName}, {FormattedMemory})";
}

/// <summary>
/// Detects available GPU encoders (NVIDIA NVENC, AMD VCE/AMF, Intel QuickSync)
/// and physical graphics adapters via Windows DXGI.
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

    private static readonly object SyncLock = new();
    private static List<GpuAdapterInfo>? _cachedAdapters;

    #region DXGI Interop

    private const uint DXGI_ADAPTER_FLAG_SOFTWARE = 2;
    private const uint VENDOR_NVIDIA = 0x10DE;
    private const uint VENDOR_INTEL = 0x8086;
    private const uint VENDOR_AMD = 0x1002;
    private const uint VENDOR_MICROSOFT = 0x1414;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DXGI_ADAPTER_DESC1
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubSysId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public long AdapterLuid;
        public uint Flags;
    }

    [ComImport]
    [Guid("770aae78-f26f-4dba-a829-253c83d1b387")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIFactory1
    {
        [PreserveSig] int SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface([In] ref Guid Name, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent([In] ref Guid riid, out IntPtr ppParent);
        [PreserveSig] int EnumAdapters(uint Adapter, out IntPtr ppAdapter);
        [PreserveSig] int MakeWindowAssociation(IntPtr WindowHandle, uint Flags);
        [PreserveSig] int GetWindowAssociation(out IntPtr pWindowHandle);
        [PreserveSig] int CreateSwapChain(IntPtr pDevice, IntPtr pDesc, out IntPtr ppSwapChain);
        [PreserveSig] int CreateSoftwareAdapter(IntPtr Module, out IntPtr ppAdapter);
        [PreserveSig] int EnumAdapters1(uint Adapter, [MarshalAs(UnmanagedType.Interface)] out IDXGIAdapter1 ppAdapter);
        [PreserveSig] int IsCurrent();
    }

    [ComImport]
    [Guid("29038f61-3839-4626-91fd-086879011a05")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDXGIAdapter1
    {
        [PreserveSig] int SetPrivateData([In] ref Guid Name, uint DataSize, IntPtr pData);
        [PreserveSig] int SetPrivateDataInterface([In] ref Guid Name, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnknown);
        [PreserveSig] int GetPrivateData([In] ref Guid Name, ref uint pDataSize, IntPtr pData);
        [PreserveSig] int GetParent([In] ref Guid riid, out IntPtr ppParent);
        [PreserveSig] int EnumOutputs(uint Output, out IntPtr ppOutput);
        [PreserveSig] int GetDesc(IntPtr pDesc);
        [PreserveSig] int CheckInterfaceSupport([In] ref Guid InterfaceName, out long pUMDVersion);
        [PreserveSig] int GetDesc1(out DXGI_ADAPTER_DESC1 pDesc);
    }

    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1([In] ref Guid riid, [MarshalAs(UnmanagedType.Interface)] out IDXGIFactory1 ppFactory);

    #endregion

    /// <summary>
    /// Gets all detected physical hardware graphics cards installed in the system.
    /// </summary>
    public static IReadOnlyList<GpuAdapterInfo> GetAvailableAdapters(bool forceRefresh = false)
    {
        lock (SyncLock)
        {
            if (_cachedAdapters != null && !forceRefresh)
            {
                return _cachedAdapters;
            }

            var adapters = new List<GpuAdapterInfo>();

            try
            {
                var factoryGuid = typeof(IDXGIFactory1).GUID;
                if (CreateDXGIFactory1(ref factoryGuid, out var factory) == 0 && factory != null)
                {
                    try
                    {
                        uint index = 0;
                        var vendorCounts = new Dictionary<GpuType, int>();

                        while (factory.EnumAdapters1(index, out var adapter) == 0 && adapter != null)
                        {
                            try
                            {
                                if (adapter.GetDesc1(out var desc) == 0)
                                {
                                    // Skip software rasterizers (like Microsoft Basic Render Driver)
                                    var isSoftware = (desc.Flags & DXGI_ADAPTER_FLAG_SOFTWARE) != 0 || desc.VendorId == VENDOR_MICROSOFT;
                                    if (!isSoftware)
                                    {
                                        var type = desc.VendorId switch
                                        {
                                            VENDOR_NVIDIA => GpuType.Nvidia,
                                            VENDOR_INTEL => GpuType.Intel,
                                            VENDOR_AMD => GpuType.Amd,
                                            _ => GpuType.None
                                        };

                                        var vendorIndex = 0;
                                        if (vendorCounts.TryGetValue(type, out var count))
                                        {
                                            vendorIndex = count;
                                            vendorCounts[type] = count + 1;
                                        }
                                        else
                                        {
                                            vendorCounts[type] = 1;
                                        }

                                        adapters.Add(new GpuAdapterInfo
                                        {
                                            Index = (int)index,
                                            Name = desc.Description.Trim(),
                                            Type = type,
                                            VendorId = desc.VendorId,
                                            DedicatedVideoMemory = desc.DedicatedVideoMemory.ToUInt64(),
                                            VendorIndex = vendorIndex
                                        });
                                    }
                                }
                            }
                            finally
                            {
                                Marshal.ReleaseComObject(adapter);
                            }

                            index++;
                        }
                    }
                    finally
                    {
                        Marshal.ReleaseComObject(factory);
                    }
                }
            }
            catch
            {
                // Fallback to empty if DXGI is unavailable
            }

            _cachedAdapters = adapters;
            return _cachedAdapters;
        }
    }

    /// <summary>
    /// Prints all detected graphics adapters and usage instructions to the specified writer (or Console.Out).
    /// </summary>
    public static void PrintAvailableGpus(TextWriter? writer = null)
    {
        var outWriter = writer ?? Console.Out;
        var adapters = GetAvailableAdapters();

        outWriter.WriteLine("Available graphics cards (GPUs):");
        if (adapters.Count == 0)
        {
            outWriter.WriteLine("  No dedicated hardware graphics adapters detected via DXGI.");
        }
        else
        {
            foreach (var adapter in adapters)
            {
                outWriter.WriteLine($"  [{adapter.Index}] {adapter.Name} (Vendor: {adapter.VendorName}, Technology: {adapter.EncoderTechnology}, Memory: {adapter.FormattedMemory})");
            }
        }

        outWriter.WriteLine();
        outWriter.WriteLine("Usage with --gpu (-g):");
        outWriter.WriteLine("  --gpu auto             Auto-detect fastest available GPU (Default)");
        if (adapters.Count > 0)
        {
            foreach (var adapter in adapters)
            {
                outWriter.WriteLine($"  --gpu {adapter.Index}                Select [{adapter.Index}] {adapter.Name}");
            }
        }
        outWriter.WriteLine("  --gpu intel            Select first Intel GPU (Intel Quick Sync)");
        outWriter.WriteLine("  --gpu nvidia           Select first NVIDIA GPU (NVIDIA NVENC)");
        outWriter.WriteLine("  --gpu amd              Select first AMD GPU (AMD AMF)");
        outWriter.WriteLine("  --gpu none             Use CPU only (Software encoder libx264)");
    }

    /// <summary>
    /// Resolves the user's preference to a concrete GPU type and optional adapter info.
    /// Supports "auto", "none", numeric adapter index ("0", "1"), vendor names ("nvidia", "intel", "amd"),
    /// or partial device name matching (e.g. "iris", "rtx", "geforce").
    /// </summary>
    public static (GpuType Type, GpuAdapterInfo? Adapter) ResolveGpu(string? gpuPreference, string ffmpegPath = "ffmpeg")
    {
        if (string.IsNullOrWhiteSpace(gpuPreference) || gpuPreference.Equals("auto", StringComparison.OrdinalIgnoreCase))
        {
            var adapters = GetAvailableAdapters();
            if (adapters.Count > 0)
            {
                // Auto priority: NVIDIA > AMD > Intel
                var nvidia = adapters.FirstOrDefault(a => a.Type == GpuType.Nvidia);
                if (nvidia != null) return (GpuType.Nvidia, nvidia);

                var amd = adapters.FirstOrDefault(a => a.Type == GpuType.Amd);
                if (amd != null) return (GpuType.Amd, amd);

                var intel = adapters.FirstOrDefault(a => a.Type == GpuType.Intel);
                if (intel != null) return (GpuType.Intel, intel);

                var first = adapters.First();
                return (first.Type, first);
            }

            // Fallback to legacy process-based detection if DXGI found nothing
            if (HasNvidiaGpu()) return (GpuType.Nvidia, null);
            if (HasAmdGpu(ffmpegPath)) return (GpuType.Amd, null);
            if (HasIntelGpu(ffmpegPath)) return (GpuType.Intel, null);

            return (GpuType.None, null);
        }

        if (gpuPreference.Equals("none", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("cpu", StringComparison.OrdinalIgnoreCase))
        {
            return (GpuType.None, null);
        }

        var allAdapters = GetAvailableAdapters();

        // 1. Try parsing as numeric adapter index (e.g. "0", "1")
        if (int.TryParse(gpuPreference, NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetIndex))
        {
            var matchedByIndex = allAdapters.FirstOrDefault(a => a.Index == targetIndex);
            if (matchedByIndex != null)
            {
                return (matchedByIndex.Type, matchedByIndex);
            }
        }

        // 2. Try vendor matching (nvidia, intel, amd)
        if (gpuPreference.Equals("nvidia", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("nvenc", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("cuda", StringComparison.OrdinalIgnoreCase))
        {
            var nvidia = allAdapters.FirstOrDefault(a => a.Type == GpuType.Nvidia);
            if (nvidia != null) return (GpuType.Nvidia, nvidia);
            return HasNvidiaGpu() ? (GpuType.Nvidia, null) : (GpuType.None, null);
        }

        if (gpuPreference.Equals("intel", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("qsv", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("quicksync", StringComparison.OrdinalIgnoreCase))
        {
            var intel = allAdapters.FirstOrDefault(a => a.Type == GpuType.Intel);
            if (intel != null) return (GpuType.Intel, intel);
            return HasIntelGpu(ffmpegPath) ? (GpuType.Intel, null) : (GpuType.None, null);
        }

        if (gpuPreference.Equals("amd", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("amf", StringComparison.OrdinalIgnoreCase) ||
            gpuPreference.Equals("vce", StringComparison.OrdinalIgnoreCase))
        {
            var amd = allAdapters.FirstOrDefault(a => a.Type == GpuType.Amd);
            if (amd != null) return (GpuType.Amd, amd);
            return HasAmdGpu(ffmpegPath) ? (GpuType.Amd, null) : (GpuType.None, null);
        }

        // 3. Try partial name matching (e.g. "iris", "rtx", "geforce", "t600")
        var matchedByName = allAdapters.FirstOrDefault(a => a.Name.Contains(gpuPreference, StringComparison.OrdinalIgnoreCase));
        if (matchedByName != null)
        {
            return (matchedByName.Type, matchedByName);
        }

        // Default fallback: auto
        return ResolveGpu("auto", ffmpegPath);
    }

    /// <summary>
    /// Detects the best available GPU or CPU based on user preference.
    /// Preserved for backward compatibility.
    /// </summary>
    public static GpuType DetectGpu(string gpuPreference)
    {
        return DetectGpu(gpuPreference, "ffmpeg");
    }

    /// <summary>
    /// Detects the best available GPU or CPU based on user preference.
    /// Preserved for backward compatibility.
    /// </summary>
    public static GpuType DetectGpu(string gpuPreference, string ffmpegPath)
    {
        return ResolveGpu(gpuPreference, ffmpegPath).Type;
    }

    private static bool HasNvidiaGpu()
    {
        try
        {
            var process = new ProcessStartInfo("nvidia-smi")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(process);
            if (proc == null) return false;
            proc.WaitForExit(3000);
            return proc.ExitCode == 0;
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
            var process = new ProcessStartInfo(ffmpegPath, "-encoders")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(process);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return output.Contains("hevc_amf") || output.Contains("h264_amf");
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
            var process = new ProcessStartInfo(ffmpegPath, "-encoders")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = Process.Start(process);
            if (proc == null) return false;
            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(5000);
            return output.Contains("hevc_qsv") || output.Contains("h264_qsv");
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
    public static string GetGpuParams(GpuType gpuType, GpuAdapterInfo? adapter = null)
    {
        var deviceIdx = adapter?.VendorIndex ?? 0;
        return gpuType switch
        {
            GpuType.Nvidia => $"-hwaccel cuda -hwaccel_device {deviceIdx}",
            GpuType.Amd => "-hwaccel auto",
            GpuType.Intel => $"-hwaccel auto -hwaccel_device {deviceIdx}",
            _ => ""
        };
    }
}
