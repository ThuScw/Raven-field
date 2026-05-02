@echo off
chcp 65001 >nul
echo ========================================
echo Ravenfield with AI Enhancement
echo ========================================
echo.
echo Starting Ravenfield with AI Threat Assessment Mod...
echo.

REM Set Unity to log more information
set UNITY_LOG_PATH=%USERPROFILE%\AppData\LocalLow\SteelRaven7\Ravenfield

REM Launch the game
start "" "%~dp0Ravenfield.exe"

echo Game launched! Check the log for [AIEnhancement] messages.
echo Log location: %UNITY_LOG_PATH%\output_log.txt
echo.
pause
