@echo off
rem Build WinSwitcher.exe using the in-box .NET Framework compiler (no SDK needed).
setlocal
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe

"%CSC%" /nologo /codepage:65001 /target:winexe /platform:anycpu ^
  /win32manifest:app.manifest /win32icon:app.ico /out:Switcher.exe Switcher.cs

if errorlevel 1 (
  echo Build failed.
  pause
  exit /b 1
)
echo Built: %CD%\Switcher.exe
pause
