@echo off
echo =============================================
echo  Royal Bakery API - Service Uninstaller
echo =============================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %ERRORLEVEL% neq 0 (
    echo ERROR: This script must be run as Administrator!
    echo Right-click this file and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

echo Stopping service...
sc stop RoyalBakeryAPI >nul 2>&1
timeout /t 3 /nobreak >nul

echo Removing service...
sc delete RoyalBakeryAPI

echo Removing firewall rule...
netsh advfirewall firewall delete rule name="Royal Bakery API" >nul 2>&1

echo.
echo Service uninstalled successfully.
echo.
pause
