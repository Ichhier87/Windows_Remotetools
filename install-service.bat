@echo off
REM Windows Remote Tools Service Installation Script
REM Run this script as Administrator to install the service

echo ========================================
echo Windows Remote Tools Service Installer
echo ========================================
echo.

REM Check for admin privileges
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click on this file and select "Run as administrator"
    pause
    exit /b 1
)

set SERVICE_NAME=WindowsRemoteToolsService
set EXE_PATH=%~dp0WindowsRemoteTools\bin\Release\net8.0-windows\WindowsRemoteTools.exe
set UI_EXE_PATH=%~dp0WindowsRemoteToolsUI\bin\Release\net8.0-windows\WindowsRemoteToolsUI.exe

REM Validate required binaries exist
if not exist "%EXE_PATH%" (
    echo ERROR: Service-Binary nicht gefunden: %EXE_PATH%
    echo Bitte zuerst kompilieren: dotnet build WindowsRemoteTools.sln -c Release
    pause
    exit /b 1
)
if not exist "%UI_EXE_PATH%" (
    echo WARNUNG: UI-Binary nicht gefunden: %UI_EXE_PATH%
    echo Der Service wird ohne UI-Prozess installiert. Bitte WindowsRemoteToolsUI ebenfalls kompilieren:
    echo   dotnet build WindowsRemoteToolsUI\WindowsRemoteToolsUI.csproj -c Release
    echo.
) else (
    REM Service expects WindowsRemoteToolsUI.exe next to its own exe (Assembly.Location dir).
    REM Copy UI binaries (and their dependencies) into the service output folder.
    echo Kopiere UI-Dateien neben Service-Exe...
    for %%F in (WindowsRemoteToolsUI.exe WindowsRemoteToolsUI.dll WindowsRemoteToolsUI.deps.json WindowsRemoteToolsUI.runtimeconfig.json) do (
        copy /Y "%~dp0WindowsRemoteToolsUI\bin\Release\net8.0-windows\%%F" "%~dp0WindowsRemoteTools\bin\Release\net8.0-windows\" >nul
    )

    REM WebView2 managed DLLs (fuer Vokabeltrainer-Overlay)
    for %%F in (Microsoft.Web.WebView2.Core.dll Microsoft.Web.WebView2.WinForms.dll) do (
        if exist "%~dp0WindowsRemoteToolsUI\bin\Release\net8.0-windows\%%F" (
            copy /Y "%~dp0WindowsRemoteToolsUI\bin\Release\net8.0-windows\%%F" "%~dp0WindowsRemoteTools\bin\Release\net8.0-windows\" >nul
        )
    )

    REM WebView2 nativer Loader (arch-spezifisch unter runtimes\win-x64\native)
    if exist "%~dp0WindowsRemoteToolsUI\bin\Release\net8.0-windows\runtimes\win-x64\native\WebView2Loader.dll" (
        if not exist "%~dp0WindowsRemoteTools\bin\Release\net8.0-windows\runtimes\win-x64\native\" (
            mkdir "%~dp0WindowsRemoteTools\bin\Release\net8.0-windows\runtimes\win-x64\native"
        )
        copy /Y "%~dp0WindowsRemoteToolsUI\bin\Release\net8.0-windows\runtimes\win-x64\native\WebView2Loader.dll" "%~dp0WindowsRemoteTools\bin\Release\net8.0-windows\runtimes\win-x64\native\" >nul
    )

    echo OK UI-Dateien kopiert
    echo.
)

echo Checking if service exists...
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Service already exists. Stopping and removing old service...
    sc stop %SERVICE_NAME%
    timeout /t 2 /nobreak >nul
    sc delete %SERVICE_NAME%
    timeout /t 2 /nobreak >nul
)

echo.
echo Installing service...
echo Executable: %EXE_PATH%
echo.

sc create %SERVICE_NAME% binPath= "\"%EXE_PATH%\" --service" start= auto DisplayName= "Windows Remote Tools Service" obj= "LocalSystem"

if %errorLevel% neq 0 (
    echo.
    echo ERROR: Failed to create service!
    echo Make sure you have built the project in Release mode.
    pause
    exit /b 1
)

echo.
echo Service created successfully!
echo.

REM Set service description
sc description %SERVICE_NAME% "Steuert Windows-Geraete remote ueber WebSocket. Startet automatisch den UI-Prozess in der Benutzer-Session und ueberwacht ihn."

REM Configure automatic recovery: restart after 60s/120s/300s, reset failure counter daily.
REM This ensures the service is restarted by the Windows Service Control Manager (SCM)
REM if it crashes. The internal ProcessWatchdog handles UI restarts; SCM handles service restarts.
echo Configuring automatic service recovery...
sc failure %SERVICE_NAME% reset= 86400 actions= restart/60000/restart/120000/restart/300000

if %errorLevel% neq 0 (
    echo WARNUNG: Service-Recovery-Konfiguration fehlgeschlagen!
    echo Der Service wird bei einem Crash nicht automatisch neu gestartet.
) else (
    echo Service-Recovery konfiguriert: Neustart nach 60s / 120s / 300s
)

REM Enable recovery actions also for clean stops with non-zero exit codes
sc failureflag %SERVICE_NAME% 1 >nul 2>&1

echo.
echo Starting service...
sc start %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo.
    echo WARNING: Failed to start service automatically.
    echo You can start it manually from Services (services.msc)
    echo or by running: sc start %SERVICE_NAME%
) else (
    echo.
    echo Service started successfully!
)

echo.
echo ========================================
echo Installation complete!
echo ========================================
echo.
echo You can manage the service using:
echo - Services (services.msc)
echo - sc start %SERVICE_NAME%
echo - sc stop %SERVICE_NAME%
echo - sc query %SERVICE_NAME%
echo.
pause
