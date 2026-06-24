using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;

namespace DigitAnalyzerSTR
{
    public class MoondreamEngine : IDisposable
    {
        private readonly string _pythonExe;   // e.g. "py -3.12" or "python"
        private readonly string _scriptPath;
        private bool _disposed;

        // Shipped as .py.txt to prevent Visual Studio compiling it.
        // Copied to .py at runtime by EnsureScriptExtracted().
        public static readonly string ScriptPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "moondream_infer.py");

        private static void EnsureScriptExtracted()
        {
            string txt = ScriptPath + ".txt";
            if (!File.Exists(txt)) return;
            // Always overwrite so updates to the .txt are picked up
            File.Copy(txt, ScriptPath, overwrite: true);
        }

        public MoondreamEngine()
        {
            EnsureScriptExtracted();
            _scriptPath = ScriptPath;
            _pythonExe = FindPython();

            if (string.IsNullOrEmpty(_pythonExe))
                throw new InvalidOperationException(
                    "Python with moondream not found. Please run setup_moondream.bat.");

            if (!File.Exists(_scriptPath))
                throw new InvalidOperationException(
                    $"Inference script not found: {_scriptPath}");

            ValidatePythonPackage();
        }

        // ---------------------------------------------------------------
        // Public API
        // ---------------------------------------------------------------
        public Dictionary<string, string> AnalyzeFrame(string imagePath,
            IReadOnlyList<string>? knownColumns = null)
        {
            string mode = (knownColumns == null || knownColumns.Count == 0)
                ? "discover" : "read";

            string args = mode == "discover"
                ? $"\"{_scriptPath}\" \"{imagePath}\" discover"
                : $"\"{_scriptPath}\" \"{imagePath}\" read \"{EscapeJson(knownColumns!)}\"";

            string output = RunPython(args);
            return ParseJson(output);
        }

        public List<string> DiscoverColumns(string imagePath)
        {
            var result = AnalyzeFrame(imagePath);
            return result
                .Where(kv => kv.Key != "error")
                .Select(kv => kv.Key)
                .OrderBy(k => k)
                .ToList();
        }

        // ---------------------------------------------------------------
        // Python execution — splits "py -3.12" into exe + prefix args
        // ---------------------------------------------------------------
        private string RunPython(string arguments, int timeoutMs = 120_000)
        {
            var parts = _pythonExe.Split(' ', 2);
            string exe = parts[0];
            string prefix = parts.Length > 1 ? parts[1] + " " : "";

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = prefix + arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            using var proc = Process.Start(psi)
                ?? throw new InvalidOperationException("Failed to start Python process.");

            string stdout = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(timeoutMs);
            return stdout.Trim();
        }

        private void ValidatePythonPackage()
        {
            string result = RunPython("-c \"import moondream; print('ok')\"", 15_000);
            if (!result.Contains("ok"))
                throw new InvalidOperationException(
                    "moondream Python package not installed. Please run setup_moondream.bat.");
        }

        // ---------------------------------------------------------------
        // Find Python with moondream installed — prefers py -3.12
        // ---------------------------------------------------------------
        private static string FindPython()
        {
            var candidates = new[]
            {
                ("py", "-3.12"),
                ("py", "-3.11"),
                ("py", "-3.10"),
                ("py", "-3.9"),
                ("python",  ""),
                ("python3", ""),
            };

            foreach (var (exe, ver) in candidates)
            {
                try
                {
                    // 1. Check Python version runs
                    string vArgs = string.IsNullOrEmpty(ver) ? "--version" : $"{ver} --version";
                    var vPsi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = vArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var vProc = Process.Start(vPsi);
                    vProc?.WaitForExit(3000);
                    if (vProc?.ExitCode != 0) continue;

                    // 2. Check moondream is importable
                    string iArgs = string.IsNullOrEmpty(ver)
                        ? "-c \"import moondream\""
                        : $"{ver} -c \"import moondream\"";
                    var iPsi = new ProcessStartInfo
                    {
                        FileName = exe,
                        Arguments = iArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var iProc = Process.Start(iPsi);
                    iProc?.WaitForExit(8000);
                    if (iProc?.ExitCode != 0) continue;

                    // Return full invocation string e.g. "py -3.12" or "python"
                    return string.IsNullOrEmpty(ver) ? exe : $"{exe} {ver}";
                }
                catch { }
            }

            return string.Empty;
        }

        // ---------------------------------------------------------------
        // IsModelReady — called on startup to check before InitEngine
        // ---------------------------------------------------------------
        public static bool IsModelReady()
        {
            EnsureScriptExtracted();
            var candidates = new[] { "py -3.12", "py -3.11", "py -3.10", "py -3.9", "python", "python3" };
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
                        Arguments = prefix + "-c \"import moondream\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var proc = Process.Start(psi);
                    proc?.WaitForExit(8000);
                    if (proc?.ExitCode == 0) return true;
                }
                catch { }
            }
            return false;
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------
        private static string EscapeJson(IReadOnlyList<string> columns)
        {
            string json = JsonConvert.SerializeObject(columns);
            return json.Replace("\"", "\\\"");
        }

        private static Dictionary<string, string> ParseJson(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return new Dictionary<string, string>();
            try
            {
                int start = raw.IndexOf('{');
                int end = raw.LastIndexOf('}') + 1;
                if (start < 0 || end <= start)
                    return new Dictionary<string, string>();

                var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(raw[start..end]);
                return parsed?.ToDictionary(
                    kv => kv.Key.Trim(),
                    kv => kv.Value?.ToString()?.Trim() ?? "?")
                    ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        public void Dispose()
        {
            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}