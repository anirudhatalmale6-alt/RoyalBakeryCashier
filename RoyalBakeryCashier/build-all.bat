@echo off
echo =============================================
echo   Royal Bakery POS - Build All Terminals
echo =============================================
echo.

set OUTDIR=%~dp0publish

echo [1/2] Building Cashier Terminal...
dotnet publish RoyalBakeryCashier\RoyalBakeryCashierTerminal.csproj -c Release -f net9.0-windows10.0.19041.0 -o "%OUTDIR%\Cashier" --self-contained false
if %errorlevel% neq 0 (
    echo ERROR: Cashier build failed!
    pause
    exit /b 1
)
echo Cashier built successfully.
echo.

echo [2/2] Building Salesman Terminal...
dotnet publish RoyalBakeryCashier\RoyalBakerySalesmanTerminal.csproj -c Release -f net9.0-windows10.0.19041.0 -o "%OUTDIR%\Salesman" --self-contained false
if %errorlevel% neq 0 (
    echo ERROR: Salesman build failed!
    pause
    exit /b 1
)
echo Salesman built successfully.
echo.

REM Create default terminal.config for Salesman
echo Mode=Salesman> "%OUTDIR%\Salesman\terminal.config"
echo Name=Salesman 1>> "%OUTDIR%\Salesman\terminal.config"

echo =============================================
echo   BUILD COMPLETE
echo =============================================
echo.
echo Output folders:
echo   Cashier:  %OUTDIR%\Cashier\RoyalBakeryCashier.exe
echo   Salesman: %OUTDIR%\Salesman\RoyalBakerySalesman.exe
echo.
echo SETUP:
echo   - For Salesman 2, copy the Salesman folder and edit terminal.config:
echo       Mode=Salesman
echo       Name=Salesman 2
echo   - For Salesman 3, copy again and change Name=Salesman 3, etc.
echo.
pause
