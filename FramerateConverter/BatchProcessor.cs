using System.Collections.Concurrent;

namespace NewFPSConverter;

/// <summary>
/// Handles batch processing of multiple video files with parallel execution
/// </summary>
public class BatchProcessor
{
    private readonly ConcurrentQueue<string> _fileQueue = new();
    private readonly int _maxParallelJobs;
    private readonly Action<string>? _onProgress;
    private int _completedJobs;
    private int _totalJobs;

    public event EventHandler<string>? FileProcessed;
    public event EventHandler<string>? FileError;

    public BatchProcessor(int maxParallelJobs = 0, Action<string>? onProgress = null)
    {
        // Auto-detect: use CPU core count, but max 4 to avoid overload
        _maxParallelJobs = maxParallelJobs <= 0
            ? Math.Min(Environment.ProcessorCount / 2, 4)
            : Math.Min(maxParallelJobs, Environment.ProcessorCount);

        _onProgress = onProgress;
    }

    /// <summary>
    /// Enqueues video files from a directory
    /// </summary>
    public int EnqueueDirectory(string directoryPath, string searchPattern = "*.mp4")
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var videoExtensions = new[] { ".mp4", ".avi", ".mkv", ".mov", ".flv", ".wmv", ".webm", ".m3u8" };
        var files = Directory.GetFiles(directoryPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => videoExtensions.Contains(Path.GetExtension(f).ToLower()))
            .ToList();

        foreach (var file in files)
        {
            _fileQueue.Enqueue(file);
        }

        _totalJobs = _fileQueue.Count;
        _completedJobs = 0;

        return _totalJobs;
    }

    /// <summary>
    /// Processes all queued files in parallel
    /// </summary>
    public async Task ProcessAllAsync(Func<string, Task<bool>> processFileFunc)
    {
        if (_fileQueue.IsEmpty)
            throw new InvalidOperationException("No files queued for processing");

        var tasks = new List<Task>();

        for (int i = 0; i < _maxParallelJobs; i++)
        {
            tasks.Add(ProcessWorkerAsync(processFileFunc));
        }

        await Task.WhenAll(tasks);
    }

    private async Task ProcessWorkerAsync(Func<string, Task<bool>> processFileFunc)
    {
        while (_fileQueue.TryDequeue(out var filePath))
        {
            try
            {
                _onProgress?.Invoke($"[{_completedJobs + 1}/{_totalJobs}] Processing: {Path.GetFileName(filePath)}");

                bool success = await processFileFunc(filePath);

                if (success)
                {
                    FileProcessed?.Invoke(this, filePath);
                }
                else
                {
                    FileError?.Invoke(this, filePath);
                }
            }
            catch (Exception ex)
            {
                FileError?.Invoke(this, $"{filePath} - Error: {ex.Message}");
            }
            finally
            {
                Interlocked.Increment(ref _completedJobs);
            }
        }
    }

    /// <summary>
    /// Gets the number of queued files
    /// </summary>
    public int GetQueuedFileCount()
    {
        return _fileQueue.Count;
    }

    /// <summary>
    /// Gets progress info
    /// </summary>
    public (int Completed, int Total) GetProgress()
    {
        return (_completedJobs, _totalJobs);
    }

    /// <summary>
    /// Gets optimal parallel job count based on CPU
    /// </summary>
    public static int GetOptimalJobCount()
    {
        return Math.Min(Environment.ProcessorCount / 2, 4);
    }
}
