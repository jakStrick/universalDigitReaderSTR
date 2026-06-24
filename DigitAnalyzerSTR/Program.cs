namespace DigitAnalyzerSTR
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.SystemAware);

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) =>
                MessageBox.Show($"Unexpected error: {e.Exception.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    MessageBox.Show($"Fatal error: {ex.Message}", "Fatal Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            // Ensure FFmpeg is findable
            var ffmpegPaths = new[]
            {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg", "bin"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin"),
                AppDomain.CurrentDomain.BaseDirectory
            };
            foreach (var p in ffmpegPaths)
                if (File.Exists(Path.Combine(p, "ffmpeg.exe")))
                { FFMpegCore.GlobalFFOptions.Configure(o => o.BinaryFolder = p); break; }

            Application.Run(new MainForm());
        }
    }
}