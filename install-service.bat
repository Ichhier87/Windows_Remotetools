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

sc create %SERVICE_NAME% binPath= "\"%EXE_PATH%\" --service" start= auto DisplayName= "Windows Remote Tools Service"

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
