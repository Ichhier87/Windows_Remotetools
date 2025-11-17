@echo off
REM Windows Remote Tools Service Uninstallation Script
REM Run this script as Administrator to uninstall the service

echo ==========================================
echo Windows Remote Tools Service Uninstaller
echo ==========================================
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

echo Checking if service exists...
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% neq 0 (
    echo Service does not exist. Nothing to uninstall.
    pause
    exit /b 0
)

echo.
echo Stopping service...
sc stop %SERVICE_NAME%
timeout /t 3 /nobreak >nul

echo.
echo Removing service...
sc delete %SERVICE_NAME%

if %errorLevel% neq 0 (
    echo.
    echo ERROR: Failed to delete service!
    pause
    exit /b 1
)

echo.
echo ==========================================
echo Service uninstalled successfully!
echo ==========================================
echo.
pause
