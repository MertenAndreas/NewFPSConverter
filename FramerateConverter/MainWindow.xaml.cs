using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using CommandLine;
using Microsoft.Win32;

namespace NewFPSConverter;

/// <summary>
    ///     Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Wpf.Ui.Controls.FluentWindow, IDisposable
    {
        /// <summary>
        /// The file extension used for compressed container format
        /// </summary>
        private const string CompressedFileExtension = ".mpkg";

        /// <summary>
        ///     The executable FFprobe.exe
        /// </summary>
        private const string ExecutableFFprobe = @"tools\FFprobe.exe";

        /// <summary>
        ///     The executable FFmpeg.exe
        /// </summary>
        private const string ExecutableFFmpeg = @"tools\FFmpeg.exe";

        /// <summary>
        ///     The executable FFprobe stream info parameters
        /// </summary>
        private const string ExecutableFFprobeStreamInfoParameters = @" ""{0}"" -v 0 -select_streams v:0 -print_format flat -show_entries stream=r_frame_rate,avg_frame_rate,width,height,duration:format=duration";

        /// <summary>
        ///     The executable FFprobe frames info parameters
        /// </summary>
        private const string ExecutableFFprobeFramesInfoParameters = @" -show_frames ""{0}"" -v 0 -select_streams v -print_format flat -show_entries frame=pkt_size";

        /// <summary>
        ///     The FFprobe output width
        /// </summary>
        private const string FFprobeOutputWidth = @"streams.stream.0.width=";

        /// <summary>
        ///     The FFprobe output height
        /// </summary>
        private const string FFprobeOutputHeight = @"streams.stream.0.height=";

        /// <summary>
        ///     The FFprobe output duration
        /// </summary>
        private const string FFprobeOutputDuration = @"streams.stream.0.duration=""";

        private const string FFprobeOutputFormatDuration = @"format.duration=""";

        /// <summary>
        ///     The FFprobe output framerate
        /// </summary>
        private const string FFprobeOutputFramerate = @"streams.stream.0.r_frame_rate=""";

        private const string FFprobeOutputAverageFramerate = @"streams.stream.0.avg_frame_rate=""";

        /// <summary>
        ///     The FFmpeg converter parameters for IVF (only Key Frames)
        /// </summary>
        private const string ExecutableFFmpegParametersOnlyKeyFramesIvf = @" -y -i ""{0}"" -c:v libvpx -b:v {2}K -g 1 -keyint_min 0 -sc_threshold 1 ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters for IVF
        /// </summary>
        private const string ExecutableFFmpegParametersStandardIvf = @" -y -i ""{0}"" -c:v libvpx -b:v {2}K ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters for H.264 (only Key Frames)
        /// </summary>
        private const string ExecutableFFmpegParametersOnlyKeyFramesH264 = @" -y -i ""{0}"" -tune zerolatency -x264opts bitrate={2}:vbv-maxrate={2}:vbv-bufsize=1000:slices=1:ref=1:no-scenecut:nal-hrd=cbr -g 1 -keyint_min 0 -c:v libx264 ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters for H.264 (only Key Frames with forced size)
        /// </summary>
        private const string ExecutableFFmpegParametersOnlyKeyFramesH264WithForcedSize = @" -y -i ""{0}"" -filter_complex ""nullsrc=size={3}x{4},lutrgb=0:0:0,fps=fps={5} [base]; [0:v] setpts=PTS-STARTPTS,scale=w='min({3}\,iw):h=min({4}\,ih):force_original_aspect_ratio=decrease' [upperleft]; [base][upperleft] overlay=shortest=1"" -tune zerolatency -x264opts bitrate={2}:vbv-maxrate={2}:vbv-bufsize=1000:slices=1:ref=1:no-scenecut:nal-hrd=cbr -g 1 -keyint_min 0 -c:v libx264 ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters for H.264
        /// </summary>
        private const string ExecutableFFmpegParametersStandardH264 = @" -y -i ""{0}"" -c:v libx264 -b:v {2}K ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters for H.264 (with forced size)
        /// </summary>
        private const string ExecutableFFmpegParametersStandardH264WithForcedSize = @" -y -i ""{0}"" -filter_complex ""nullsrc=size={3}x{4},lutrgb=0:0:0,fps=fps={5} [base]; [0:v] setpts=PTS-STARTPTS,scale=w='min({3}\,iw):h=min({4}\,ih):force_original_aspect_ratio=decrease' [upperleft]; [base][upperleft] overlay=shortest=1"" -c:v libx264 -b:v {2}K ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters to extract an audio stream
        /// </summary>
        private const string ExecutableFFmpegParametersExtractAudio = @" -y -i ""{0}"" -vn -c:a libvorbis ""{1}""";

        /// <summary>
        ///     The FFmpeg converter parameters to mux video+audio to output file
        /// </summary>
        private const string ExecutableFFmpegParametersMuxVideoAndAudio = @" -y -i ""{0}"" -i ""{1}"" -c copy ""{2}""";

        /// <summary>
        /// File dialog filter
        /// </summary>
        private static readonly string MediaFilter = $"Video Files|*.mpg;*.avi;*.wmv;*.mov;*.mp4;*.h264;*.mkv;*.ivf;*.webm|Media Package|*.{CompressedFileExtension.TrimStart('.')}|All Files|*.*";

        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".avi",
            ".flv",
            ".h264",
            ".ivf",
            ".m3u8",
            ".mkv",
            ".mov",
            ".mp4",
            ".mpg",
            ".webm",
            ".wmv"
        };

        /// <summary>
        ///     The bit rate property
        /// </summary>
        public static readonly DependencyProperty BitRateProperty = DependencyProperty.Register("BitRate", typeof(int), typeof(MainWindow), new PropertyMetadata(0));

        /// <summary>
        ///     The key frames only property
        /// </summary>
        public static readonly DependencyProperty KeyFramesOnlyProperty = DependencyProperty.Register("KeyFramesOnly", typeof (bool), typeof (MainWindow), new PropertyMetadata(false));

        /// <summary>
        ///     The no flicker mode property
        /// </summary>
        public static readonly DependencyProperty NoFlickerModeProperty = DependencyProperty.Register("NoFlickerMode", typeof (bool), typeof (MainWindow), new PropertyMetadata(false));

        /// <summary>
        ///     The no FPS reduce property
        /// </summary>
        public static readonly DependencyProperty NoFpsReduceProperty = DependencyProperty.Register("NoFpsReduce", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty SlCompatibilityProperty = DependencyProperty.Register(
            "SlCompatibility",
            typeof(bool),
            typeof(MainWindow),
            new PropertyMetadata(false, OnSlCompatibilityChanged));

        public static readonly DependencyProperty GpuMediaPackageProperty = DependencyProperty.Register("GpuMediaPackage", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        /// <summary>
        ///     The keep audio property
        /// </summary>
        public static readonly DependencyProperty KeepAudioProperty = DependencyProperty.Register("KeepAudio", typeof(bool), typeof(MainWindow), new PropertyMetadata(false));

        public static readonly DependencyProperty OpenOutputDirectoryWhenDoneProperty = DependencyProperty.Register("OpenOutputDirectoryWhenDone", typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

        /// <summary>
        ///     The target framerate property
        /// </summary>
        public static readonly DependencyProperty TargetFrameRateProperty = DependencyProperty.Register("TargetFrameRate", typeof (double), typeof (MainWindow), new PropertyMetadata(0.0));

        /// <summary>
        ///     The source filename property
        /// </summary>
        public static readonly DependencyProperty SourceFilenameProperty = DependencyProperty.Register("SourceFilename", typeof (string), typeof (MainWindow), new PropertyMetadata(null));

        /// <summary>
        ///     The target filename property
        /// </summary>
        public static readonly DependencyProperty TargetFilenameProperty = DependencyProperty.Register("TargetFilename", typeof (string), typeof (MainWindow), new PropertyMetadata(null));

        /// <summary>
        ///     The original frame rate property
        /// </summary>
        public static readonly DependencyProperty OriginalFrameRateTextProperty = DependencyProperty.Register("OriginalFrameRateText", typeof(string), typeof(MainWindow), new PropertyMetadata("From \"Unknown\" to"));

        public static readonly DependencyProperty AnalysisPreviewTextProperty = DependencyProperty.Register("AnalysisPreviewText", typeof(string), typeof(MainWindow), new PropertyMetadata("Analysis: no source selected."));

        public static readonly DependencyProperty HardwareStatusTextProperty = DependencyProperty.Register("HardwareStatusText", typeof(string), typeof(MainWindow), new PropertyMetadata("Hardware: not checked yet."));

        /// <summary>
        ///     Forced target video width
        /// </summary>
        public static readonly DependencyProperty ForcedWidthProperty = DependencyProperty.Register("ForcedWidth", typeof(int), typeof(MainWindow), new PropertyMetadata(0, OnForcedVideoSizeChanged, OnCoerceVideoSizeProperty));

        /// <summary>
        ///     Forced target video height
        /// </summary>
        public static readonly DependencyProperty ForcedHeightProperty = DependencyProperty.Register("ForcedHeight", typeof(int), typeof(MainWindow), new PropertyMetadata(0, OnForcedVideoSizeChanged, OnCoerceVideoSizeProperty));

        /// <summary>
        ///     The working directory
        /// </summary>
        private readonly string _workingDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools");

        /// <summary>
        ///     The command line opts
        /// </summary>
        private Options _cmdLineOpts = null!;

        private bool _isCommandLineMode;

        /// <summary>
        ///     The conversion canceled
        /// </summary>
        private bool _conversionCanceled;

        /// <summary>
        /// The output frame count
        /// </summary>
        private long _outputFrameCount;

        /// <summary>
        ///     The progress bar initialized flag
        /// </summary>
        private bool _progressbarInitialized;

        /// <summary>
        ///     The running process
        /// </summary>
        private Process _runningProcess = null!;

        private readonly ConcurrentBag<Process> _batchProcesses = new();
        private CancellationTokenSource? _batchCancellationTokenSource;

        /// <summary>
        /// The temp audio file
        /// </summary>
        private string _tempAudioFile = string.Empty;

        /// <summary>
        ///     The temp AVS file
        /// </summary>
        private string _tempAvsFile = string.Empty;

        /// <summary>
        /// The temp video file
        /// </summary>
        private string _tempCompressionTargetFile = string.Empty;

        /// <summary>
        /// The temp video file
        /// </summary>
        private string _tempVideoFile = string.Empty;

        private bool _replaceTargetFile;
        private int? _forcedWidthBeforeSlCompatibility;
        private int? _forcedHeightBeforeSlCompatibility;

        /// <summary>
        ///     Initializes a new instance of the <see cref="MainWindow" /> class.
        /// </summary>
        public MainWindow()
        {
            TargetFrameRate = 30.0;
            AnalysisPreviewText = "Analysis: no source selected.";
            HardwareStatusText = "Hardware: not checked yet.";
            _outputFrameCount = 0;
            OriginalFrameRateText = "From \"Unknown\" to ";

#if DEBUG
            SourceFilename = @"C:\tmp\input.avi";
            TargetFilename = @"C:\tmp\output.h264";
#else
            SourceFilename = string.Empty;
            TargetFilename = string.Empty;
#endif

            InitializeComponent();
            InitializeGpuComboBox();

            // Set DataContext so Bindings work
            DataContext = this;

            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            _isCommandLineMode = args.Length > 0;
            new Parser(settings => settings.HelpWriter = Console.Out)
                .ParseArguments<Options>(args)
                .WithParsed(Run)
                .WithNotParsed(Exit);
        }

        /// <summary>
        ///     Gets the version information.
        /// </summary>
        /// <value>The version information.</value>
        public string VersionInfo => "Version " + Assembly.GetExecutingAssembly().GetName().Version;

        /// <summary>
        ///     Gets or sets a value indicating whether debug mode is active.
        /// </summary>
        /// <value><c>true</c> if [debug mode]; otherwise, <c>false</c>.</value>
        public bool DebugMode { get; set; }

        /// <summary>
        /// Gets or sets the bit rate in kbit/sec.
        /// </summary>
        /// <value>
        /// The bit rate.
        /// </value>
        public int BitRate
        {
            get => (int) GetValue(BitRateProperty);
            set => SetValue(BitRateProperty, value);
        }

        /// <summary>Gets or sets a value indicating whether only key frames are produced.</summary>
        /// <value>
        ///   <c>true</c> if [key frames only]; otherwise, <c>false</c>.</value>
        public bool KeyFramesOnly
        {
            get => (bool) GetValue(KeyFramesOnlyProperty);
            set => SetValue(KeyFramesOnlyProperty, value);
        }

        public bool NoFlickerMode
        {
            get => (bool) GetValue(NoFlickerModeProperty);
            set => SetValue(NoFlickerModeProperty, value);
        }

        public bool NoFpsReduce
        {
            get => (bool) GetValue(NoFpsReduceProperty);
            set => SetValue(NoFpsReduceProperty, value);
        }

        public bool SlCompatibility
        {
            get => (bool)GetValue(SlCompatibilityProperty);
            set => SetValue(SlCompatibilityProperty, value);
        }

        public bool GpuMediaPackage
        {
            get => (bool)GetValue(GpuMediaPackageProperty);
            set => SetValue(GpuMediaPackageProperty, value);
        }

        /// <summary>
        /// Gets or sets a value indicating whether the audio tracks should be removed or not.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [keep audio]; otherwise, <c>false</c>.
        /// </value>
        public bool KeepAudio
        {
            get => (bool)GetValue(KeepAudioProperty);
            set => SetValue(KeepAudioProperty, value);
        }

        public bool OpenOutputDirectoryWhenDone
        {
            get => (bool)GetValue(OpenOutputDirectoryWhenDoneProperty);
            set => SetValue(OpenOutputDirectoryWhenDoneProperty, value);
        }

        /// <summary>
        ///     Gets or sets the target frame rate.
        /// </summary>
        /// <value>The target frame rate.</value>
        public double TargetFrameRate
        {
            get => (double) GetValue(TargetFrameRateProperty);
            set => SetValue(TargetFrameRateProperty, value);
        }

        /// <summary>
        ///     Gets or sets the source filename.
        /// </summary>
        /// <value>The source filename.</value>
        public string SourceFilename
        {
            get => (string) GetValue(SourceFilenameProperty);
            set
            {
                SetValue(SourceFilenameProperty, value);

                if (!string.IsNullOrEmpty(value))
                {
                    LastSourceDirectory = Path.GetDirectoryName(value) ?? string.Empty;
                }

                UpdateHeader();
            }
        }

        /// <summary>
        ///     Gets or sets the target filename.
        /// </summary>
        /// <value>The target filename.</value>
        public string TargetFilename
        {
            get => (string) GetValue(TargetFilenameProperty);
            set
            {
                SetValue(TargetFilenameProperty, value);
                if (!string.IsNullOrEmpty(value))
                {
                    LastTargetDirectory = Path.GetDirectoryName(value) ?? string.Empty;
                }
            }
        }

        /// <summary>
        ///     Gets or sets the original framerate.
        /// </summary>
        /// <value>The original framerate.</value>
        public string OriginalFrameRateText
        {
            get => (string)GetValue(OriginalFrameRateTextProperty);
            set => SetValue(OriginalFrameRateTextProperty, value);
        }

        public string AnalysisPreviewText
        {
            get => (string)GetValue(AnalysisPreviewTextProperty);
            set => SetValue(AnalysisPreviewTextProperty, value);
        }

        public string HardwareStatusText
        {
            get => (string)GetValue(HardwareStatusTextProperty);
            set => SetValue(HardwareStatusTextProperty, value);
        }

        /// <summary>
        ///     Gets or sets the forced target width
        /// </summary>
        /// <value>The forced target width.</value>
        public int ForcedWidth
        {
            get => (int)GetValue(ForcedWidthProperty);
            set => SetValue(ForcedWidthProperty, value);
        }

        /// <summary>
        ///     Gets or sets the forced target height
        /// </summary>
        /// <value>The forced target height.</value>
        public int ForcedHeight
        {
            get => (int)GetValue(ForcedHeightProperty);
            set => SetValue(ForcedHeightProperty, value);
        }

        /// <summary>
        ///     Gets or sets the original framerate.
        /// </summary>
        /// <value>The original framerate.</value>
        public double OriginalFramerate { get; set; }

        /// <summary>
        ///     Gets or sets the duration of the original.
        /// </summary>
        /// <value>The duration of the original.</value>
        public double OriginalDuration { get; set; }

        /// <summary>
        ///     Gets or sets the width of the original.
        /// </summary>
        /// <value>The width of the original.</value>
        public int OriginalWidth { get; set; }

        /// <summary>
        ///     Gets or sets the height of the original.
        /// </summary>
        /// <value>The height of the original.</value>
        public int OriginalHeight { get; set; }

        /// <summary>
        ///     Gets or sets the original numerator.
        /// </summary>
        /// <value>The original numerator.</value>
        public int OriginalNumerator { get; set; }

        /// <summary>
        ///     Gets or sets the original denominator.
        /// </summary>
        /// <value>The original denonimator.</value>
        public int OriginalDenominator { get; set; }

        private readonly record struct VideoSize(int Width, int Height);

        private static string LastSourceDirectory
        {
            get => Registry.GetValue(@"HKEY_CURRENT_USER\Software\NewFPSConverter", "LastSourceDir", string.Empty) as string ?? string.Empty;
            set => Registry.SetValue(@"HKEY_CURRENT_USER\Software\NewFPSConverter", "LastSourceDir", value);
        }

        private static string LastTargetDirectory
        {
            get => Registry.GetValue(@"HKEY_CURRENT_USER\Software\NewFPSConverter", "LastTargetDir", string.Empty) as string ?? string.Empty;
            set => Registry.SetValue(@"HKEY_CURRENT_USER\Software\NewFPSConverter", "LastTargetDir", value);
        }

        private static object OnCoerceVideoSizeProperty(DependencyObject d, object value)
        {
            if (value != null && Convert.ToInt32(value) < 0)
            {
                value = 0;
            }

            return value ?? 0;
        }

        private static void OnSlCompatibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow window)
            {
                window.ApplySlCompatibilityState();
            }
        }

        private static void OnForcedVideoSizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is MainWindow window)
            {
                window.RefreshBatchFileAnalysisTexts();
            }
        }

        private void ApplySlCompatibilityState()
        {
            if (SlCompatibility)
            {
                _forcedWidthBeforeSlCompatibility ??= ForcedWidth;
                _forcedHeightBeforeSlCompatibility ??= ForcedHeight;
                EnsureSlCompatibilityRequiredOptions();
                ApplySlCompatibleSizeToCurrentSource();
            }
            else if (_forcedWidthBeforeSlCompatibility.HasValue || _forcedHeightBeforeSlCompatibility.HasValue)
            {
                ForcedWidth = _forcedWidthBeforeSlCompatibility ?? 0;
                ForcedHeight = _forcedHeightBeforeSlCompatibility ?? 0;
                _forcedWidthBeforeSlCompatibility = null;
                _forcedHeightBeforeSlCompatibility = null;
            }

            UpdateSlCompatibilityControlState(true);
            UpdateAnalysisPreviewText();
            RefreshBatchFileAnalysisTexts();
        }

        private void EnsureSlCompatibilityRequiredOptions()
        {
            if (!SlCompatibility)
            {
                return;
            }

            KeyFramesOnly = true;
            NoFlickerMode = true;
        }

        private void ApplySlCompatibleSizeToCurrentSource()
        {
            if (!SlCompatibility || OriginalWidth <= 0 || OriginalHeight <= 0)
            {
                return;
            }

            var size = CalculateSlCompatibleSize(OriginalWidth, OriginalHeight);
            ForcedWidth = size.Width;
            ForcedHeight = size.Height;
        }

        private void UpdateSlCompatibilityControlState(bool enabled)
        {
            if (CheckSlCompatibility == null)
            {
                return;
            }

            CheckSlCompatibility.IsEnabled = enabled;
            CheckBoxKeyFrames.IsEnabled = enabled && !SlCompatibility;
            CheckBoxNoFLicker.IsEnabled = enabled && !SlCompatibility;
            TextBoxForcedWidth.IsEnabled = enabled && !SlCompatibility;
            TextBoxForcedHeight.IsEnabled = enabled && !SlCompatibility;
        }

        private bool ShouldForceTargetSize => SlCompatibility || ForcedWidth != 0 && ForcedHeight != 0;

        private VideoSize GetEffectiveTargetSize(int sourceWidth, int sourceHeight)
        {
            if (SlCompatibility)
            {
                return CalculateSlCompatibleSize(sourceWidth, sourceHeight);
            }

            return ForcedWidth != 0 && ForcedHeight != 0
                ? new VideoSize(ForcedWidth, ForcedHeight)
                : new VideoSize(sourceWidth, sourceHeight);
        }

        private static VideoSize CalculateSlCompatibleSize(int sourceWidth, int sourceHeight)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                return new VideoSize(16, 16);
            }

            return new VideoSize(
                Math.Max(16, sourceWidth / 16 * 16),
                Math.Max(16, sourceHeight / 16 * 16));
        }

        private void Run(Options options)
        {
            _cmdLineOpts = options;

            if (options.ListGpus)
            {
                GpuDetector.PrintAvailableGpus();
                Application.Current.Shutdown((int)ErrorCodes.Ok);
                return;
            }

            if (!string.IsNullOrEmpty(_cmdLineOpts.SourceFile))
            {
                SourceFilename = _cmdLineOpts.SourceFile;
            }

            if (!string.IsNullOrEmpty(_cmdLineOpts.TargetFile))
            {
                TargetFilename = EnsureTargetExtension(_cmdLineOpts.TargetFile);
                var targetExtension = Path.GetExtension(_cmdLineOpts.TargetFile);
                if (!string.IsNullOrEmpty(targetExtension))
                {
                    ComboBoxBatchOutputFormat.SelectedValue = targetExtension.ToLowerInvariant();
                }
            }
            else if (_isCommandLineMode && !_cmdLineOpts.BatchMode && !string.IsNullOrEmpty(_cmdLineOpts.SourceFile))
            {
                TargetFilename = Path.ChangeExtension(_cmdLineOpts.SourceFile, CompressedFileExtension);
            }

            if (!string.IsNullOrEmpty(_cmdLineOpts.SourceDirectory))
            {
                TextBoxSourceDirectory.Text = _cmdLineOpts.SourceDirectory;
                LastSourceDirectory = _cmdLineOpts.SourceDirectory;
                if (Directory.Exists(_cmdLineOpts.SourceDirectory))
                {
                    UpdateDetectedFiles(_cmdLineOpts.SourceDirectory);
                }
            }

            if (!string.IsNullOrEmpty(_cmdLineOpts.TargetDirectory))
            {
                TextBoxTargetDirectory.Text = _cmdLineOpts.TargetDirectory;
                LastTargetDirectory = _cmdLineOpts.TargetDirectory;
            }

            KeyFramesOnly = _cmdLineOpts.KeyFramesOnly;
            NoFlickerMode = _cmdLineOpts.NoFlicker;
            SlCompatibility = _cmdLineOpts.SlCompatibility;
            GpuMediaPackage = _cmdLineOpts.GpuMediaPackage;
            NoFpsReduce = _cmdLineOpts.MinFrameRate;
            KeepAudio = _cmdLineOpts.KeepAudio;
            ForcedWidth = _cmdLineOpts.ForceWidth;
            ForcedHeight = _cmdLineOpts.ForceHeight;
            BitRate = _cmdLineOpts.BitrateKBits;
            ComboBoxBitratePreset.SelectedValue = BitRate switch
            {
                0 or 8000 or 12000 or 19000 or 25000 => BitRate.ToString(CultureInfo.InvariantCulture),
                _ => null
            };
            SetSelectedGpu(_cmdLineOpts.GpuType);
            NumberBoxParallelJobs.Value = _cmdLineOpts.ParallelJobs;
            RefreshHardwareStatus();

            ToggleSwitchBatchMode.IsChecked = _cmdLineOpts.BatchMode;
            SetBatchModeUi(_cmdLineOpts.BatchMode);

            if (_cmdLineOpts.NoGui)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                Visibility = Visibility.Hidden;
            }

            if (_cmdLineOpts.FramesPerSecond != null)
            {
                if (_cmdLineOpts.FramesPerSecond < 10)
                {
                    if (_isCommandLineMode)
                    {
                        Application.Current.Shutdown((int)ErrorCodes.IllegalFps);
                    }

                    _cmdLineOpts.FramesPerSecond = 10;
                }

                if (_cmdLineOpts.FramesPerSecond > 200)
                {
                    if (_isCommandLineMode)
                    {
                        Application.Current.Shutdown((int)ErrorCodes.IllegalFps);
                    }

                    _cmdLineOpts.FramesPerSecond = 200;
                }

                if (_cmdLineOpts.FramesPerSecond != null)
                {
                    TargetFrameRate = (int)_cmdLineOpts.FramesPerSecond;
                }
            }


            // check acceptance of agreement
            var keySoftware = Registry.CurrentUser.OpenSubKey("Software", true);
            var keyApplication = keySoftware?.CreateSubKey("NewFPSConverter");

            if (keyApplication != null)
            {
                if (keyApplication.GetValue("LicenseAccepted") == null)
                {
                    if (_isCommandLineMode)
                    {
                        AddToLog("Error: license has not been accepted. Start NewFPS Converter once without command-line arguments and accept the license.");
                        Application.Current.Shutdown((int)ErrorCodes.LicenseNotAccepted);
                        return;
                    }

                    if (ShowLicense())
                    {
                        keyApplication.SetValue("LicenseAccepted", "1");
                    }
                    else
                    {
                        Application.Current.Shutdown((int)ErrorCodes.LicenseNotAccepted);
                        return;
                    }
                }
            }

            if (_isCommandLineMode)
            {
                TextBoxSourceFilename.IsEnabled = false;
                ButtonBrowseSource.IsEnabled = false;
                TextBoxTargetFilename.IsEnabled = false;
                ButtonBrowseTarget.IsEnabled = false;
                CheckBoxKeyFrames.IsEnabled = false;
                ComboBoxFrameRate.IsEnabled = false;
                TextBoxForcedWidth.IsEnabled = false;
                TextBoxForcedHeight.IsEnabled = false;
                ButtonConvert.IsEnabled = false;
                ButtonAbout.IsEnabled = false;
                SpinnerBitRate.IsEnabled = false;
                CheckSlCompatibility.IsEnabled = false;
                CheckBoxNoFLicker.IsEnabled = false;
                CheckBoxMinimum.IsEnabled = false;
                CheckKeepAudio.IsEnabled = false;
                CheckOpenOutputDirectoryWhenDone.IsEnabled = false;

                Application.Current.Dispatcher.BeginInvoke(
                    (Action)(() => ButtonConvert_Click(ButtonConvert, null)));
            }

            if (!_isCommandLineMode && !string.IsNullOrEmpty(SourceFilename))
            {
                GetVideoFileInformationAndUpdateFramerRate();
                ApplySlCompatibilityState();
            }
        }

        private void Exit(IEnumerable<Error> errors)
        {
            var exitCode = errors.Any(error =>
                error.Tag is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError)
                ? ErrorCodes.Ok
                : ErrorCodes.IllegalCommand;

            Application.Current.Shutdown((int)exitCode);
        }

        /// <summary>
        ///     Handles the Click event of the ButtonBrowseSource control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void ButtonBrowseSource_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Select video file...",
                InitialDirectory = !string.IsNullOrEmpty(LastSourceDirectory) ? LastSourceDirectory : AppDomain.CurrentDomain.BaseDirectory,
                DefaultExt = ".avi",
                Filter = MediaFilter
            };

            if (true != openFileDialog.ShowDialog())
            {
                return;
            }

            SourceFilename = openFileDialog.FileName;

            GetVideoFileInformationAndUpdateFramerRate();
        }

        private void ToggleSwitchBatchMode_Toggled(object sender, RoutedEventArgs e)
        {
            var batchMode = ToggleSwitchBatchMode.IsChecked == true;
            SetBatchModeUi(batchMode);
        }

        private static string EnsureTargetExtension(string targetFile)
        {
            return string.IsNullOrEmpty(Path.GetExtension(targetFile))
                ? Path.ChangeExtension(targetFile, CompressedFileExtension)
                : targetFile;
        }

        private void SetBatchModeUi(bool batchMode)
        {
            var singleModeVisibility = batchMode ? Visibility.Collapsed : Visibility.Visible;
            var batchModeVisibility = batchMode ? Visibility.Visible : Visibility.Collapsed;

            GridSingleSource.Visibility = singleModeVisibility;
            GridSingleTarget.Visibility = singleModeVisibility;
            GridBatchSource.Visibility = batchModeVisibility;
            GridFilesList.Visibility = batchModeVisibility;
            GridBatchTarget.Visibility = batchModeVisibility;
            GridBatchFormat.Visibility = batchModeVisibility;
        }

        private void ButtonBrowseSourceDir_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog
            {
                Title = "Select source folder...",
                InitialDirectory = !string.IsNullOrEmpty(LastSourceDirectory) ? LastSourceDirectory : AppDomain.CurrentDomain.BaseDirectory
            };

            if (true != openFolderDialog.ShowDialog())
            {
                return;
            }

            TextBoxSourceDirectory.Text = openFolderDialog.FolderName;
            LastSourceDirectory = openFolderDialog.FolderName;

            if (_cmdLineOpts != null)
            {
                _cmdLineOpts.SourceDirectory = openFolderDialog.FolderName;
            }

            UpdateDetectedFiles(openFolderDialog.FolderName);
        }

        private void ButtonBrowseTargetDir_Click(object sender, RoutedEventArgs e)
        {
            var openFolderDialog = new OpenFolderDialog
            {
                Title = "Select target folder...",
                InitialDirectory = !string.IsNullOrEmpty(LastTargetDirectory)
                    ? LastTargetDirectory
                    : (!string.IsNullOrEmpty(LastSourceDirectory) ? LastSourceDirectory : AppDomain.CurrentDomain.BaseDirectory)
            };

            if (true != openFolderDialog.ShowDialog())
            {
                return;
            }

            TextBoxTargetDirectory.Text = openFolderDialog.FolderName;
            LastTargetDirectory = openFolderDialog.FolderName;

            if (_cmdLineOpts != null)
            {
                _cmdLineOpts.TargetDirectory = openFolderDialog.FolderName;
            }
        }

        private void UpdateDetectedFiles(string folderName)
        {
            ListBoxDetectedFiles.Items.Clear();

            foreach (var fileName in Directory.EnumerateFiles(folderName)
                         .Where(IsCandidateVideoFile)
                         .OrderBy(Path.GetFileName))
            {
                var sourceFile = new BatchSourceFile(fileName);
                UpdateBatchSourceFileAnalysis(sourceFile);
                ListBoxDetectedFiles.Items.Add(sourceFile);
            }

            UpdateBatchAnalysisPreviewText();
        }

        private void RefreshBatchFileAnalysisTexts()
        {
            if (ListBoxDetectedFiles == null || ListBoxDetectedFiles.Items.Count == 0)
            {
                return;
            }

            foreach (var sourceFile in ListBoxDetectedFiles.Items.OfType<BatchSourceFile>())
            {
                UpdateBatchSourceFileAnalysis(sourceFile);
            }

            ListBoxDetectedFiles.Items.Refresh();
        }

        private void UpdateBatchSourceFileAnalysis(BatchSourceFile sourceFile)
        {
            if (!sourceFile.VideoInfo.HasValue)
            {
                if (TryGetVideoInfo(sourceFile.FilePath, out var analyzedInfo))
                {
                    sourceFile.VideoInfo = analyzedInfo;
                }
            }

            if (!sourceFile.VideoInfo.HasValue)
            {
                sourceFile.AnalysisText = "Analysis unavailable";
                return;
            }

            var info = sourceFile.VideoInfo.Value;
            var targetSize = GetEffectiveTargetSize(info.Width, info.Height);
            var fps = info.Framerate.ToString("F2", CultureInfo.InvariantCulture);
            sourceFile.AnalysisText = $"Source: {info.Width}x{info.Height} {fps} FPS -> Target: {targetSize.Width}x{targetSize.Height} {fps} FPS";
        }

        private void BatchSourceFileSelectionChanged(object sender, RoutedEventArgs e)
        {
            UpdateBatchAnalysisPreviewText();
        }

        private void UpdateBatchAnalysisPreviewText()
        {
            var total = ListBoxDetectedFiles.Items.Count;
            var selected = ListBoxDetectedFiles.Items
                .OfType<BatchSourceFile>()
                .Count(file => file.IsSelected);

            AnalysisPreviewText = $"Batch analysis: {total} video file(s) detected, {selected} selected.";
        }

        private List<string> GetSelectedBatchSourceFiles(string sourceDirectory)
        {
            var selectedFiles = ListBoxDetectedFiles.Items
                .OfType<BatchSourceFile>()
                .Where(file => file.IsSelected && File.Exists(file.FilePath))
                .Select(file => file.FilePath)
                .OrderBy(Path.GetFileName)
                .ToList();

            if (selectedFiles.Count > 0 || ListBoxDetectedFiles.Items.Count > 0)
            {
                return selectedFiles;
            }

            return Directory.EnumerateFiles(sourceDirectory)
                .Where(IsCandidateVideoFile)
                .OrderBy(Path.GetFileName)
                .ToList();
        }

        private static bool IsCandidateVideoFile(string fileName)
        {
            var name = Path.GetFileName(fileName);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("._", StringComparison.Ordinal))
            {
                return false;
            }

            if (!VideoExtensions.Contains(Path.GetExtension(fileName)))
            {
                return false;
            }

            try
            {
                var attributes = File.GetAttributes(fileName);
                if ((attributes & (FileAttributes.Directory | FileAttributes.Hidden | FileAttributes.System)) != 0)
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private async Task RunBatchConversionAsync()
        {
            var sourceDirectory = TextBoxSourceDirectory.Text;
            var targetDirectory = TextBoxTargetDirectory.Text;

            if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
            {
                AddToLog("Error: batch source directory is empty or does not exist.");
                ShutdownIfCommandLineBatch(ErrorCodes.IllegalCommand);
                return;
            }

            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                targetDirectory = sourceDirectory;
                TextBoxTargetDirectory.Text = targetDirectory;
            }

            Directory.CreateDirectory(targetDirectory);

            var sourceFiles = GetSelectedBatchSourceFiles(sourceDirectory);
            var outputExtension = GetSelectedBatchOutputExtension();

            if (sourceFiles.Count == 0)
            {
                AddToLog("Error: no video files selected for batch conversion.");
                ShutdownIfCommandLineBatch(ErrorCodes.IllegalCommand);
                return;
            }

            var overwriteExistingFiles = _cmdLineOpts.Overwrite;
            var existingTargetFiles = sourceFiles
                .Select(sourceFile => GetBatchTargetFilename(sourceFile, targetDirectory, outputExtension))
                .Where(File.Exists)
                .OrderBy(Path.GetFileName)
                .ToList();

            if (!_cmdLineOpts.BatchMode && !_isCommandLineMode && existingTargetFiles.Count > 0)
            {
                overwriteExistingFiles = AskOverwriteExistingBatchFiles(existingTargetFiles);
            }

            _conversionCanceled = false;
            _batchCancellationTokenSource?.Dispose();
            _batchCancellationTokenSource = new CancellationTokenSource();

            var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFmpeg);
            var gpuPreference = ComboBoxGpuType.SelectedValue as string ?? _cmdLineOpts.GpuType;
            var (gpuType, selectedAdapter) = GpuDetector.ResolveGpu(gpuPreference, ffmpegPath);
            var parallelJobs = GetSelectedParallelJobCount(gpuType);

            _cmdLineOpts.SourceDirectory = sourceDirectory;
            _cmdLineOpts.TargetDirectory = targetDirectory;
            _cmdLineOpts.GpuType = gpuPreference;
            _cmdLineOpts.ParallelJobs = parallelJobs;

            SetConversionControlsEnabled(false);

            var completed = 0;
            var failed = 0;
            ProgressBarConversion.Minimum = 0;
            ProgressBarConversion.Maximum = 100;
            ProgressBarConversion.Value = 0;
            OriginalFrameRateText = $"Batch: 0 of {sourceFiles.Count} files completed.";

            var adapterInfo = selectedAdapter != null ? $" ({selectedAdapter.Name})" : string.Empty;
            AddToLog($"Batch conversion started: {sourceFiles.Count} files, {parallelJobs} parallel job(s), encoder mode {gpuType}{adapterInfo}.");
            if (outputExtension.Equals(CompressedFileExtension, StringComparison.OrdinalIgnoreCase)
                || outputExtension.Equals(".h264", StringComparison.OrdinalIgnoreCase))
            {
                AddToLog(GpuMediaPackage
                    ? "GPU MediaPackage mode is enabled (experimental). Sympholight compatibility is not guaranteed."
                    : "GPU note: MediaPackage and raw H.264 use CPU libx264 for Sympholight-compatible encoding. Enable experimental GPU MediaPackage mode to test GPU encoding.");
            }

            try
            {
                using var semaphore = new SemaphoreSlim(parallelJobs);
                var tasks = sourceFiles.Select(async (sourceFile, index) =>
                {
                    await semaphore.WaitAsync(_batchCancellationTokenSource.Token);
                    try
                    {
                        var success = await ConvertBatchFileAsync(sourceFile, index + 1, sourceFiles.Count, targetDirectory, outputExtension, overwriteExistingFiles, gpuType, selectedAdapter, _batchCancellationTokenSource.Token);
                        if (!success)
                        {
                            Interlocked.Increment(ref failed);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref failed);
                    }
                    finally
                    {
                        var currentCompleted = Interlocked.Increment(ref completed);
                        UpdateBatchCompletedProgress(currentCompleted, sourceFiles.Count);
                        semaphore.Release();
                    }
                }).ToList();

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                _conversionCanceled = true;
            }
            finally
            {
                SetConversionControlsEnabled(true);
            }

            if (_conversionCanceled || _batchCancellationTokenSource.IsCancellationRequested)
            {
                AddToLog("Batch conversion canceled.");
                ShutdownIfCommandLineBatch(ErrorCodes.Canceled);
                return;
            }

            AddToLog($"Batch conversion done. Successful: {sourceFiles.Count - failed}, failed: {failed}.");
            if (sourceFiles.Count - failed > 0)
            {
                OpenOutputDirectoryIfRequested(targetDirectory);
            }

            ShutdownIfCommandLineBatch(failed == 0 ? ErrorCodes.Ok : ErrorCodes.Unknown);
        }

        private bool AskOverwriteExistingBatchFiles(IReadOnlyCollection<string> existingTargetFiles)
        {
            var preview = string.Join(
                Environment.NewLine,
                existingTargetFiles.Take(8).Select(Path.GetFileName));

            if (existingTargetFiles.Count > 8)
            {
                preview += Environment.NewLine + $"... and {existingTargetFiles.Count - 8} more";
            }

            var dialog = new ConfirmDialog(
                $"{existingTargetFiles.Count} output file(s) already exist.{Environment.NewLine}{Environment.NewLine}{preview}{Environment.NewLine}{Environment.NewLine}Overwrite existing files?",
                "Output files already exist",
                "Overwrite all",
                "Skip existing")
            {
                Owner = this
            };

            dialog.ShowDialog();
            return dialog.Result == MessageBoxResult.Yes;
        }

        private void ShutdownIfCommandLineBatch(ErrorCodes errorCode)
        {
            if (_isCommandLineMode && Application.Current != null)
            {
                Application.Current.Shutdown((int)errorCode);
            }
        }

        private int GetSelectedParallelJobCount(GpuDetector.GpuType gpuType)
        {
            var selectedValue = (int)Math.Round(NumberBoxParallelJobs.Value ?? 0);
            if (selectedValue > 0)
            {
                return Math.Clamp(selectedValue, 1, 16);
            }

            if (gpuType != GpuDetector.GpuType.None)
            {
                return 1;
            }

            return Math.Clamp(Math.Max(1, Environment.ProcessorCount / 2), 1, 4);
        }

        private async Task<bool> ConvertBatchFileAsync(
            string sourceFile,
            int fileIndex,
            int totalFiles,
            string targetDirectory,
            string outputExtension,
            bool overwriteExistingFiles,
            GpuDetector.GpuType gpuType,
            GpuAdapterInfo? gpuAdapter,
            CancellationToken cancellationToken)
        {
            var packageRequired = outputExtension.Equals(CompressedFileExtension, StringComparison.OrdinalIgnoreCase);
            var targetFile = GetBatchTargetFilename(sourceFile, targetDirectory, outputExtension);
            string? tempPackageDirectory = null;
            var conversionTargetFile = targetFile;

            if (File.Exists(targetFile) && !overwriteExistingFiles)
            {
                AddToLog($"Batch skipped existing file: {targetFile}");
                UpdateBatchFileProgress(fileIndex, totalFiles, sourceFile, 100);
                return true;
            }

            if (packageRequired)
            {
                tempPackageDirectory = GetTempFolderPath();
                Directory.CreateDirectory(tempPackageDirectory);
                conversionTargetFile = Path.Combine(tempPackageDirectory, Path.GetFileNameWithoutExtension(sourceFile) + ".h264");
            }

            try
            {
                if (!TryGetVideoInfo(sourceFile, out var info))
                {
                    AddToLog($"Batch error: source file could not be analyzed: {sourceFile}");
                    return false;
                }

                EnsureSlCompatibilityRequiredOptions();
                var targetFrameRate = GetBatchTargetFrameRate(info.Framerate);
                var targetSize = GetEffectiveTargetSize(info.Width, info.Height);
                var forceTargetSize = ShouldForceTargetSize;
                var bitrateInKBits = BitRate > 0 ? BitRate : CalculateBitRate(targetSize.Width, targetSize.Height, targetFrameRate);
                var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFmpeg);
                var arguments = BuildBatchFfmpegArguments(sourceFile, conversionTargetFile, targetFrameRate, bitrateInKBits, gpuType, gpuAdapter, forceTargetSize ? targetSize : null);

                AddToLog($"Batch converting: {Path.GetFileName(sourceFile)} -> {Path.GetFileName(targetFile)}");
                if (SlCompatibility)
                {
                    AddToLog($"SL-Compatibility target size: {info.Width}x{info.Height} -> {targetSize.Width}x{targetSize.Height}");
                }

                UpdateBatchFileProgress(fileIndex, totalFiles, sourceFile, 0);

                var success = await RunBatchFfmpegAsync(ffmpegPath, arguments, sourceFile, info.Duration, fileIndex, totalFiles, cancellationToken);
                if (!success || !File.Exists(conversionTargetFile))
                {
                    AddToLog($"Batch error: conversion failed for {sourceFile}");
                    return false;
                }

                CreateBatchXmlDescriptionFile(
                    sourceFile,
                    conversionTargetFile,
                    info,
                    targetFrameRate,
                    targetSize,
                    packageRequired ? Path.GetFileName(conversionTargetFile) : null);

                if (!CreateXmlFrameInfoFile(conversionTargetFile))
                {
                    AddToLog($"Batch error: frame info XML could not be written for {sourceFile}");
                    return false;
                }

                if (packageRequired)
                {
                    var packageCreated = CreateBatchMediaPackage(tempPackageDirectory!, targetFile, overwriteExistingFiles);
                    if (packageCreated)
                    {
                        UpdateBatchFileProgress(fileIndex, totalFiles, sourceFile, 100);
                    }

                    return packageCreated;
                }

                UpdateBatchFileProgress(fileIndex, totalFiles, sourceFile, 100);
                return true;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempPackageDirectory))
                {
                    DeleteFileOrFolder(tempPackageDirectory);
                }
            }
        }

        private string GetSelectedBatchOutputExtension()
        {
            if (_cmdLineOpts.BatchMode && !string.IsNullOrWhiteSpace(_cmdLineOpts.TargetFile))
            {
                var targetExtension = Path.GetExtension(_cmdLineOpts.TargetFile);
                if (IsSupportedBatchOutputExtension(targetExtension))
                {
                    return targetExtension.ToLowerInvariant();
                }
            }

            var selectedExtension = ComboBoxBatchOutputFormat.SelectedValue as string;
            return IsSupportedBatchOutputExtension(selectedExtension)
                ? selectedExtension!.ToLowerInvariant()
                : CompressedFileExtension;
        }

        private static bool IsSupportedBatchOutputExtension(string? extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return false;
            }

            return extension.Equals(".mp4", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".mkv", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".avi", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".mov", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".webm", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".ivf", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(".h264", StringComparison.OrdinalIgnoreCase)
                   || extension.Equals(CompressedFileExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetBatchTargetFilename(string sourceFile, string targetDirectory, string targetExtension)
        {
            return Path.Combine(targetDirectory, Path.GetFileNameWithoutExtension(sourceFile) + targetExtension);
        }

        private double GetBatchTargetFrameRate(double sourceFrameRate)
        {
            var targetFrameRate = TargetFrameRate;

            if (NoFlickerMode)
            {
                var factor = targetFrameRate / sourceFrameRate;
                targetFrameRate = (int)(factor + 0.5f) * sourceFrameRate;
            }

            if (Math.Abs(targetFrameRate) < double.Epsilon || NoFpsReduce && targetFrameRate < sourceFrameRate)
            {
                targetFrameRate = sourceFrameRate;
            }

            return targetFrameRate;
        }

        private string BuildBatchFfmpegArguments(string sourceFile, string targetFile, double targetFrameRate, int bitrateInKBits, GpuDetector.GpuType gpuType, GpuAdapterInfo? gpuAdapter, VideoSize? forcedTargetSize)
        {
            var targetExtension = Path.GetExtension(targetFile).ToLowerInvariant();
            var experimentalGpuH264 = GpuMediaPackage && targetExtension is ".h264" && gpuType != GpuDetector.GpuType.None;
            var encoder = GetBatchVideoEncoder(targetExtension, gpuType, experimentalGpuH264);
            var fps = targetFrameRate.ToString(CultureInfo.InvariantCulture);
            var sympholightCompatibleH264 = targetExtension is ".h264";
            var args = new List<string>
            {
                "-y",
                "-r",
                fps,
                "-i",
                Quote(sourceFile)
            };

            if (!sympholightCompatibleH264)
            {
                args.Add("-map");
                args.Add("0:v:0");
            }

            if (KeepAudio && targetExtension is not ".h264" and not ".ivf")
            {
                args.Add("-map");
                args.Add("0:a?");
            }

            var filter = BuildBatchVideoFilter(fps, sympholightCompatibleH264, forcedTargetSize);
            if (!string.IsNullOrEmpty(filter))
            {
                args.Add(sympholightCompatibleH264 ? "-filter_complex" : "-vf");
                args.Add(Quote(filter));
            }

            args.Add("-c:v");
            args.Add(encoder);
            args.AddRange(GetBatchEncoderOptions(encoder, bitrateInKBits, sympholightCompatibleH264, experimentalGpuH264, gpuAdapter));

            if (KeepAudio && targetExtension is not ".h264" and not ".ivf")
            {
                args.Add("-c:a");
                args.Add("copy");
            }
            else
            {
                args.Add("-an");
            }

            args.Add(Quote(targetFile));
            return string.Join(" ", args);
        }

        private static string BuildBatchVideoFilter(string fps, bool sympholightCompatibleH264, VideoSize? forcedTargetSize)
        {
            if (sympholightCompatibleH264 && forcedTargetSize.HasValue)
            {
                var targetSize = forcedTargetSize.Value;
                return $"nullsrc=size={targetSize.Width}x{targetSize.Height},lutrgb=0:0:0,fps=fps={fps} [base]; [0:v] setpts=PTS-STARTPTS,scale=w='min({targetSize.Width}\\,iw):h=min({targetSize.Height}\\,ih):force_original_aspect_ratio=decrease' [upperleft]; [base][upperleft] overlay=shortest=1";
            }

            var filters = new List<string>();
            if (forcedTargetSize.HasValue)
            {
                var targetSize = forcedTargetSize.Value;
                filters.Add($"scale=w='min({targetSize.Width}\\,iw)':h='min({targetSize.Height}\\,ih)':force_original_aspect_ratio=decrease");
                filters.Add($"pad={targetSize.Width}:{targetSize.Height}:(ow-iw)/2:(oh-ih)/2");
            }

            filters.Add($"fps={fps}");
            return string.Join(",", filters);
        }

        private static string GetBatchVideoEncoder(string targetExtension, GpuDetector.GpuType gpuType, bool experimentalGpuH264)
        {
            if (targetExtension is ".webm" or ".ivf")
            {
                return "libvpx";
            }

            if (targetExtension is ".h264")
            {
                return experimentalGpuH264
                    ? GpuDetector.GetEncoderName(gpuType, "libx264")
                    : "libx264";
            }

            return GpuDetector.GetEncoderName(gpuType, "libx264");
        }

        private IEnumerable<string> GetBatchEncoderOptions(string encoder, int bitrateInKBits, bool sympholightCompatibleH264, bool experimentalGpuH264, GpuAdapterInfo? gpuAdapter = null)
        {
            var options = new List<string>();

            if (encoder == "libvpx")
            {
                options.Add("-b:v");
                options.Add($"{bitrateInKBits}K");

                if (KeyFramesOnly)
                {
                    options.Add("-g");
                    options.Add("1");
                    options.Add("-keyint_min");
                    options.Add("0");
                    options.Add("-sc_threshold");
                    options.Add("1");
                }

                return options;
            }

            if (sympholightCompatibleH264 && !experimentalGpuH264)
            {
                options.Add("-tune");
                options.Add("zerolatency");
                options.Add("-x264opts");
                options.Add($"bitrate={bitrateInKBits}:vbv-maxrate={bitrateInKBits}:vbv-bufsize=1000:slices=1:ref=1:no-scenecut:nal-hrd=cbr");
                options.Add("-g");
                options.Add("1");
                options.Add("-keyint_min");
                options.Add("0");
                return options;
            }

            options.Add("-b:v");
            options.Add($"{bitrateInKBits}K");
            options.Add("-pix_fmt");
            options.Add("yuv420p");

            if (sympholightCompatibleH264 && experimentalGpuH264)
            {
                options.AddRange(GetExperimentalGpuH264Options(encoder, bitrateInKBits, gpuAdapter));
                return options;
            }

            if (encoder == "libx264")
            {
                options.Add("-threads");
                options.Add("0");

                if (KeyFramesOnly)
                {
                    options.Add("-tune");
                    options.Add("zerolatency");
                    options.Add("-x264opts");
                    options.Add($"bitrate={bitrateInKBits}:vbv-maxrate={bitrateInKBits}:vbv-bufsize=1000:slices=1:ref=1:no-scenecut:nal-hrd=cbr");
                    options.Add("-g");
                    options.Add("1");
                    options.Add("-keyint_min");
                    options.Add("0");
                }
            }
            else
            {
                if (encoder is "h264_nvenc" or "hevc_nvenc")
                {
                    if (gpuAdapter != null)
                    {
                        options.Add("-gpu");
                        options.Add(gpuAdapter.VendorIndex.ToString(CultureInfo.InvariantCulture));
                    }
                }
                else if (encoder is "h264_qsv" or "hevc_qsv")
                {
                    if (gpuAdapter != null && gpuAdapter.VendorIndex > 0)
                    {
                        options.Add("-qsv_device");
                        options.Add(gpuAdapter.VendorIndex.ToString(CultureInfo.InvariantCulture));
                    }
                }
                else if (encoder is "h264_amf" or "hevc_amf")
                {
                    if (gpuAdapter != null)
                    {
                        options.Add("-gpu");
                        options.Add(gpuAdapter.VendorIndex.ToString(CultureInfo.InvariantCulture));
                    }
                }

                if (KeyFramesOnly)
                {
                    options.Add("-g");
                    options.Add("1");
                    options.Add("-forced-idr");
                    options.Add("1");
                }
            }

            return options;
        }

        private static IEnumerable<string> GetExperimentalGpuH264Options(string encoder, int bitrateInKBits, GpuAdapterInfo? gpuAdapter = null)
        {
            if (encoder == "h264_nvenc")
            {
                var opts = new List<string>
                {
                    "-rc", "cbr",
                    "-maxrate", $"{bitrateInKBits}K",
                    "-bufsize", "1000K",
                    "-g", "1",
                    "-forced-idr", "1",
                    "-bf", "0",
                    "-preset", "p4",
                    "-tune", "ull"
                };
                if (gpuAdapter != null)
                {
                    opts.Add("-gpu");
                    opts.Add(gpuAdapter.VendorIndex.ToString(CultureInfo.InvariantCulture));
                }
                return opts;
            }

            if (encoder == "h264_amf")
            {
                var opts = new List<string>
                {
                    "-rc", "cbr",
                    "-maxrate", $"{bitrateInKBits}K",
                    "-bufsize", "1000K",
                    "-g", "1",
                    "-bf", "0"
                };
                if (gpuAdapter != null)
                {
                    opts.Add("-gpu");
                    opts.Add(gpuAdapter.VendorIndex.ToString(CultureInfo.InvariantCulture));
                }
                return opts;
            }

            if (encoder == "h264_qsv")
            {
                var opts = new List<string>
                {
                    "-global_quality", "20",
                    "-g", "1",
                    "-bf", "0"
                };
                if (gpuAdapter != null && gpuAdapter.VendorIndex > 0)
                {
                    opts.Add("-qsv_device");
                    opts.Add(gpuAdapter.VendorIndex.ToString(CultureInfo.InvariantCulture));
                }
                return opts;
            }

            return new[] { "-g", "1", "-forced-idr", "1" };
        }

        private async Task<bool> RunBatchFfmpegAsync(
            string ffmpegPath,
            string arguments,
            string sourceFile,
            double sourceDuration,
            int fileIndex,
            int totalFiles,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(ffmpegPath))
            {
                AddToLog($"Error: Executable not found: {ffmpegPath}");
                return false;
            }

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo(ffmpegPath, arguments)
                {
                    WorkingDirectory = _workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };

            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            var stdoutClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var stderrClosed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            DataReceivedEventHandler stdoutHandler = (_, e) =>
            {
                if (e.Data == null)
                {
                    stdoutClosed.TrySetResult();
                    return;
                }

                stdout.AppendLine(e.Data);
            };

            DataReceivedEventHandler stderrHandler = (_, e) =>
            {
                if (e.Data == null)
                {
                    stderrClosed.TrySetResult();
                    return;
                }

                stderr.AppendLine(e.Data);
                if (TryReadFfmpegProgressPercentage(e.Data, sourceDuration, out var percentage))
                {
                    UpdateBatchFileProgress(fileIndex, totalFiles, sourceFile, percentage);
                }
            };

            process.OutputDataReceived += stdoutHandler;
            process.ErrorDataReceived += stderrHandler;

            try
            {
                process.Start();
                _batchProcesses.Add(process);
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var cancellationRegistration = cancellationToken.Register(() => TryKillProcess(process));

                await process.WaitForExitAsync(cancellationToken);
                await stdoutClosed.Task;
                await stderrClosed.Task;

                WriteBatchProcessLog(sourceFile, arguments, process.ExitCode, stdout.ToString(), stderr.ToString());

                if (DebugMode)
                {
                    AddToLog(stderr.ToString());
                }

                return process.ExitCode == 0;
            }
            catch (OperationCanceledException)
            {
                TryKillProcess(process);
                throw;
            }
            catch (Exception ex)
            {
                AddToLog($"Batch error: failed to start ffmpeg for {sourceFile}: {ex.Message}");
                return false;
            }
            finally
            {
                process.OutputDataReceived -= stdoutHandler;
                process.ErrorDataReceived -= stderrHandler;
            }
        }

        private void WriteBatchProcessLog(string sourceFile, string arguments, int exitCode, string stdout, string stderr)
        {
            try
            {
                var safeName = string.Join("_", Path.GetFileNameWithoutExtension(sourceFile).Split(Path.GetInvalidFileNameChars()));
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var logFile = Path.Combine(Path.GetTempPath(), $"NewFPSConverter_batch_{safeName}_{timestamp}.log");
                var sb = new StringBuilder();
                sb.AppendLine("Command: " + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFmpeg));
                sb.AppendLine("Parameters: " + arguments);
                sb.AppendLine("ExitCode: " + exitCode);
                sb.AppendLine("--- STDOUT ---");
                sb.AppendLine(stdout);
                sb.AppendLine("--- STDERR ---");
                sb.AppendLine(stderr);
                File.WriteAllText(logFile, sb.ToString(), Encoding.UTF8);
                AddToLog("Batch process finished. ExitCode=" + exitCode + "; Log: " + logFile);
            }
            catch (Exception ex)
            {
                AddToLog("Warning: Could not write batch process log: " + ex.Message);
            }
        }

        private bool TryGetVideoInfo(string sourceFile, out VideoInfo info)
        {
            info = default;

            var command = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFprobe);
            var output = ExecuteAndGetOutput(command, string.Format(ExecutableFFprobeStreamInfoParameters, sourceFile));
            if (string.IsNullOrEmpty(output))
            {
                return false;
            }

            var hasFrameRate = TryReadFrameRate(output, out var frameRate);
            var hasDuration = TryReadDuration(output, out var duration);

            return hasFrameRate
                   && TryReadInt(output, FFprobeOutputWidth, out var width)
                   && TryReadInt(output, FFprobeOutputHeight, out var height)
                   && hasDuration
                   && frameRate > 0
                   && width > 0
                   && height > 0
                   && duration > 0
                   && SetVideoInfo(out info, frameRate, width, height, duration);
        }

        private static bool TryReadFrameRate(string output, out double frameRate)
        {
            return TryReadDouble(output, FFprobeOutputFramerate, out frameRate)
                   || TryReadDouble(output, FFprobeOutputAverageFramerate, out frameRate);
        }

        private static bool TryReadDuration(string output, out double duration)
        {
            return TryReadDouble(output, FFprobeOutputDuration, out duration)
                   || TryReadDouble(output, FFprobeOutputFormatDuration, out duration);
        }

        private static bool SetVideoInfo(out VideoInfo info, double frameRate, int width, int height, double duration)
        {
            info = new VideoInfo(frameRate, width, height, duration);
            return true;
        }

        private static bool TryReadInt(string output, string key, out int value)
        {
            value = 0;
            var position = output.IndexOf(key, StringComparison.Ordinal);
            if (position < 0)
            {
                return false;
            }

            position += key.Length;
            var end = position;
            while (end < output.Length && char.IsDigit(output[end]))
            {
                end++;
            }

            return int.TryParse(output[position..end], out value);
        }

        private static bool TryReadDouble(string output, string key, out double value)
        {
            value = 0;
            var position = output.IndexOf(key, StringComparison.Ordinal);
            if (position < 0)
            {
                return false;
            }

            position += key.Length;
            var end = position;
            while (end < output.Length && output[end] != '"')
            {
                end++;
            }

            var rawValue = output[position..end];
            var divider = rawValue.IndexOf("/", StringComparison.Ordinal);
            if (divider > 0)
            {
                return double.TryParse(rawValue[..divider], NumberStyles.Float, CultureInfo.InvariantCulture, out var upper)
                       && double.TryParse(rawValue[(divider + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var lower)
                       && lower != 0
                       && SetDouble(out value, upper / lower);
            }

            return double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool SetDouble(out double value, double newValue)
        {
            value = newValue;
            return true;
        }

        private bool CreateBatchMediaPackage(string packageSourceDirectory, string packageTargetFile, bool overwriteExistingFile)
        {
            try
            {
                if (overwriteExistingFile && File.Exists(packageTargetFile))
                {
                    File.Delete(packageTargetFile);
                }

                using var archive = ZipFile.Open(packageTargetFile, ZipArchiveMode.Create);

                foreach (var file in Directory.EnumerateFiles(packageSourceDirectory))
                {
                    archive.CreateEntryFromFile(file, Path.GetFileName(file), CompressionLevel.NoCompression);
                }

                return true;
            }
            catch (Exception ex)
            {
                AddToLog($"Batch error: media package {packageTargetFile} cannot be written: {ex.Message}");
                return false;
            }
        }

        private void CreateBatchXmlDescriptionFile(string sourceFile, string targetFile, VideoInfo info, double targetFrameRate, VideoSize targetSize, string? packageOutputFilename = null)
        {
            try
            {
                var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("VideoInfo",
                        new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"),
                        new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                        new XElement("InputFilename", sourceFile),
                        new XElement("OutputFilename", packageOutputFilename ?? targetFile),
                        new XElement("Duration", info.Duration.ToString("F8", CultureInfo.InvariantCulture)),
                        new XElement("Width", targetSize.Width),
                        new XElement("Height", targetSize.Height),
                        new XElement("Framerate", targetFrameRate.ToString("F8", CultureInfo.InvariantCulture)),
                        new XElement("OriginalWidth", info.Width),
                        new XElement("OriginalHeight", info.Height),
                        new XElement("OriginalFramerate", info.Framerate.ToString("F8", CultureInfo.InvariantCulture))));

                doc.Save(targetFile + ".desc.xml");
            }
            catch (Exception ex)
            {
                AddToLog($"Warning: XML description file cannot be written for {targetFile}: {ex.Message}");
            }
        }

        private static bool TryReadFfmpegProgressPercentage(string message, double duration, out double percentage)
        {
            percentage = 0;
            if (duration <= 0)
            {
                return false;
            }

            var position = message.IndexOf("time=", StringComparison.Ordinal);
            if (position < 0)
            {
                return false;
            }

            position += "time=".Length;
            while (position < message.Length && char.IsWhiteSpace(message[position]))
            {
                position++;
            }

            var end = position;
            while (end < message.Length && !char.IsWhiteSpace(message[end]))
            {
                end++;
            }

            var rawTime = message[position..end];
            var parts = rawTime.Split(':');
            if (parts.Length != 3
                || !double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var hours)
                || !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
                || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            {
                return false;
            }

            var elapsed = hours * 3600.0 + minutes * 60.0 + seconds;
            percentage = Math.Clamp(elapsed * 100.0 / duration, 0, 100);
            return true;
        }

        private void UpdateBatchFileProgress(int fileIndex, int totalFiles, string sourceFile, double percentage)
        {
            Application.Current.Dispatcher.BeginInvoke(
                (Action)(() =>
                {
                    ProgressBarConversion.Minimum = 0;
                    ProgressBarConversion.Maximum = 100;
                    ProgressBarConversion.Value = Math.Clamp(percentage, 0, 100);
                    OriginalFrameRateText = $"File {fileIndex} of {totalFiles}: {Path.GetFileName(sourceFile)}";
                    UpdateHeader(percentage);
                }));
        }

        private void UpdateBatchCompletedProgress(int completed, int total)
        {
            Application.Current.Dispatcher.BeginInvoke(
                (Action)(() =>
                {
                    if (completed >= total)
                    {
                        ProgressBarConversion.Value = 100;
                    }

                    OriginalFrameRateText = completed >= total
                        ? $"Batch completed: {completed} of {total} files."
                        : $"Batch: {completed} of {total} files completed.";
                    UpdateHeader(total == 0 ? 0 : completed * 100.0 / total);
                }));
        }

        private void SetConversionControlsEnabled(bool enabled)
        {
            TextBoxSourceFilename.IsEnabled = enabled;
            ButtonBrowseSource.IsEnabled = enabled;
            TextBoxTargetFilename.IsEnabled = enabled;
            ButtonBrowseTarget.IsEnabled = enabled;
            TextBoxSourceDirectory.IsEnabled = enabled;
            ButtonBrowseSourceDir.IsEnabled = enabled;
            TextBoxTargetDirectory.IsEnabled = enabled;
            ButtonBrowseTargetDir.IsEnabled = enabled;
            ComboBoxBatchOutputFormat.IsEnabled = enabled;
            ToggleSwitchBatchMode.IsEnabled = enabled;
            ComboBoxBitratePreset.IsEnabled = enabled;
            ComboBoxGpuType.IsEnabled = enabled;
            NumberBoxParallelJobs.IsEnabled = enabled;
            CheckOpenOutputDirectoryWhenDone.IsEnabled = enabled;
            ComboBoxFrameRate.IsEnabled = enabled;
            ButtonConvert.IsEnabled = enabled;
            ButtonAbout.IsEnabled = enabled;
            SpinnerBitRate.IsEnabled = enabled;
            CheckBoxMinimum.IsEnabled = enabled;
            CheckKeepAudio.IsEnabled = enabled;
            UpdateSlCompatibilityControlState(enabled);
        }

        private void InitializeGpuComboBox()
        {
            ComboBoxGpuType.Items.Clear();

            ComboBoxGpuType.Items.Add(new ComboBoxItem
            {
                Content = "Auto detect",
                Tag = "auto"
            });

            var adapters = GpuDetector.GetAvailableAdapters();
            if (adapters.Count > 0)
            {
                foreach (var adapter in adapters)
                {
                    ComboBoxGpuType.Items.Add(new ComboBoxItem
                    {
                        Content = $"[{adapter.Index}] {adapter.Name} ({adapter.EncoderTechnology})",
                        Tag = adapter.Index.ToString(CultureInfo.InvariantCulture)
                    });
                }
            }
            else
            {
                ComboBoxGpuType.Items.Add(new ComboBoxItem { Content = "NVIDIA NVENC", Tag = "nvidia" });
                ComboBoxGpuType.Items.Add(new ComboBoxItem { Content = "AMD AMF", Tag = "amd" });
                ComboBoxGpuType.Items.Add(new ComboBoxItem { Content = "Intel Quick Sync", Tag = "intel" });
            }

            ComboBoxGpuType.Items.Add(new ComboBoxItem
            {
                Content = "CPU only",
                Tag = "none"
            });

            ComboBoxGpuType.SelectedValue = "auto";
        }

        private void SetSelectedGpu(string? gpuPreference)
        {
            if (string.IsNullOrWhiteSpace(gpuPreference) || gpuPreference.Equals("auto", StringComparison.OrdinalIgnoreCase))
            {
                ComboBoxGpuType.SelectedValue = "auto";
                return;
            }

            if (gpuPreference.Equals("none", StringComparison.OrdinalIgnoreCase) ||
                gpuPreference.Equals("cpu", StringComparison.OrdinalIgnoreCase))
            {
                ComboBoxGpuType.SelectedValue = "none";
                return;
            }

            // Direct tag match (e.g. index "0", "1", or "nvidia")
            foreach (ComboBoxItem item in ComboBoxGpuType.Items)
            {
                if (string.Equals(item.Tag as string, gpuPreference, StringComparison.OrdinalIgnoreCase))
                {
                    ComboBoxGpuType.SelectedItem = item;
                    return;
                }
            }

            // Semantic match via GpuDetector.ResolveGpu
            var (resolvedType, adapter) = GpuDetector.ResolveGpu(gpuPreference);
            if (adapter != null)
            {
                var adapterTag = adapter.Index.ToString(CultureInfo.InvariantCulture);
                foreach (ComboBoxItem item in ComboBoxGpuType.Items)
                {
                    if (string.Equals(item.Tag as string, adapterTag, StringComparison.OrdinalIgnoreCase))
                    {
                        ComboBoxGpuType.SelectedItem = item;
                        return;
                    }
                }
            }

            if (resolvedType != GpuDetector.GpuType.None)
            {
                var vendorTag = resolvedType switch
                {
                    GpuDetector.GpuType.Nvidia => "nvidia",
                    GpuDetector.GpuType.Intel => "intel",
                    GpuDetector.GpuType.Amd => "amd",
                    _ => null
                };

                if (vendorTag != null)
                {
                    foreach (ComboBoxItem item in ComboBoxGpuType.Items)
                    {
                        if (string.Equals(item.Tag as string, vendorTag, StringComparison.OrdinalIgnoreCase))
                        {
                            ComboBoxGpuType.SelectedItem = item;
                            return;
                        }
                    }
                }
            }

            ComboBoxGpuType.SelectedValue = "auto";
        }

        private async void RefreshHardwareStatus()
        {
            var ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFmpeg);
            var gpuPreference = ComboBoxGpuType.SelectedValue as string ?? "auto";

            HardwareStatusText = "Hardware: detecting...";

            var (detectedGpu, adapter) = await Task.Run(() => GpuDetector.ResolveGpu(gpuPreference, ffmpegPath));
            var encoder = GpuDetector.GetEncoderName(detectedGpu);
            var mode = detectedGpu switch
            {
                GpuDetector.GpuType.Nvidia => adapter != null ? $"NVIDIA NVENC ({adapter.Name})" : "NVIDIA NVENC",
                GpuDetector.GpuType.Amd => adapter != null ? $"AMD AMF ({adapter.Name})" : "AMD AMF",
                GpuDetector.GpuType.Intel => adapter != null ? $"Intel Quick Sync ({adapter.Name})" : "Intel Quick Sync",
                _ => "CPU"
            };

            HardwareStatusText = $"Hardware: {mode} ({encoder})";
        }

        private void ComboBoxBitratePreset_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (ComboBoxBitratePreset.SelectedValue is string rawValue
                && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bitrate))
            {
                BitRate = bitrate;
            }
        }

        private void ComboBoxGpuType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                RefreshHardwareStatus();
            }
        }

        private void UpdateAnalysisPreviewText()
        {
            if (string.IsNullOrWhiteSpace(SourceFilename))
            {
                AnalysisPreviewText = "Analysis: no source selected.";
                return;
            }

            if (OriginalWidth <= 0 || OriginalHeight <= 0 || OriginalDuration <= 0 || OriginalFramerate <= 0)
            {
                AnalysisPreviewText = "Analysis: source could not be analyzed.";
                return;
            }

            if (SlCompatibility)
            {
                var targetSize = CalculateSlCompatibleSize(OriginalWidth, OriginalHeight);
                AnalysisPreviewText = $"Analysis: {OriginalWidth}x{OriginalHeight} -> SL {targetSize.Width}x{targetSize.Height}, {OriginalFramerate:F2} FPS, {FormatDuration(OriginalDuration)}";
                return;
            }

            AnalysisPreviewText = $"Analysis: {OriginalWidth}x{OriginalHeight}, {OriginalFramerate:F2} FPS, {FormatDuration(OriginalDuration)}";
        }

        private static string FormatDuration(double durationSeconds)
        {
            var duration = TimeSpan.FromSeconds(durationSeconds);
            return duration.TotalHours >= 1
                ? duration.ToString(@"h\:mm\:ss", CultureInfo.InvariantCulture)
                : duration.ToString(@"m\:ss", CultureInfo.InvariantCulture);
        }

        private void OpenOutputDirectoryIfRequested(string? outputPath)
        {
            if (!OpenOutputDirectoryWhenDone || _isCommandLineMode || string.IsNullOrWhiteSpace(outputPath))
            {
                return;
            }

            var directory = Directory.Exists(outputPath) ? outputPath : Path.GetDirectoryName(outputPath);
            if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            {
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                AddToLog("Warning: Could not open output directory: " + ex.Message);
            }
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>
        ///     Handles the Click event of the ButtonBrowseTarget control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void ButtonBrowseTarget_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog {Title = "Select target file..."};

            if (!string.IsNullOrEmpty(LastTargetDirectory))
            {
                saveFileDialog.InitialDirectory = LastTargetDirectory;
            }
            else if (!string.IsNullOrEmpty(LastSourceDirectory))
            {
                saveFileDialog.InitialDirectory = LastSourceDirectory;
            }
            else
            {
                saveFileDialog.InitialDirectory = AppDomain.CurrentDomain.BaseDirectory;
            }

            saveFileDialog.DefaultExt = CompressedFileExtension;
            saveFileDialog.Filter = $"Media Package|*.{CompressedFileExtension.TrimStart('.')}|Video Files|*.mpg;*.avi;*.wmv;*.mov;*.mp4;*.h264;*.mkv;*.ivf;*.webm|All Files|*.*";
            saveFileDialog.FilterIndex = 1;
            saveFileDialog.AddExtension = true;
            if (true == saveFileDialog.ShowDialog())
            {
                TargetFilename = saveFileDialog.FileName;
            }
        }

        /// <summary>
        ///     Executes the and get output.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns>System.String.</returns>
        private static string ExecuteAndGetOutput(string filename, string parameters)
        {
            var processStartInfo = new ProcessStartInfo(filename, parameters)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Normal,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            var process = new Process {StartInfo = processStartInfo};
            process.Start();

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return output + error;
        }

        /// <summary>
        ///     Gets information from the video and updates the frame rate in the GUI.
        /// </summary>
        /// <returns>System.Double.</returns>
        private double GetVideoFileInformationAndUpdateFramerRate()
        {
            // initialize defaults
            OriginalDuration = 0.0;
            OriginalWidth = 0;
            OriginalHeight = 0;
            OriginalFramerate = 0;
            OriginalFrameRateText = "From \"Unknown\" to ";
            AnalysisPreviewText = "Analysis: analyzing source...";
            OriginalNumerator = 60;
            OriginalDenominator = 1;

            _progressbarInitialized = false;

            var command = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFprobe);
            var output = ExecuteAndGetOutput(command, string.Format(ExecutableFFprobeStreamInfoParameters, SourceFilename));

            if (string.IsNullOrEmpty(output))
            {
                AddToLog("Error: source filename " + SourceFilename + " could not be analyzed!");
                UpdateAnalysisPreviewText();

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.SourceFileNoAcceptable);
                }

                return 0.0;
            }

            if (!TryReadFrameRate(output, out var frameRate))
            {
                AddToLog("Error: source filename " + SourceFilename + " could not be analyzed!");
                UpdateAnalysisPreviewText();

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.SourceFileNoAcceptable);
                }

                return 0.0;
            }

            if (!TryReadInt(output, FFprobeOutputWidth, out var width))
            {
                AddToLog("Error: source filename " + SourceFilename + " could not be analyzed!");
                UpdateAnalysisPreviewText();

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.SourceFileNoAcceptable);
                }

                return frameRate;
            }

            if (!TryReadInt(output, FFprobeOutputHeight, out var height))
            {
                AddToLog("Error: source filename " + SourceFilename + " could not be analyzed!");
                UpdateAnalysisPreviewText();

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.SourceFileNoAcceptable);
                }

                return frameRate;
            }

            OriginalFramerate = frameRate;
            OriginalNumerator = (int)Math.Round(frameRate);
            OriginalDenominator = 1;
            OriginalWidth = width;
            OriginalHeight = height;
            OriginalFrameRateText = "From " + (Math.Abs(frameRate) < 1.0 ? @"""Unknown""" : $"{frameRate:F2}") + " FPS to ";

            if (TryReadDuration(output, out var duration))
            {
                OriginalDuration = duration;

                ProgressBarConversion.Minimum = 0.0;
                ProgressBarConversion.Maximum = 100.0;
                ProgressBarConversion.Value = 0.0f;

                _progressbarInitialized = true;
            }
            else
            {
                AddToLog("Warning: source duration is not available; progress percentage may be limited.");
            }

            UpdateAnalysisPreviewText();
            ApplySlCompatibleSizeToCurrentSource();
            return frameRate;
        }

        /// <summary>
        ///     Handles the Click event of the Button Convert control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private async void ButtonConvert_Click(object sender, RoutedEventArgs? e)
        {
            if (ToggleSwitchBatchMode.IsChecked == true)
            {
                await RunBatchConversionAsync();
                return;
            }

            if (string.IsNullOrEmpty(SourceFilename))
            {
                AddToLog("Error: source filename is empty");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalCommand);
                }

                return;
            }

            var sourceExt = Path.GetExtension(SourceFilename).ToLower();

            if (sourceExt != ".mpg" && sourceExt != ".avi" && sourceExt != ".wmv" && sourceExt != ".mov" && sourceExt != ".mp4" && sourceExt != ".h264" && sourceExt != ".mkv")
            {
                AddToLog("Error: source filename has illegal extension");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalCommand);
                }

                return;
            }

            if (string.IsNullOrEmpty(TargetFilename))
            {
                AddToLog("Error: target filename is empty");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalCommand);
                }

                return;
            }

            var targetDir = Path.GetDirectoryName(TargetFilename);
            if (string.IsNullOrEmpty(targetDir))
            {
                TargetFilename = Path.GetFullPath(TargetFilename);
                targetDir = Path.GetDirectoryName(TargetFilename);
            }

            if (targetDir == null || !Directory.Exists(targetDir))
            {
                AddToLog("Error: target filename contains illegal directory");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalCommand);
                }

                return;
            }

            var targetExt = Path.GetExtension(TargetFilename).ToLower();

            if (targetExt != ".mpg" && targetExt != ".avi" && targetExt != ".wmv" && targetExt != ".mov" && targetExt != ".mp4" && targetExt != ".h264" && targetExt != ".mkv" && targetExt != ".ivf" && targetExt != ".webm" && targetExt != CompressedFileExtension)
            {
                AddToLog("Error: target filename has illegal extension");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalCommand);
                }

                return;
            }


            if (!File.Exists(SourceFilename))
            {
                AddToLog("Error: source filename " + SourceFilename + " does not exist!");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalCommand);
                }

                return;
            }

            if (File.Exists(TargetFilename))
            {
                MessageBoxResult result;
                _replaceTargetFile = false;
                if (_isCommandLineMode)
                {
                    if (_cmdLineOpts.Overwrite)
                    {
                        result = MessageBoxResult.Yes;
                        _replaceTargetFile = true;
                    }
                    else
                    {
                        AddToLog("Error: target file already exists: " + TargetFilename);
                        Application.Current.Shutdown((int)ErrorCodes.TargetExists);
                        return;
                    }
                }
                else if (_cmdLineOpts.BatchMode && _cmdLineOpts.Overwrite)
                {
                    result = MessageBoxResult.Yes;
                    _replaceTargetFile = true;
                }
                else
                {
                    var isCompressedFile = IsZipFile(TargetFilename);
                    if (isCompressedFile)
                    {
                        var videoFileName = Path.GetFileName(SourceFilename);
                        var dialog1 = new ConfirmDialog($"Do you want to add the video '{videoFileName}' to the container file {TargetFilename}?", "Target File already exists");
                        dialog1.Owner = this;
                        dialog1.ShowDialog();
                        result = dialog1.Result;
                        if (result == MessageBoxResult.Yes)
                        {
                            var alreadyExists = PackageContainsFile(TargetFilename, videoFileName);
                            if (alreadyExists)
                            {
                                var dialog2 = new ConfirmDialog($"The container {TargetFilename} already contains a video with the name '{videoFileName}'\n\nDo you want to overwrite {videoFileName} in the container?", "File already exists");
                                dialog2.Owner = this;
                                dialog2.ShowDialog();
                                result = dialog2.Result;
                            }
                        } 
                        else if (result == MessageBoxResult.No)
                        {
                            var dialog3 = new ConfirmDialog($"Do you want to overwrite {TargetFilename}?", "Target File already exists");
                            dialog3.Owner = this;
                            dialog3.ShowDialog();
                            result = dialog3.Result;
                            _replaceTargetFile = result == MessageBoxResult.Yes;
                        }
                    }
                    else
                    {
                        var dialog4 = new ConfirmDialog($"Do you want to overwrite {TargetFilename}?", "Target File already exists");
                        dialog4.Owner = this;
                        dialog4.ShowDialog();
                        result = dialog4.Result;
                        _replaceTargetFile = result == MessageBoxResult.Yes;
                    }
                }

                if (result != MessageBoxResult.Yes)
                {
                    AddToLog("Error: User chose not to overwrite existing file: " + TargetFilename);

                    if (_isCommandLineMode)
                    {
                        Application.Current.Shutdown((int) ErrorCodes.TargetExists);
                    }

                    return;
                }
            }
            else
            {
                _replaceTargetFile = false;
            }

            var sourceFrameRate = GetVideoFileInformationAndUpdateFramerRate();
            EnsureSlCompatibilityRequiredOptions();
            ApplySlCompatibleSizeToCurrentSource();

            if (SlCompatibility && OriginalWidth > 0 && OriginalHeight > 0)
            {
                AddToLog($"SL-Compatibility target size: {OriginalWidth}x{OriginalHeight} -> {ForcedWidth}x{ForcedHeight}");
            }

            if (Math.Abs(TargetFrameRate) > double.Epsilon && (TargetFrameRate < 10.0 || TargetFrameRate > 5000))
            {
                AddToLog("Error: Frame rate exceeds range 10-5000!");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.IllegalFps);
                }

                return;
            }

            if (Math.Abs(TargetFrameRate) < double.Epsilon || NoFpsReduce && TargetFrameRate < OriginalFramerate)
            {
                TargetFrameRate = OriginalFramerate;
            }

            ConvertVideo(sourceFrameRate);
        }

        /// <summary>
        ///     Returns GGTs of a and b
        /// </summary>
        /// <param name="a">A.</param>
        /// <param name="b">The b.</param>
        /// <returns>System.Int32.</returns>
        private static int Ggt(int a, int b)
        {
            int rest;
            do
            {
                rest = b%a;
                b = a;
                a = rest;
            } while (rest != 0);
            return b;
        }

        /// <summary>
        ///     Converts the double to fraction.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="num">The numerator.</param>
        /// <param name="den">The denominator.</param>
        private void ConvertDoubleToFraction(double value, out int num, out int den)
        {
            if (NoFlickerMode)
            {
                // get rid of the decimal places
                value = (int) (value + 0.5f);
                TargetFrameRate = value*OriginalFramerate;
            }

            num = (int) (1000000*value);
            den = 1000000;

            var tempGgt = Ggt(num, den);

            num /= tempGgt;
            den /= tempGgt;
        }

        /// <summary>
        /// Gets the temporary filename.
        /// </summary>
        /// <param name="extension">The extension.</param>
        /// <returns>System.String.</returns>
        private string GetTempFilename(string extension)
        {
            var tempDir = Path.GetTempPath();
            string tempPath;
            var rnd = new Random();

            for (; ; )
            {
                var index = rnd.Next(0, 100000);

                tempPath = Path.Combine(tempDir, $"conv{index:D6}.{extension}");

                if (!File.Exists(tempPath))
                {
                    break;
                }
            }

            return tempPath;
        }

        /// <summary>
        /// Gets the temporary folder path.</summary>
        /// <returns>Unique Path to temporary folder</returns>
        private string GetTempFolderPath()
        {
            var tempDir = Path.GetTempPath();
            string tempPath;
            var rnd = new Random();

            for (; ; )
            {
                var index = rnd.Next(0, 100000);

                tempPath = Path.Combine(tempDir, $"vid{index:D6}");
                if (!Directory.Exists(tempPath))
                {
                    break;
                }
            }

            return tempPath;
        }

        /// <summary>
        /// Converts the video.
        /// </summary>
        /// <param name="sourceFrameRate">The source frame rate.</param>
        private void ConvertVideo(double sourceFrameRate)
        {
            StopAnyRunningProcess();
            CleanUp();

            _outputFrameCount = 0;

            _tempAvsFile = GetTempFilename("avs");

            try
            {
                var speedChangeFactor = TargetFrameRate/sourceFrameRate;
                var forcedWidth = ForcedWidth;
                var forcedHeight = ForcedHeight;

                ConvertDoubleToFraction(speedChangeFactor, out var num, out var den);

                AddToLog($"Starting conversion process for {Path.GetFileName(SourceFilename)} -> {Path.GetFileName(TargetFilename)}, original fps {sourceFrameRate:F2} to {TargetFrameRate:F2} fps");

                // Use SourceFilename directly instead of .avs file (skip AviSynth dependency)
                _tempAvsFile = SourceFilename;

                var ffmpegCommand = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFmpeg);
                var extension = Path.GetExtension(TargetFilename) ?? ".h264";
                var targetExtension = extension.ToLower();
                var sympholightCompatibleH264 = targetExtension is ".h264" or CompressedFileExtension;
                var targetSize = GetEffectiveTargetSize(OriginalWidth, OriginalHeight);
                var bitrateInKBits = "" + (BitRate > 0 ? BitRate : CalculateBitRate(targetSize.Width, targetSize.Height, TargetFrameRate));

                string ffmpegParametersString;
                var patchRequired = false;
                var compressionRequired = false;
                switch (targetExtension)
                {
                    case ".webm":
                        ffmpegParametersString = KeyFramesOnly ? ExecutableFFmpegParametersOnlyKeyFramesIvf : ExecutableFFmpegParametersStandardIvf;
                        break;

                    case ".ivf":
                        ffmpegParametersString = KeyFramesOnly ? ExecutableFFmpegParametersOnlyKeyFramesIvf : ExecutableFFmpegParametersStandardIvf;
                        patchRequired = true;
                        break;
                    
                    case CompressedFileExtension:
                        compressionRequired = true;
                        targetExtension = ".h264";
                        goto default;

                    default:
                        if (forcedWidth != 0 && forcedHeight != 0)
                        {
                            ffmpegParametersString = (KeyFramesOnly || sympholightCompatibleH264)
                                ? ExecutableFFmpegParametersOnlyKeyFramesH264WithForcedSize
                                : ExecutableFFmpegParametersStandardH264WithForcedSize;
                        }
                        else
                        {
                            ffmpegParametersString = (KeyFramesOnly || sympholightCompatibleH264)
                                ? ExecutableFFmpegParametersOnlyKeyFramesH264
                                : ExecutableFFmpegParametersStandardH264;
                        }

                        break;
                }

                var commands2Execute = new List<Command2Execute>();

                string ffmpegParameters;

                // extract the audio part (if required)
                if (KeepAudio)
                {
                    _tempAudioFile = GetTempFilename("ogg");
                    _tempVideoFile = GetTempFilename(targetExtension.Substring(1));

                    ffmpegParameters = string.Format(ExecutableFFmpegParametersExtractAudio, SourceFilename, _tempAudioFile);
                    AddToLog("--- DEMUXING AUDIO ----------------------------------------------------");
                    AddToLog("Command:   " + ffmpegCommand);
                    AddToLog("Parameters:" + ffmpegParameters);

                    commands2Execute.Add(new Command2Execute(ffmpegCommand, ffmpegParameters));
                }
                else
                {
                    _tempAudioFile = string.Empty;
                    _tempVideoFile = string.Empty;
                }

                _tempCompressionTargetFile = string.Empty;
                if (compressionRequired)
                {
                    var tmpPath = GetTempFolderPath();
                    if (!Directory.Exists(tmpPath))
                    {
                        Directory.CreateDirectory(tmpPath);
                        _tempCompressionTargetFile = Path.Combine(tmpPath, Path.GetFileNameWithoutExtension(SourceFilename) + targetExtension);
                    }
                }

                // convert the video
                var targetFilename = string.IsNullOrEmpty(_tempCompressionTargetFile) ? TargetFilename : _tempCompressionTargetFile;
                
                if (forcedWidth != 0 && forcedHeight != 0)
                {
                    ffmpegParameters = string.Format(ffmpegParametersString, SourceFilename, string.IsNullOrEmpty(_tempVideoFile) ? targetFilename : _tempVideoFile, bitrateInKBits, forcedWidth, forcedHeight, TargetFrameRate.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    ffmpegParameters = string.Format(ffmpegParametersString, SourceFilename, string.IsNullOrEmpty(_tempVideoFile) ? targetFilename : _tempVideoFile, bitrateInKBits);
                }

                // Inject -r fps before -i to avoid AviSynth dependency
                var fpsStr = TargetFrameRate.ToString(CultureInfo.InvariantCulture);
                ffmpegParameters = ffmpegParameters.Replace(" -i \"", $" -r {fpsStr} -i \"");

                AddToLog("--- CONVERTING VIDEO --------------------------------------------------");
                AddToLog("Command:   " + ffmpegCommand);
                AddToLog("Parameters:" + ffmpegParameters);
                commands2Execute.Add(new Command2Execute(ffmpegCommand, ffmpegParameters));

                // mux video and audio (if required)
                if (KeepAudio)
                {
                    ffmpegParameters = string.Format(ExecutableFFmpegParametersMuxVideoAndAudio, _tempVideoFile, _tempAudioFile, targetFilename);
                    AddToLog("--- MUXING VIDEO AND AUDIO --------------------------------------------");
                    AddToLog("Command:   " + ffmpegCommand);
                    AddToLog("Parameters:" + ffmpegParameters);
                    commands2Execute.Add(new Command2Execute(ffmpegCommand, ffmpegParameters));
                }

                ExecuteCommandsAndReportProgress(commands2Execute);
                
                if (patchRequired)
                {
                    PatchFrameCountInIvfFile();
                }
            }
            catch (Exception e)
            {
                StopAnyRunningProcess();
                CleanUp();

                AddToLog("Error, Exception: " + e.Message);

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.Unknown);
                }
            }
        }

        /// <summary>
        ///     Processes the UI tasks.
        /// </summary>
        public static void ProcessUiTasks()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(delegate
            {
                frame.Continue = false;
                return null;
            }), null);
            Dispatcher.PushFrame(frame);
        }

        /// <summary>
        /// Executes the command and report progress.
        /// </summary>
        /// <param name="commands2Execute">The list of commands to execute.</param>
        private void ExecuteCommandsAndReportProgress(IEnumerable<Command2Execute> commands2Execute)
        {
            _conversionCanceled = false;

            // disable all buttons except Debug-Mode, About, Usage, and Cancel
            TextBoxSourceFilename.IsEnabled = false;
            ButtonBrowseSource.IsEnabled = false;
            TextBoxTargetFilename.IsEnabled = false;
            ButtonBrowseTarget.IsEnabled = false;
            ComboBoxFrameRate.IsEnabled = false;
            ButtonConvert.IsEnabled = false;
            ButtonAbout.IsEnabled = false;
            SpinnerBitRate.IsEnabled = false;
            CheckSlCompatibility.IsEnabled = false;
            CheckBoxKeyFrames.IsEnabled = false;
            CheckBoxMinimum.IsEnabled = false;
            CheckBoxNoFLicker.IsEnabled = false;
            TextBoxForcedWidth.IsEnabled = false;
            TextBoxForcedHeight.IsEnabled = false;
            CheckKeepAudio.IsEnabled = false;
            CheckOpenOutputDirectoryWhenDone.IsEnabled = false;

            foreach (var command2Execute in commands2Execute)
            {
                var processStartInfo = new ProcessStartInfo(command2Execute.Command, command2Execute.Parameters)
                {
                    WorkingDirectory = _workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    WindowStyle = ProcessWindowStyle.Normal,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };

                _runningProcess = new Process {StartInfo = processStartInfo};
                _runningProcess.OutputDataReceived += process_OutputDataReceived;
                _runningProcess.ErrorDataReceived += process_ErrorDataReceived;

                if (!File.Exists(processStartInfo.FileName))
                {
                    AddToLog($"Error: Executable not found: {processStartInfo.FileName}");
                    _conversionCanceled = true;
                    break;
                }

                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();

                // also capture output to local buffers so we can write a complete log file
                DataReceivedEventHandler outHandler = (s, e) => { if (e?.Data != null) sbOut.AppendLine(e.Data); };
                DataReceivedEventHandler errHandler = (s, e) => { if (e?.Data != null) sbErr.AppendLine(e.Data); };

                _runningProcess.OutputDataReceived += outHandler;
                _runningProcess.ErrorDataReceived += errHandler;

                try
                {
                    _runningProcess.Start();
                    _runningProcess.PriorityClass = ProcessPriorityClass.BelowNormal;

                    _runningProcess.BeginOutputReadLine();
                    _runningProcess.BeginErrorReadLine();
                }
                catch (Exception ex)
                {
                    AddToLog($"Error: Failed to start process {processStartInfo.FileName}: {ex.Message}");
                    _conversionCanceled = true;
                    // detach handlers
                    _runningProcess.OutputDataReceived -= outHandler;
                    _runningProcess.ErrorDataReceived -= errHandler;
                    break;
                }

                try
                {
                    while (!_runningProcess.WaitForExit(100))
                    {
                        ProcessUiTasks();
                        Thread.Sleep(100);
                    }

                    // Process finished for this command; write a detailed log
                    try
                    {
                        var exitCode = _runningProcess.HasExited ? _runningProcess.ExitCode : -1;
                        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        var logFile = Path.Combine(Path.GetTempPath(), $"NewFPSConverter_ffmpeg_{timestamp}.log");
                        var sb = new StringBuilder();
                        sb.AppendLine("Command: " + command2Execute.Command);
                        sb.AppendLine("Parameters: " + command2Execute.Parameters);
                        sb.AppendLine("ExitCode: " + exitCode);
                        sb.AppendLine("--- STDOUT ---");
                        sb.AppendLine(sbOut.ToString());
                        sb.AppendLine("--- STDERR ---");
                        sb.AppendLine(sbErr.ToString());
                        File.WriteAllText(logFile, sb.ToString(), Encoding.UTF8);
                        AddToLog("Process finished. ExitCode=" + exitCode + "; Log: " + logFile);
                    }
                    catch (Exception ex)
                    {
                        AddToLog("Warning: Could not write process log: " + ex.Message);
                    }
                    finally
                    {
                        // detach local handlers to avoid memory leaks
                        _runningProcess.OutputDataReceived -= outHandler;
                        _runningProcess.ErrorDataReceived -= errHandler;
                    }
                }
                catch
                {
                    _conversionCanceled = true;
                    break;
                }
            }

            ErrorCodes result = ErrorCodes.Ok;
            if (!_conversionCanceled)
            {
                var videoFilename = string.IsNullOrEmpty(_tempCompressionTargetFile) ? TargetFilename : _tempCompressionTargetFile;
                if(File.Exists(videoFilename))
                {
                    if (CreateXmlDescriptionFile(videoFilename))
                    {
                        if (CreateXmlFrameInfoFile(videoFilename))
                        {
                            var completed = true;
                            if (!string.IsNullOrEmpty(_tempCompressionTargetFile))
                            {
                                var folder = Path.GetDirectoryName(videoFilename);
                                if (!string.IsNullOrEmpty(folder))
                                {
                                    completed = CreatePackedVideoFile(folder, Path.GetFileName(videoFilename));
                                }
                            }

                            if (completed)
                            {
                                UpdateProgress(100);
                                AddToLog("Conversion done!");
                                OpenOutputDirectoryIfRequested(TargetFilename);
                            }
                        }
                    }
                }
                else
                {
                    result = ErrorCodes.Unknown;
                    AddToLog("Error: Conversion failed.");
                }

                CleanUp();

                if (_isCommandLineMode && Application.Current != null)
                {
                    Application.Current.Shutdown((int)result);
                }
            }

            // re-enable all UI elements that are disabled during conversion
            TextBoxSourceFilename.IsEnabled = true;
            ButtonBrowseSource.IsEnabled = true;
            TextBoxTargetFilename.IsEnabled = true;
            ButtonBrowseTarget.IsEnabled = true;
            ComboBoxFrameRate.IsEnabled = true;
            ButtonConvert.IsEnabled = true;
            ButtonAbout.IsEnabled = true;
            SpinnerBitRate.IsEnabled = true;
            CheckBoxMinimum.IsEnabled = true;
            CheckKeepAudio.IsEnabled = true;
            CheckOpenOutputDirectoryWhenDone.IsEnabled = true;
            UpdateSlCompatibilityControlState(true);
        }

        /// <summary>
        ///     Creates the XML description file.
        /// </summary>
        /// <returns><c>true</c> if no exception when creating XML file, <c>false</c> otherwise.</returns>
        private bool CreateXmlDescriptionFile(string videoFilename)
        {
            try
            {
                int width, height;
                if (ForcedWidth != 0 && ForcedHeight != 0)
                {
                    width = ForcedWidth;
                    height = ForcedHeight;
                }
                else
                {
                    width = OriginalWidth;
                    height = OriginalHeight;
                }

                var doc = new XDocument(new XDeclaration("1.0", "utf-8", "yes"),
                    new XElement("VideoInfo", new XAttribute(XNamespace.Xmlns + "xsi", "http://www.w3.org/2001/XMLSchema-instance"), new XAttribute(XNamespace.Xmlns + "xsd", "http://www.w3.org/2001/XMLSchema"),
                        new XElement("InputFilename", SourceFilename),
                        new XElement("OutputFilename", string.IsNullOrEmpty(_tempCompressionTargetFile) ? videoFilename : Path.GetFileName(videoFilename)),
                        new XElement("Duration", OriginalDuration.ToString("F8", CultureInfo.InvariantCulture)),
                        new XElement("Width", width),
                        new XElement("Height", height),
                        new XElement("Framerate", TargetFrameRate.ToString("F8", CultureInfo.InvariantCulture)),
                        new XElement("OriginalWidth", OriginalWidth),
                        new XElement("OriginalHeight", OriginalHeight),
                        new XElement("OriginalFramerate", OriginalFramerate.ToString("F8", CultureInfo.InvariantCulture))));

                var xmlDestFilename = videoFilename + ".desc.xml";
                doc.Save(xmlDestFilename);

                AddToLog("XML description file written!");

                return true;
            }
            catch (Exception)
            {
                AddToLog("Error: XML description file cannot be written!");
            }
            return false;
        }

        /// <summary>
        ///     Handles the ErrorDataReceived event of the process control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataReceivedEventArgs" /> instance containing the event data.</param>
        private void process_ErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data == null)
            {
                return;
            }

            if (DebugMode)
            {
                AddToLog("Encoder:  " + e.Data);
            }

            HandleFFmpegOutput(e.Data);
        }

        /// <summary>
        ///     Handles the OutputDataReceived event of the process control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DataReceivedEventArgs" /> instance containing the event data.</param>
        private void process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (e?.Data == null)
            {
                return;
            }

            if (DebugMode)
            {
                AddToLog("Encoder:  " + e.Data);
            }

            HandleFFmpegOutput(e.Data);
        }

        /// <summary>
        ///     Handles the FFmpeg output.
        /// </summary>
        /// <param name="message">The message.</param>
        private void HandleFFmpegOutput(string message)
        {
            if (_progressbarInitialized && OriginalDuration > 0)
            {
                var posFrame = message.IndexOf("frame=", StringComparison.Ordinal);

                if (posFrame >= 0)
                {
                    posFrame += "frame=".Length;

                    var posEnd = posFrame + 1;
                    while (char.IsDigit(message[posEnd]) || char.IsWhiteSpace(message[posEnd])) posEnd++;

                    var frameCountAsString = message.Substring(posFrame, posEnd - posFrame).Trim();
                    long.TryParse(frameCountAsString, out _outputFrameCount);
                }
                
                var posTime = message.IndexOf("time=", StringComparison.Ordinal);

                if (posTime >= 0)
                {
                    posTime += "time=".Length;

                    var seconds = 0.0;

                    try
                    {
                        seconds += 60.0*60.0*long.Parse(message.Substring(posTime, 2));
                        seconds += 60.0*long.Parse(message.Substring(posTime + 3, 2));
                        seconds += long.Parse(message.Substring(posTime + 6, 2));
                        seconds += 0.01*long.Parse(message.Substring(posTime + 9, 2));

                        var percentage = seconds*100.0/OriginalDuration;
                        UpdateProgress(percentage);
                    }
                    catch (Exception)
                    {
                        _progressbarInitialized = false;
                    }
                }
            }
        }

        private void UpdateProgress(double percentage)
        {
            Application.Current.Dispatcher.BeginInvoke(
                (Action) (() =>
                {
                    if (_progressbarInitialized)
                    {
                        ProgressBarConversion.Value = percentage;
                    }
                    UpdateHeader(percentage);
                }));

        }

        /// <summary>
        ///     Updates the header.
        /// </summary>
        /// <param name="percentage">The percentage.</param>
        private void UpdateHeader(double? percentage = null)
        {
            var newHeader = "NewFPS Converter";

            if (!string.IsNullOrEmpty(SourceFilename))
            {
                newHeader += " - " + Path.GetFileName(SourceFilename);
            }

            if (percentage.HasValue)
            {
                newHeader += " - " + percentage.Value.ToString("0.00", CultureInfo.InvariantCulture) + @"%";
            }

            Title = newHeader;
        }

        /// <summary>
        ///     Handles the Click event of the Button Cancel control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            StopAnyRunningProcess();
            CleanUp();

            _conversionCanceled = true;

            if (_isCommandLineMode)
            {
                Application.Current.Shutdown((int) ErrorCodes.Canceled);
            }
        }

        /// <summary>
        ///     Stops any running process.
        /// </summary>
        private void StopAnyRunningProcess()
        {
            _batchCancellationTokenSource?.Cancel();

            foreach (var process in _batchProcesses)
            {
                TryKillProcess(process);
            }

            if (null != _runningProcess)
            {
                TryKillProcess(_runningProcess);
            }
        }

        private void TryKillProcess(Process process)
        {
            try
            {
                _ = process.Id;
                if (process.HasExited)
                {
                    return;
                }

                process.Kill();
                AddToLog("Conversion process was stopped!");
            }
            catch (InvalidOperationException)
            {
                // Process object was already detached or never started.
            }
            catch (Exception e)
            {
                AddToLog("Error: Exception when trying to stop conversion process: " + e.Message);
            }
        }

        /// <summary>
        /// Deletes the file.
        /// </summary>
        /// <param name="path">Name of the file/folder to delete.</param>
        private void DeleteFileOrFolder(string path)
        {
            var fileExists = File.Exists(path);
            var folderExists = Directory.Exists(path);
            if (!string.IsNullOrEmpty(path) && (fileExists || folderExists))
            {
                for (var i = 0; i < 10; i++)
                    try
                    {
                        AddToLog("Trying to delete: " + path);

                        if (fileExists)
                        {
                            File.Delete(path);
                        }
                        if (folderExists)
                        {
                            Directory.Delete(path, true);
                        }

                        AddToLog("Deleted: " + path);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (i > 0)
                        {
                            AddToLog("Error: Exception when trying to clean up: " + e.Message);
                        }

                        Thread.Sleep(1000);
                    }
            }
        }

        /// <summary>
        ///     Cleans up.
        /// </summary>
        private void CleanUp()
        {
            if(!string.IsNullOrEmpty(_tempVideoFile))
            {
                DeleteFileOrFolder(_tempVideoFile);
                _tempVideoFile = string.Empty;
            }

            if(!string.IsNullOrEmpty(_tempAudioFile))
            {
                DeleteFileOrFolder(_tempAudioFile);
                _tempAudioFile = string.Empty;
            }

            // Only delete _tempAvsFile if it's not the SourceFilename (it's no longer created, so it should equal SourceFilename)
            if(!string.IsNullOrEmpty(_tempAvsFile) && _tempAvsFile != SourceFilename)
            {
                DeleteFileOrFolder(_tempAvsFile);
                _tempAvsFile = string.Empty;
            }

            if(!string.IsNullOrEmpty(_tempCompressionTargetFile))
            {
                var folder = Path.GetDirectoryName(_tempCompressionTargetFile);
                if (!string.IsNullOrEmpty(folder))
                {
                    DeleteFileOrFolder(folder);
                }
                _tempCompressionTargetFile = string.Empty;
            }

            var indexFile = SourceFilename + ".ffindex";
            DeleteFileOrFolder(indexFile);
        }

        /// <summary>
        ///     Adds message to ListBox asynchronous.
        /// </summary>
        /// <param name="message">The message.</param>
        private void AddToListBoxAsync(string message)
        {
            if (Application.Current != null)
            {
                Application.Current.Dispatcher.BeginInvoke(
                    (Action) (() =>
                    {
                        ListBoxReport.Items.Add(message);
                        ListBoxReport.SelectedIndex = ListBoxReport.Items.Count - 1;
                        ListBoxReport.ScrollIntoView(ListBoxReport.SelectedItem);
                    }));
            }
        }

        /// <summary>
        ///     Adds message to log.
        /// </summary>
        /// <param name="message">The message.</param>
        private void AddToLog(string message)
        {
            if (_isCommandLineMode)
            {
                Console.WriteLine(message);
            }

            AddToListBoxAsync(message);
            Trace.WriteLine(message);
        }

        /// <summary>
        ///     Handles the OnClosing event of the MainWindow control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="CancelEventArgs" /> instance containing the event data.</param>
        private void MainWindow_OnClosing(object sender, CancelEventArgs e)
        {
            StopAnyRunningProcess();
            CleanUp();

            _conversionCanceled = true;

            if (_cmdLineOpts != null && _isCommandLineMode)
            {
                Application.Current.Shutdown((int) ErrorCodes.Canceled);
            }
        }

        /// <summary>
        ///     Shows the license.
        /// </summary>
        private static bool ShowLicense()
        {
            var newWindow = new AboutWindow();
            newWindow.ShowDialog();

            return newWindow.DialogResult.HasValue && newWindow.DialogResult.Value;
        }

        /// <summary>
        ///     Handles the Click event of the ButtonAbout control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="RoutedEventArgs" /> instance containing the event data.</param>
        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            var newWindow = new AboutWindow();
            newWindow.ShowDialog();

            if (!newWindow.DialogResult.HasValue || !newWindow.DialogResult.Value)
            {
                // remove acceptance of license
                var keySoftware = Registry.CurrentUser.OpenSubKey("Software", true);
                var keyApplication = keySoftware?.CreateSubKey("NewFPSConverter");

                if (keyApplication?.GetValue("LicenseAccepted") != null)
                {
                    keyApplication.DeleteValue("LicenseAccepted");
                }

                Application.Current.Shutdown((int) ErrorCodes.Canceled);
            }
        }

        private void ButtonUsage_OnClick(object sender, RoutedEventArgs e)
        {
            var newWindow = new UsageWindow();
            newWindow.ShowDialog();
        }

        private void ButtonCopyLog_Click(object sender, RoutedEventArgs e)
        {
            var logText = string.Join(
                Environment.NewLine,
                ListBoxReport.Items.Cast<object>().Select(item => item?.ToString() ?? string.Empty));

            if (string.IsNullOrWhiteSpace(logText))
            {
                AddToLog("Log output is empty.");
                return;
            }

            try
            {
                Clipboard.SetText(logText);
                AddToLog("Log output copied to clipboard.");
            }
            catch (Exception ex)
            {
                AddToLog("Error: Could not copy log output to clipboard: " + ex.Message);
            }
        }

        /// <summary>
        /// Store all video meta files into a packed single file
        /// </summary>
        private bool CreatePackedVideoFile(string folderPath, string searchPattern)
        {
            try
            {
                var overwriteFile = _replaceTargetFile || _isCommandLineMode && _cmdLineOpts.Overwrite;
                if (overwriteFile && File.Exists(TargetFilename))
                {
                    DeleteFileOrFolder(TargetFilename);
                }

                var updateExistingArchive = File.Exists(TargetFilename);
                using (var archive = updateExistingArchive
                    ? ZipFile.Open(TargetFilename, ZipArchiveMode.Update)
                    : ZipFile.Open(TargetFilename, ZipArchiveMode.Create))
                {
                    foreach (var tmpFile in Directory.EnumerateFiles(folderPath, searchPattern + "*"))
                    {
                        var entryName = Path.GetFileName(tmpFile);
                        if (updateExistingArchive)
                        {
                            var existingEntry = archive.GetEntry(entryName);
                            existingEntry?.Delete();
                        }

                        archive.CreateEntryFromFile(tmpFile, entryName, CompressionLevel.NoCompression);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                AddToLog("Error: Packed target file " + TargetFilename + " cannot be written: " + ex.Message);
            }

            return false;
        }

        /// <summary>
        ///     Creates the XML frame information file.
        /// </summary>
        /// <returns><c>true</c> if no exception when creating output <c>false</c> otherwise.</returns>
        private bool CreateXmlFrameInfoFile(string videoFilename)
        {
            var command = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ExecutableFFprobe);
            var output = ExecuteAndGetOutput(command, string.Format(ExecutableFFprobeFramesInfoParameters, videoFilename));

            if (string.IsNullOrEmpty(output))
            {
                AddToLog("Error: Frame analysis failed for " + videoFilename + "!");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.TargetFileCorrupt);
                }

                return false;
            }

            var rgx = new Regex("frames\\.frame\\.\\d+\\.pkt_size=\"(\\d+)\"");
            var allMatches = rgx.Matches(output);

            var sb = new StringBuilder();

            for (var i = 0; i < allMatches.Count; ++i)
            {
                sb.Append("<Frame length=");
                sb.Append(allMatches[i].Groups[1].Value);
                sb.Append(" />");
                sb.Append(Environment.NewLine);
            }

            try
            {
                var xmlDestFilename = videoFilename + ".xml";
                File.WriteAllText(xmlDestFilename, sb.ToString(), Encoding.UTF8);

                AddToLog("XML frame info file written!");

                return true;
            }
            catch (Exception)
            {
                AddToLog("Error: Frame analysis failed for " + videoFilename + "!");

                if (_isCommandLineMode)
                {
                    Application.Current.Shutdown((int) ErrorCodes.TargetFileCorrupt);
                }
            }

            return false;
        }

        /// <summary>
        ///     Handles the PreviewDropSourceFilename event of the TextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragEventArgs" /> instance containing the event data.</param>
        private void TextBox_PreviewDropSourceFilename(object sender, DragEventArgs e)
        {
            var text = e.Data.GetData(DataFormats.FileDrop);

            SourceFilename = $"{((string[]) text)?[0]}";
            GetVideoFileInformationAndUpdateFramerRate();
        }

        /// <summary>
        ///     Handles the PreviewDropTargetFilename event of the TextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragEventArgs" /> instance containing the event data.</param>
        private void TextBox_PreviewDropTargetFilename(object sender, DragEventArgs e)
        {
            var text = e.Data.GetData(DataFormats.FileDrop);

            TargetFilename = $"{((string[]) text)?[0]}";
        }

        /// <summary>
        ///     Handles the PreviewDragEnter event of the TextBox control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="DragEventArgs" /> instance containing the event data.</param>
        private void TextBox_PreviewDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
        }

        /// <summary>
        /// Calculates the bit rate:
        /// Kush Gauge bit rate calculation
        /// motion factor can be 1 (low), 2 (medium) or 4 (high motion)
        /// frame width * frame height * frame rate * motion factor * 0.07 /1000 = Kbps
        /// </summary>
        /// <param name="width">The width.</param>
        /// <param name="height">The height.</param>
        /// <param name="frameRate">The frame rate.</param>
        /// <returns>Bit rate in KBits (int)</returns>
        private static int CalculateBitRate(int width, int height, double frameRate)
        {
            const int motionFactor = 4;
            var bitRate = width * height * frameRate * 0.07 * motionFactor;
            bitRate /= 1000;  // convert the target bit rate to kilobits per second

            return 500 * (int)((bitRate + 500) / 500);
        }

        /// <summary>
        /// Patches the frame count in ivf file.
        /// </summary>
        private void PatchFrameCountInIvfFile()
        {
            try
            {
                using (var fs = File.Open(TargetFilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    //bytes 0-3    signature: 'DKIF'
                    //bytes 4-5    version (should be 0)
                    //bytes 6-7    length of header in bytes
                    //bytes 8-11   codec FourCC (e.g., 'VP80')
                    //bytes 12-13  width in pixels
                    //bytes 14-15  height in pixels
                    //bytes 16-19  frame rate
                    //bytes 20-23  time scale
                    //bytes 24-27  number of frames in file (*) 
                    //bytes 28-31  unused
                    var numberOfFrames = new byte[4];

                    fs.Seek(24, SeekOrigin.Begin);
                    fs.ReadExactly(numberOfFrames);

                    if (numberOfFrames[0] == 0 && numberOfFrames[1] == 0 && numberOfFrames[2] == 0 && numberOfFrames[3] == 0)
                    {
                        // the frame count was not set, set it now
                        numberOfFrames[0] = (byte)(_outputFrameCount & 0xFF);
                        numberOfFrames[1] = (byte)((_outputFrameCount >> 8) & 0xFF);
                        numberOfFrames[2] = (byte)((_outputFrameCount >> 16) & 0xFF);
                        numberOfFrames[3] = (byte)((_outputFrameCount >> 24) & 0xFF);

                        fs.Seek(24, SeekOrigin.Begin);
                        fs.Write(numberOfFrames, 0, 4);
                    }
                }
            }
            catch (Exception)
            {
                AddToLog("Error: Cannot patch target file " + TargetFilename + "!");
            }
        }

        /// <summary>
        /// Checks whether the specified file is a valid ZIP package.
        /// </summary>
        /// <param name="filename">The filename.</param>
        private bool IsZipFile(string filename)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    return false;
                }

                using var fileStream = File.OpenRead(filename);
                using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: true);
                return archive.Entries.Count > 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if a package contains file a specific entry.
        /// </summary>
        /// <param name="filename">The filename.</param>
        /// <param name="entry">The entry to check.</param>
        private bool PackageContainsFile(string filename, string entry)
        {
            try
            {
                if (!File.Exists(filename))
                {
                    return false;
                }

                using var archive = ZipFile.OpenRead(filename);
                return archive.Entries.Any(e => string.Equals(e.FullName, entry, StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception)
            {
                return false;
            }
        }

        private struct Command2Execute
        {
            public Command2Execute(string command, string parameter)
            {
                Command = command;
                Parameters = parameter;
            }

            public readonly string Command;
            public readonly string Parameters;
        }

        private readonly record struct VideoInfo(double Framerate, int Width, int Height, double Duration);

        private sealed class BatchSourceFile
        {
            public BatchSourceFile(string filePath)
            {
                FilePath = filePath;
                FileName = Path.GetFileName(filePath);
            }

            public string FilePath { get; }

            public string FileName { get; }

            public string AnalysisText { get; set; } = "Analysis pending";

            public VideoInfo? VideoInfo { get; set; }

            public bool IsSelected { get; set; } = true;
        }

        private bool _disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    _runningProcess.Dispose();
                }

                _disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
    }
