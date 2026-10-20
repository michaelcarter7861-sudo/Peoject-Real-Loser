@echo off
REM ============================================================================
REM Project Real Loser v1.23 — One-Click Unity 2022.3.40f1 Android 9+ APK Builder
REM ============================================================================
setlocal

set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\2022.3.40f1\Editor\Unity.exe"
if not exist "%UNITY_EXE%" (
    for /d %%D in ("C:\Program Files\Unity\Hub\Editor\2022.3*") do (
        if exist "%%~fD\Editor\Unity.exe" set "UNITY_EXE=%%~fD\Editor\Unity.exe"
    )
)

if not exist "%UNITY_EXE%" (
    echo [ERROR] Could not locate Unity 2022.3 Editor at "%UNITY_EXE%".
    echo Please edit UNITY_EXE in build_apk_windows.bat or open the project in Unity Hub.
    exit /b 1
)

echo [INFO] Using Unity Editor: "%UNITY_EXE%"
echo [INFO] Building Android 9+ Universal APK (ARM64 + ARMv7, OpenGL ES 3.0+)...
"%UNITY_EXE%" -batchmode -nographics -quit -projectPath "%~dp0." -executeMethod RealLoser.Editor.RealLoserProjectBuilder.BuildAndroidAPK -logFile "%~dp0unity_android_build.log"

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] APK built at: "%~dp0Builds\Android\ProjectRealLoser_v1.23_Android9.apk"
) else (
    echo [FAILED] Build exited with code %ERRORLEVEL%. Check unity_android_build.log for details.
)
endlocal
