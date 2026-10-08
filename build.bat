@echo off
rem Build ClickTimer.exe with the C# compiler that ships with Windows (.NET Framework 4.x)
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo csc.exe not found - .NET Framework 4.x is required.
  pause
  exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /codepage:65001 /win32icon:app.ico /out:ClickTimer.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll *.cs
if errorlevel 1 (echo Build failed.) else (echo Build OK: ClickTimer.exe)
pause
