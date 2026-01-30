
@echo off
echo Starting DoorMonitorSystem to trigger migration...
cd e:\VS2022\WPF\DoorMonitorSystem\bin\Debug\net6.0-windows
start "" "DoorMonitorSystem.exe"

echo Waiting 30 seconds for startup and migration...
timeout /t 30

echo Checking for SystemConfig.txt...
if exist "Config\SystemConfig.txt" (
    echo [SUCCESS] SystemConfig.txt generated.
    type "Config\SystemConfig.txt"
) else (
    echo [ERROR] SystemConfig.txt NOT found.
)

echo.
echo Checking logs for migration success...
findstr "Migrate" "logs\*.log"

echo.
echo Stopping application...
taskkill /f /im DoorMonitorSystem.exe
