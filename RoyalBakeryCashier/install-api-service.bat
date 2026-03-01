@echo off
echo =============================================
echo  Royal Bakery API - Windows Service Installer
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

:: Set paths
set "PROJECT_DIR=%~dp0RoyalBakeryAPI"
set "PUBLISH_DIR=%~dp0publish\API"

echo Step 1: Publishing API...
echo.
dotnet publish "%PROJECT_DIR%\RoyalBakeryAPI.csproj" -c Release -o "%PUBLISH_DIR%" --self-contained false
if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Build failed! Make sure .NET 9 SDK is installed.
    pause
    exit /b 1
)

echo.
echo Step 2: Stopping existing service (if running)...
sc stop RoyalBakeryAPI >nul 2>&1
timeout /t 3 /nobreak >nul

echo.
echo Step 3: Removing old service (if exists)...
sc delete RoyalBakeryAPI >nul 2>&1
timeout /t 2 /nobreak >nul

echo.
echo Step 4: Installing service...
sc create RoyalBakeryAPI binPath="%PUBLISH_DIR%\RoyalBakeryAPI.exe" start=auto DisplayName="Royal Bakery API"
if %ERRORLEVEL% neq 0 (
    echo ERROR: Failed to create service!
    pause
    exit /b 1
)

:: Set service description
sc description RoyalBakeryAPI "Royal Bakery POS API - serves cashier terminals and GRN app"

:: Set service to restart on failure
sc failure RoyalBakeryAPI reset=86400 actions=restart/5000/restart/10000/restart/30000

echo.
echo Step 5: Adding firewall rule...
netsh advfirewall firewall delete rule name="Royal Bakery API" >nul 2>&1
netsh advfirewall firewall add rule name="Royal Bakery API" dir=in action=allow protocol=TCP localport=5000

echo.
echo Step 6: Starting service...
sc start RoyalBakeryAPI
if %ERRORLEVEL% neq 0 (
    echo WARNING: Service failed to start. Check Windows Event Viewer for details.
    echo The service may need the correct SQL Server connection string in appsettings.json
    pause
    exit /b 1
)

echo.
echo =============================================
echo  SUCCESS! Royal Bakery API is now installed
echo  as a Windows Service and will auto-start
echo  when Windows boots up.
echo.
echo  Service Name: RoyalBakeryAPI
echo  URL: http://0.0.0.0:5000
echo  Firewall: Port 5000 allowed
echo  Auto-restart on failure: Yes
echo =============================================
echo.
pause
