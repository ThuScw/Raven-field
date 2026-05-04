@echo off
echo [AI Enhancement V8] Building...

set CSC_PATH="C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set GAME_DIR=%~dp0..
set REF_UE=%GAME_DIR%\Ravenfield_Data\Managed\UnityEngine.dll
set REF_AC=%GAME_DIR%\Ravenfield_Data\Managed\Assembly-CSharp.dll
set OUTPUT=%~dp0AIEnhancement.dll

if not exist %REF_UE% (
    echo ERROR: UnityEngine.dll not found at %REF_UE%
    pause
    exit /b 1
)

if not exist %REF_AC% (
    echo ERROR: Assembly-CSharp.dll not found at %REF_AC%
    pause
    exit /b 1
)

echo Reference: %REF_UE%
echo Reference: %REF_AC%

%CSC_PATH% /target:library /out:"%OUTPUT%" /optimize+ /platform:x64 /r:"%REF_UE%" /r:"%REF_AC%" /nologo /warnaserror- "%~dp0AIEnhancement.cs"

if %errorlevel% equ 0 (
    echo [AI Enhancement V8] Build SUCCESS: %OUTPUT%
) else (
    echo [AI Enhancement V8] Build FAILED
    pause
    exit /b 1
)

pause
