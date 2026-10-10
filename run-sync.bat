@echo off
cd /d "%~dp0"
py -3 Tools\UMLSync\sync.py %*
if errorlevel 1 (
  echo [ERROR] UML sync failed.
  pause
  exit /b 1
)
echo UML sync finished. Review the drawio diff before committing.
pause
