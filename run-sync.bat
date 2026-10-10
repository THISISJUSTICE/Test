@echo off
cd /d "%~dp0"

echo [CHECK] Current directory: %CD%
echo [CHECK] Python script:
dir "Tools\UMLSync\sync.py"

echo.
echo [RUN]
python "Tools\UMLSync\sync.py"

echo.
pause