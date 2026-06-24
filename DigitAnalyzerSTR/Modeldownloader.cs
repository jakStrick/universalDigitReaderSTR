using System.Diagnostics;
using System.Text;

namespace DigitAnalyzerSTR
{
    public static class ModelDownloader
    {
        public static async Task DownloadAsync(
            IProgress<DownloadProgress>? progress = null,
            CancellationToken ct = default)
        {
            progress?.Report(new DownloadProgress
            {
                FileIndex = 1,
                FileCount = 2,
                Percent = 0,
                Message = "Checking Python installation..."
            });

            string python = await FindPythonAsync();
            if (string.IsNullOrEmpty(python))
                throw new InvalidOperationException(
                    "Python 3.12 not found. Install from https://python.org and check 'Add Python to PATH'.");

            progress?.Report(new DownloadProgress
            {
                FileIndex = 1,
                FileCount = 2,
                Percent = 100,
                Message = $"Python found: {python}"
            });

            progress?.Report(new DownloadProgress
            {
                FileIndex = 2,
                FileCount = 2,
                Percent = 0,
                Message = "Running pip install moondream..."
            });

            await RunPipAsync(python, ct, progress);

            progress?.Report(new DownloadProgress
            {
                FileIndex = 2,
                FileCount = 2,
                Percent = 100,
                Message = "moondream installed successfully."
            });
        }

        // ---------------------------------------------------------------
        // Find a Python that has moondream installed.
        // Prefers py -3.12 since moondream's dependencies require it.
        // ---------------------------------------------------------------
        private static async Task<string> FindPythonAsync()
        {
            var candidates = new[] { "py -3.12", "py -3.11", "py -3.10", "python", "python3" };

            foreach (var candidate in candidates)
            {
                try
                {
                    var parts = candidate.Split(' ', 2);
                    string exe = parts[0];
                    string prefix = parts.Length > 1 ? parts[1] + " " : "";

                    var psi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = prefix + "--version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    if (proc == null) continue;
                    var stdout = proc.StandardOutput.ReadToEndAsync();
                    var stderr = proc.StandardError.ReadToEndAsync();
                    await Task.WhenAll(stdout, stderr);
                    await proc.WaitForExitAsync();
                    if (proc.ExitCode == 0) return candidate;
                }
                catch { }
            }
            return string.Empty;
        }

        // ---------------------------------------------------------------
        // Run pip install using the found Python
        // ---------------------------------------------------------------
        private static async Task RunPipAsync(string python,
            CancellationToken ct, IProgress<DownloadProgress>? progress)
        {
            // Split "py -3.12" into exe="py" args="-3.12 -u -m pip ..."
            var parts = python.Split(' ', 2);
            string exe = parts[0];
            string prefix = parts.Length > 1 ? parts[1] + " " : "";

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = prefix + "-u -m pip install moondream --upgrade --no-warn-script-location",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start pip.");

            var outputLines = new List<string>();
            var errorLines = new List<string>();

            var stdoutTask = ReadStreamAsync(proc.StandardOutput, line =>
            {
                outputLines.Add(line);
                if (!string.IsNullOrWhiteSpace(line))
                    progress?.Report(new DownloadProgress
                    {
                        FileIndex = 2,
                        FileCount = 2,
                        Percent = 50,
                        Message = line.Length > 100 ? line[..100] : line
                    });
            }, ct);

            var stderrTask = ReadStreamAsync(proc.StandardError, line =>
            {
                errorLines.Add(line);
                if (!string.IsNullOrWhiteSpace(line))
                    progress?.Report(new DownloadProgress
                    {
                        FileIndex = 2,
                        FileCount = 2,
                        Percent = 50,
                        Message = $"[err] {(line.Length > 95 ? line[..95] : line)}"
                    });
            }, ct);

            await Task.WhenAll(stdoutTask, stderrTask);
            await proc.WaitForExitAsync(ct);

            if (proc.ExitCode != 0)
            {
                string logPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "DigitAnalyzerSTR_pip_error.txt");

                var sb = new StringBuilder();
                sb.AppendLine($"pip exit code: {proc.ExitCode}");
                sb.AppendLine($"Command: {exe} {prefix}-u -m pip install moondream --upgrade");
                sb.AppendLine();
                sb.AppendLine("=== STDOUT ===");
                foreach (var l in outputLines) sb.AppendLine(l);
                sb.AppendLine();
                sb.AppendLine("=== STDERR ===");
                foreach (var l in errorLines) sb.AppendLine(l);

                await File.WriteAllTextAsync(logPath, sb.ToString(), ct);

                throw new InvalidOperationException(
                    $"pip failed (exit code {proc.ExitCode}). " +
                    $"Full error log saved to Desktop: DigitAnalyzerSTR_pip_error.txt");
            }
        }

        private static async Task ReadStreamAsync(
            System.IO.StreamReader reader,
            Action<string> onLine,
            CancellationToken ct)
        {
            try
            {
                string? line;
                while ((line = await reader.ReadLineAsync(ct)) != null)
                    onLine(line);
            }
            catch (OperationCanceledException) { }
            catch { }
        }
    }

    public class DownloadProgress
    {
        public string FileName { get; init; } = "";
        public int FileIndex { get; init; }
        public int FileCount { get; init; }
        public int Percent { get; init; }
        public string Message { get; init; } = "";
        public int Overall => (int)(((FileIndex - 1) * 100.0 + Percent) / FileCount);
    }
}