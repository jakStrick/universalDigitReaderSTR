using Newtonsoft.Json;

namespace DigitAnalyzerSTR
{
    /// <summary>Persisted user settings — saved next to the executable.</summary>
    public class AppSettings
    {
        private static readonly string SettingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        public string VideoFolder { get; set; } = "";
        public string OutputFolder { get; set; } = "";
        public int IntervalSeconds { get; set; } = 10;

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                    return JsonConvert.DeserializeObject<AppSettings>(File.ReadAllText(SettingsPath))
                           ?? new AppSettings();
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try { File.WriteAllText(SettingsPath, JsonConvert.SerializeObject(this, Formatting.Indented)); }
            catch { }
        }
    }
}