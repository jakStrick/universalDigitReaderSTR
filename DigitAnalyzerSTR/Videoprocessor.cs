using FFMpegCore;
using System.Globalization;
using System.Text;

namespace DigitAnalyzerSTR
{
    /// <summary>
    /// Processes a single video file end-to-end:
    ///   1. Extracts frames as PNG temp files at the chosen interval
    ///   2. Passes frame 1 to MoondreamEngine for column discovery
    ///   3. Passes every subsequent frame for value reading
    ///   4. Streams results to CSV as each frame completes
    /// </summary>
    public class VideoProcessor
    {
        private readonly MoondreamEngine _engine;
        private readonly string _videoPath;
        private readonly string _outputCsvPath;
        private readonly int _intervalSeconds;
        private readonly IProgress<VideoProcessorProgress>? _progress;
        private readonly CancellationToken _ct;

        public VideoProcessor(
            MoondreamEngine engine,
            string videoPath,
            string outputCsvPath,
            int intervalSeconds,
            IProgress<VideoProcessorProgress>? progress = null,
            CancellationToken ct = default)
        {
            _engine = engine;
            _videoPath = videoPath;
            _outputCsvPath = outputCsvPath;
            _intervalSeconds = intervalSeconds;
            _progress = progress;
            _ct = ct;
        }

        public async Task<ProcessResult> RunAsync()
        {
            try
            {
                var info = await FFProbe.AnalyseAsync(_videoPath, cancellationToken: _ct);
                double duration = info.VideoStreams.FirstOrDefault()?.Duration.TotalSeconds ?? 0;

                if (duration <= 0)
                    return ProcessResult.Fail("Could not determine video duration.");

                var seekTimes = new List<double>();
                for (double t = 2; t < duration; t += _intervalSeconds)
                    seekTimes.Add(t);

                if (seekTimes.Count == 0)
                    return ProcessResult.Fail("No frames to process.");

                Report("Analyzing first frame to discover data fields...", 0, seekTimes.Count);

                // --- Phase 1: extract first frame and discover columns ---
                string? firstFramePath = await ExtractFrameAsync(seekTimes[0]);
                if (firstFramePath == null)
                    return ProcessResult.Fail("Could not extract first frame.");

                List<string> columns;
                try
                {
                    columns = _engine.DiscoverColumns(firstFramePath);
                }
                finally
                {
                    TryDelete(firstFramePath);
                }

                if (columns.Count == 0)
                    return ProcessResult.Fail(
                        "No data fields found in first frame. Is this a process control display?");

                Report($"Found {columns.Count} data fields. Starting extraction...", 1, seekTimes.Count);

                // --- Phase 2: stream CSV ---
                Directory.CreateDirectory(Path.GetDirectoryName(_outputCsvPath)!);
                await using var writer = new StreamWriter(_outputCsvPath, false, Encoding.UTF8);

                // Header
                var headers = new List<string> { "filename", "timestamp_s", "wall_time" };
                headers.AddRange(columns);
                await writer.WriteLineAsync(CsvRow(headers));

                int rowsWritten = 0;

                for (int i = 0; i < seekTimes.Count; i++)
                {
                    if (_ct.IsCancellationRequested) break;

                    double t = seekTimes[i];
                    Report($"Frame {i + 1} of {seekTimes.Count}  ({FormatTime(t)})", i + 1, seekTimes.Count);

                    string? framePath = await ExtractFrameAsync(t);
                    if (framePath == null) continue;

                    Dictionary<string, string> values;
                    try
                    {
                        values = _engine.AnalyzeFrame(framePath, columns);
                    }
                    finally
                    {
                        TryDelete(framePath);
                    }

                    Report($"Frame {i + 1} of {seekTimes.Count}  ({FormatTime(t)})", i + 1, seekTimes.Count, values);

                    var row = new List<string>
                    {
                        Path.GetFileName(_videoPath),
                        t.ToString("F1", CultureInfo.InvariantCulture),
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                    };

                    foreach (var col in columns)
                        row.Add(values.TryGetValue(col, out var v) ? v : "?");

                    await writer.WriteLineAsync(CsvRow(row));
                    await writer.FlushAsync();
                    rowsWritten++;
                }

                return ProcessResult.Ok(rowsWritten, columns.Count, _outputCsvPath);
            }
            catch (OperationCanceledException) { return ProcessResult.Fail("Cancelled."); }
            catch (Exception ex) { return ProcessResult.Fail($"Error: {ex.Message}"); }
        }

        // ---------------------------------------------------------------
        // Frame extraction — Improvement #4: 2x upscale for better digit
        // recognition on small LCD displays. PNG temp file, caller deletes.
        // ---------------------------------------------------------------
        private async Task<string?> ExtractFrameAsync(double seekSeconds)
        {
            string tmp = Path.Combine(Path.GetTempPath(), $"dar_{Guid.NewGuid():N}.png");
            try
            {
                await FFMpegArguments
                    .FromFileInput(_videoPath)
                    .OutputToFile(tmp, true, opts => opts
                        .Seek(TimeSpan.FromSeconds(seekSeconds))
                        .WithFrameOutputCount(1)
                        .WithCustomArgument("-vf scale=iw*2:ih*2"))
                    .CancellableThrough(_ct)
                    .ProcessAsynchronously();

                return File.Exists(tmp) ? tmp : null;
            }
            catch
            {
                TryDelete(tmp);
                return null;
            }
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private void Report(string message, int current, int total, Dictionary<string, string>? values = null) =>
            _progress?.Report(new VideoProcessorProgress
            {
                Message = message,
                Current = current,
                Total = total,
                VideoName = Path.GetFileName(_videoPath),
                Values = values
            });

        private static string CsvRow(IEnumerable<string> fields) =>
            string.Join(",", fields.Select(f =>
            {
                string s = f ?? "";
                return (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                    ? $"\"{s.Replace("\"", "\"\"")}\""
                    : s;
            }));

        private static string FormatTime(double seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            return $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
        }

        private static void TryDelete(string? path)
        {
            if (path != null) try { File.Delete(path); } catch { }
        }
    }

    public class VideoProcessorProgress
    {
        public string VideoName { get; init; } = "";
        public string Message { get; init; } = "";
        public int Current { get; init; }
        public int Total { get; init; }
        public int Percent => Total > 0 ? Current * 100 / Total : 0;
        public Dictionary<string, string>? Values { get; init; }
    }

    public class ProcessResult
    {
        public bool Success { get; private init; }
        public string Error { get; private init; } = "";
        public int RowsWritten { get; private init; }
        public int Columns { get; private init; }
        public string OutputPath { get; private init; } = "";

        public static ProcessResult Ok(int rows, int cols, string path) =>
            new() { Success = true, RowsWritten = rows, Columns = cols, OutputPath = path };

        public static ProcessResult Fail(string error) =>
            new() { Success = false, Error = error };

        public override string ToString() => Success
            ? $"✓ {RowsWritten} rows, {Columns} columns → {Path.GetFileName(OutputPath)}"
            : $"✗ {Error}";
    }
}