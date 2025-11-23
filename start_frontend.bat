@echo off
setlocal

REM Base folder = folder where this .bat file is located
set BASE=%~dp0

REM Go to the Web folder that contains index.html
cd /d "%BASE%RoutingServiceREST\Web"

echo ==========================================
echo  Starting simple static HTTP server...
echo  URL: http://localhost:5500/index.html
echo ==========================================

REM Open the browser automatically
start "" http://localhost:5500/index.html

REM Start a simple HTTP server on port 5500 (Python 3)
python -m http.server 5500

endlocal
