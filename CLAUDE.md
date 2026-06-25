# DigitAnalyzerSTR — Claude Directive

## What this project is

**DigitAnalyzerSTR** is a Windows desktop application (WinForms, .NET 10) that reads numeric data off industrial process-control displays recorded as `.mp4` video files, and exports the values to CSV. It requires zero user configuration per tool or screen layout — the AI reads whatever it sees.

Owner: David Strickland — DCSS Web Development LLC  
Stack: C# / .NET 10 / WinForms + Python 3.12 + Moondream2 vision model + FFmpeg

---

## Architecture overview

```
DigitAnalyzerSTR\
    Program.cs              Entry point — FFmpeg path bootstrap, WinForms startup
    MainForm.cs             UI + batch processing orchestration
    MainForm.Designer.cs    Designer-generated layout (do not hand-edit)
    MainForm.resx           Designer resources
    MoondreamEngine.cs      C# ↔ Python bridge; invokes moondream_infer.py as subprocess
    ModelDownloader.cs      Async pip installer with progress reporting
    Appsettings.cs          Persisted user settings (settings.json next to exe)
    moondream_infer.py.txt  Python inference script (shipped as .txt, copied to .py at runtime)
    setup_moondream.bat     One-time pip installer helper
    ffmpeg\bin\
        ffmpeg.exe
        ffprobe.exe
```

### Data flow

```
.mp4 video
  → FFMpegCore extracts frame as temp PNG at each sample interval
  → First frame: MoondreamEngine.DiscoverColumns() → calls Python in "discover" mode
      → moondream_infer.py → Moondream API → returns {label: value} JSON
      → Column names extracted from JSON keys
  → All frames: MoondreamEngine.AnalyzeFrame(imagePath, knownColumns) → "read" mode
      → Returns {label: value} for known columns
  → Row appended to CSV immediately (streaming, never buffered)
  → Temp PNG deleted
```

### Python subprocess pattern

`MoondreamEngine` splits Python invocation strings like `"py -3.12"` into exe + prefix args.  
Python candidates are tried in order: `py -3.12`, `py -3.11`, `py -3.10`, `py -3.9`, `python`, `python3`.  
The inference script is shipped as `moondream_infer.py.txt` to prevent Visual Studio treating it as a project file; it is copied to `.py` on each startup (always overwrite so updates propagate).

### Key dependencies

| Package | Purpose |
|---|---|
| FFMpegCore 5.4.0 | Video frame extraction |
| Newtonsoft.Json 13.0.4 | JSON parsing |
| moondream (Python, pip) | Vision AI inference via cloud API |
| Pillow (Python, pip) | Image loading for inference script |
| PyTorch (Python, pip) | ML backend pulled in by moondream |

---

## Runtime requirements (non-negotiable)

- **Windows 10/11 64-bit only** — WinForms, no cross-platform path
- **.NET 10** (`net10.0-windows`, `UseWindowsForms`)
- **Python 3.10–3.12** — Python 3.13+ is NOT supported by moondream's deps
- **FFmpeg + ffprobe** both in `ffmpeg\bin\` next to the exe
- **Moondream API key** — stored in `moondream_infer.py.txt` at the `api_key=` line

---

## Design constraints — preserve these

1. **No hardcoded layouts** — the entire value proposition is zero-config field discovery. Never add code that maps pixel regions, field names, or screen coordinates to specific tools.
2. **Streaming CSV writes** — rows are flushed immediately as frames are processed, never buffered. This is intentional for long-video safety.
3. **Timestamped output filenames** — `digitReader_YYYY-MM-DD_HH-mm-ss.csv`. No overwriting.
4. **Dark industrial UI theme** — the UI targets a manufacturing-floor aesthetic. Keep controls dark, professional, and minimal.
5. **Settings next to exe** — `settings.json` lives at `AppDomain.CurrentDomain.BaseDirectory`, not in AppData. Intentional for portable/lab deployments.
6. **Python script as .txt** — `moondream_infer.py.txt` must stay `.txt` in the project. The copy-to-`.py` pattern in `MoondreamEngine.EnsureScriptExtracted()` is intentional.

---

## Build & run

```powershell
# Build
dotnet build DigitAnalyzerSTR\DigitAnalyzerSTR.csproj

# Publish self-contained (typical for distribution)
dotnet publish DigitAnalyzerSTR\DigitAnalyzerSTR.csproj -c Release

# Run (Debug)
dotnet run --project DigitAnalyzerSTR\DigitAnalyzerSTR.csproj
```

FFmpeg binaries are set to `CopyToOutputDirectory: PreserveNewest` in the `.csproj` — they will be in `bin\Debug\net10.0-windows\ffmpeg\bin\` after build.

---

## Extending this project

### Adding a new output format (e.g., JSON, Excel)

- Processing and CSV writing happen in `MainForm.cs`
- `MoondreamEngine.AnalyzeFrame()` returns `Dictionary<string, string>` — pass that to any serializer
- Keep streaming (write per row, not per batch)

### Changing the inference model

- All model interaction is in `moondream_infer.py.txt`
- The C# side only cares that the script accepts `<image_path> discover` or `<image_path> read <json_columns>` and prints a JSON object to stdout
- To swap models: rewrite the Python script, keep the CLI interface identical

### Changing the sample interval range

- UI interval picker is in `MainForm.cs` / `MainForm.Designer.cs`
- Default is 10 seconds; range is 5–60 in 5-second increments
- `AppSettings.IntervalSeconds` persists the value

### Adding GPU support

- PyTorch/moondream will use CUDA automatically if available
- No C# changes needed; the Python script handles device selection

---

## Common pitfalls

- **Python 3.13+** breaks moondream dependencies. Always test with 3.12.
- **Missing ffprobe.exe** — a common setup error. FFMpegCore requires both `ffmpeg.exe` and `ffprobe.exe`. They are separate binaries.
- **API key in .txt file** — `moondream_infer.py.txt` is the file users edit. The `.py` is generated at runtime and should not be committed or hand-edited.
- **First frame is blank** — video must have the process display visible within the first 2 seconds. The discovery column map is fixed for the entire video once set.
- **Column map set once per video** — if a display changes significantly mid-video, results will be incomplete. No dynamic re-discovery during a video.

---

## What NOT to add without explicit direction

- No cloud storage, telemetry, or auto-update logic
- No GUI for editing the Python inference script — users edit the `.txt` directly
- No per-tool configuration files or screen layout templates — that defeats the purpose
- No database — CSV is the intentional output format for spreadsheet/lab consumption
