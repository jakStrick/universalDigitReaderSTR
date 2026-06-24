# DigitAnalyzerSTR

Universal process display video logger. Reads any industrial control display,
auto-discovers all labeled fields, exports time-series data to CSV.
No hardcoded layouts. No tool numbers. No configuration.

## Requirements

- Windows 10/11
- .NET 10 runtime
- Python 3.9+ (free from https://python.org)
- FFmpeg (place ffmpeg.exe in a `ffmpeg\bin` folder next to the .exe, or on PATH)

## First-time setup

1. Install Python from https://python.org
   - Check **"Add Python to PATH"** during installation
2. Run `setup_moondream.bat` (included next to the .exe)
   - This installs the moondream package via pip (~500MB download, one time)
3. Run `DigitAnalyzerSTR.exe`
   - If moondream isn't detected, the app will offer to install it for you

## How it works

1. Select a folder of `.mp4` videos
2. Select an output folder for CSV files
3. Choose a sample interval (5–60 seconds)
4. Click Start

The first frame of each video is analyzed by Moondream2 (local AI vision model)
to discover every labeled field on the display. Those field names become the CSV
column headers. Every subsequent sampled frame is read using those same columns.
One CSV file is written per video, named after the video file.

## Output CSV columns

| Column       | Description                          |
|-------------|--------------------------------------|
| filename    | Source video filename                |
| timestamp_s | Position in video (seconds)          |
| wall_time   | Clock time when frame was processed  |
| *field names* | Whatever labels appear on the display |

Unreadable values are logged as `?`.

# DigitAnalyzerSTR

> **Universal process display video logger powered by local AI vision.**
> Point it at a folder of `.mp4` recordings from any industrial tool, hit Start, and walk away.
> No configuration. No hardcoded screen layouts. No tool numbers. Just data.

---

## What it does

Industrial process control equipment records its display to video — but that data is locked inside the footage as pixels. DigitAnalyzerSTR unlocks it.

On the **first frame** of each video, the Moondream2 vision model reads the screen like a human would — finding every labeled numeric field, reading the label text directly off the display, and building a column map on the fly. Every subsequent sampled frame is read against that same column map and the values are streamed to a CSV file in real time.

The result: a clean, timestamped spreadsheet of every process parameter, automatically named and ready for analysis — with zero manual configuration per tool or per video.

---

## Features

- **Truly universal** — works on any process control display regardless of layout, tool type, screen resolution, or field arrangement. The AI reads what it sees; you don't tell it what to look for.
- **Auto-discovers column names** — field labels are read directly from the screen, so CSV headers match exactly what the display says.
- **Batch processing** — point it at a folder, process all videos unattended, sorted by date modified.
- **Adjustable sample interval** — 5 to 60 seconds in 5-second increments. Choose the right balance between data density and processing time.
- **Streaming CSV output** — rows are written as each frame is processed, not buffered in memory. Safe for long videos.
- **Timestamped output files** — each video produces a uniquely named `digitReader_YYYY-MM-DD_HH-mm-ss.csv`, no overwriting.
- **Persistent settings** — video folder, output folder, and interval are remembered between sessions.
- **Dark industrial UI** — clean, professional interface that doesn't get in the way.

---

## Requirements

| Requirement | Notes |
|---|---|
| Windows 10 / 11 | 64-bit |
| .NET 10 runtime | [Download here](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Python 3.10–3.12 | **Not 3.13/3.14** — use 3.12 for best compatibility. [python.org](https://python.org) |
| FFmpeg | Place `ffmpeg.exe` + `ffprobe.exe` in `ffmpeg\bin\` next to the `.exe` |
| Internet (first run) | moondream Python package download via pip (~500 MB) |
| Moondream API key | Free key from [moondream.ai](https://moondream.ai) |

**Minimum hardware:** 4 GB RAM, any modern CPU. No GPU required.
**Recommended:** 8+ GB RAM. GPU (NVIDIA Ampere or newer) dramatically speeds up inference.

---

## Installation

### 1. Install Python 3.12

Download and install Python 3.12 from [python.org](https://www.python.org/downloads/release/python-31210/).

> ⚠️ During installation, check **"Add Python to PATH"**. This is required.

### 2. Install the moondream package

Open PowerShell or Command Prompt and run:

```powershell
py -3.12 -m pip install moondream
```

This downloads the moondream inference package and all dependencies (~500 MB including PyTorch). Takes a few minutes. Only needed once.

### 3. Get a Moondream API key

Sign up for a free account at [moondream.ai](https://moondream.ai) and create an API key from your dashboard. Copy it — you'll need it in the next step.

### 4. Configure the API key

Open `moondream_infer.py.txt` (located next to `DigitAnalyzerSTR.exe`) and find this line:

```python
model = md.vl(api_key="YOUR_API_KEY_HERE")
```

Replace `YOUR_API_KEY_HERE` with your actual key. Save the file.

### 5. Install FFmpeg

Download the FFmpeg essentials build from [gyan.dev/ffmpeg/builds](https://www.gyan.dev/ffmpeg/builds/) and extract it. Place `ffmpeg.exe` and `ffprobe.exe` into a folder named `ffmpeg\bin\` next to `DigitAnalyzerSTR.exe`:

```
DigitAnalyzerSTR\
    DigitAnalyzerSTR.exe
    moondream_infer.py.txt
    ffmpeg\
        bin\
            ffmpeg.exe
            ffprobe.exe
```

### 6. Run DigitAnalyzerSTR

Launch `DigitAnalyzerSTR.exe`. The activity log should show:

```
Moondream2 model ready.
Inference engine initialised.
```

You're ready to process videos.

---

## Usage

### Basic workflow

1. **VIDEO FOLDER** — click `...` and select the folder containing your `.mp4` recordings
2. **OUTPUT FOLDER** — click `...` and select where CSV files should be saved
3. **SAMPLE INTERVAL** — choose how often to sample the video (every 5–60 seconds)
4. **Refresh Queue** — confirms how many videos were found, sorted by date modified
5. **▶ Start Processing** — begins batch processing

Processing runs one video at a time. The activity log shows real-time progress. Progress bars show current file and overall batch completion. After the batch completes, bars reset automatically after 2 seconds.

To stop mid-batch click **■ Stop**. Already-completed videos are saved.

### Output

Each video produces one CSV file named:

```
digitReader_2026-06-24_13-45-22.csv
```

Columns:

| Column | Description |
|---|---|
| `filename` | Source video filename |
| `timestamp_s` | Position in video where frame was sampled (seconds) |
| `wall_time` | Clock time when the frame was processed |
| *(field names)* | One column per labeled field found on the display |

Field names come directly from what is printed on the screen — `SiH4 flow`, `HFPwr`, `Temp`, `Wafer Number`, etc. If a value cannot be read, the cell contains `?`.

---

## How it works (technical)

```
Video file
    ↓
FFmpeg extracts frame at each interval as a temp PNG
    ↓
First frame only:
    moondream_infer.py runs in discover mode
    Moondream2 reads the screen → returns JSON {label: value}
    Column names extracted from JSON keys
    ↓
All frames:
    moondream_infer.py runs in read mode with known columns
    Moondream2 reads current values → returns JSON {label: value}
    ↓
Row written to CSV immediately (streaming, not buffered)
    ↓
Temp PNG deleted
```

The Python inference script (`moondream_infer.py`) is invoked as a subprocess for each frame. The C# application handles video extraction, CSV writing, UI, and orchestration. Moondream2 handles all image understanding — no hardcoded pixel coordinates, no screen layout assumptions, no training required.

---

## Troubleshooting

**"Model not found. Download required before processing."**
The app cannot find Python with moondream installed. Run `py -3.12 -c "import moondream; print('ok')"` in PowerShell to verify. If it fails, re-run the pip install step.

**"HTTP Error 401: Unauthorized"**
Your Moondream API key is missing or incorrect in `moondream_infer.py.txt`. Check that the key is pasted correctly with no extra spaces, and that your moondream.ai account is active.

**"File not found: ffprobe.exe"**
Both `ffmpeg.exe` and `ffprobe.exe` must be in `ffmpeg\bin\` next to the executable. Make sure both files are present — ffprobe is a separate binary, not bundled inside ffmpeg.exe.

**"No data fields found in first frame."**
The first sampled frame (at 2 seconds) may be blank, a loading screen, or a transition. Try a video that starts with the process display already visible, or increase the start offset. Also verify the frame looks correct by checking `dar_debug_frame.png` on your Desktop if debug mode was enabled.

**Processing is slow**
Each frame requires a cloud API call to Moondream. Increase the sample interval to reduce the number of frames processed. On a 30-minute video at 60-second intervals, that's ~30 API calls — very fast. At 5-second intervals it's ~360 calls.

**CSV columns look wrong or incomplete**
Moondream reads what it can see. If the display is partially obscured, overexposed, or the video quality is low, some fields may be missed or misread. The first-frame discovery sets the column map for the entire video — if the display changes significantly mid-video (different screen, different recipe), consider splitting the video first.

---

## Project structure

```
DigitAnalyzerSTR\
    DigitAnalyzerSTR.exe        Main application
    moondream_infer.py.txt      Python inference script (copied to .py at runtime)
    setup_moondream.bat         Helper script to install moondream via pip
    settings.json               Persisted user settings (auto-created on first run)
    ffmpeg\
        bin\
            ffmpeg.exe
            ffprobe.exe
```

---

## Dependencies

| Package | License | Purpose |
|---|---|---|
| FFMpegCore | MIT | Video frame extraction |
| Newtonsoft.Json | MIT | JSON parsing |
| FFmpeg binaries | LGPL/GPL 2.0 | Video processing engine |
| moondream (Python) | BSL 1.1 | Vision AI inference |
| Pillow (Python) | HPND | Image loading |
| PyTorch (Python) | BSD | ML backend |

---

## License

Copyright © 2026 David Strickland — DCSS Web Development LLC

Created with assistance from Claude AI (Anthropic).

---

*DigitAnalyzerSTR — because the data was always there, just locked in pixels.*