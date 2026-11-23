@echo off
setlocal
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"

"%CSC%" /nologo /codepage:65001 /target:exe /out:"%ROOT%harness.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  "%ROOT%..\src\Native.cs" "%ROOT%..\src\ClickEngine.cs" "%ROOT%..\src\ForegroundBorrower.cs" "%ROOT%..\src\KeepFrontPinner.cs" "%ROOT%..\src\MouseHook.cs" ^
  "%ROOT%..\src\Log.cs" "%ROOT%Harness.cs"
if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)

"%ROOT%harness.exe"
set "CODE=%ERRORLEVEL%"
echo.
echo harness exit code: %CODE%
if /i not "%~1"=="-nopause" pause
exit /b %CODE%