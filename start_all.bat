@echo off
setlocal

REM Base folder = folder where this .bat file is located
set BASE=%~dp0

echo ==========================================
echo   Starting ProxyCacheService (self-host)...
echo ==========================================
start "" "%BASE%ProxyCacheService.SelfHost\bin\Debug\ProxyCacheService.SelfHost.exe"
timeout /t 2 /nobreak >nul

echo ==========================================
echo   Starting RoutingServiceREST (self-host)...
echo ==========================================
start "" "%BASE%RoutingServiceREST.SelfHost\bin\Debug\RoutingServiceREST.SelfHost.exe"
timeout /t 2 /nobreak >nul

echo ==========================================
echo   Starting NotificationService...
echo ==========================================
start "" "%BASE%NotificationService\bin\Debug\NotificationService.exe"

echo.
echo All services are now running in their own console windows.
echo You can now open the front-end (index.html) and test itineraries.
echo.
echo Press any key to exit this launcher (servers KEEP RUNNING).
pause >nul

endlocal
