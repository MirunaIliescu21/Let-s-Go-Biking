@echo off
echo ==========================================
echo   Stopping all Let's Go Biking servers...
echo ==========================================

REM Kill ProxyCacheService self-host
taskkill /IM ProxyCacheService.SelfHost.exe /F >nul 2>&1

REM Kill RoutingServiceREST self-host
taskkill /IM RoutingServiceREST.SelfHost.exe /F >nul 2>&1

REM Kill NotificationService
taskkill /IM NotificationService.exe /F >nul 2>&1

echo Done.
echo All backend services have been stopped.
pause
