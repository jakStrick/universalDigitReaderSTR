@echo off
echo DigitAnalyzerSTR — Python environment setup
echo ============================================
echo.

REM Check Python is available
python --version >nul 2>&1
if errorlevel 1 (
    echo ERROR: Python not found. Please install Python 3.9+ from https://python.org
    echo Make sure to check "Add Python to PATH" during installation.
    pause
    exit /b 1
)

echo Python found. Installing moondream package...
python -m pip install moondream pillow --upgrade --no-warn-script-location

if errorlevel 1 (
    echo ERROR: Failed to install packages. Check your internet connection.
    pause
    exit /b 1
)

echo.
echo Setup complete! You can now run DigitAnalyzerSTR.exe
pause
