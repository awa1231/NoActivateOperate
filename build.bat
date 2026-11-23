@echo off
setlocal
set "ROOT=%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
  echo Cannot find the .NET Framework C# compiler.
  set "RC=1"
  goto :done
)

"%CSC%" /nologo /codepage:65001 /langversion:5 /target:winexe /platform:anycpu /optimize+ /warn:4 ^
  /out:"%ROOT%NoActivateClick.exe" ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationClient.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\UIAutomationTypes.dll" ^
  /r:"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll" ^
  "%ROOT%src\Native.cs" ^
  "%ROOT%src\Log.cs" ^
  "%ROOT%src\ClickEngine.cs" ^
  "%ROOT%src\ForegroundBorrower.cs" ^
  "%ROOT%src\KeepFrontPinner.cs" ^
  "%ROOT%src\MouseHook.cs" ^
  "%ROOT%src\AppConfig.cs" ^
  "%ROOT%src\Hotkey.cs" ^
  "%ROOT%src\HotkeyHost.cs" ^
  "%ROOT%src\AppIcons.cs" ^
  "%ROOT%src\TextFieldDetector.cs" ^
  "%ROOT%src\HotkeyDialog.cs" ^
  "%ROOT%src\TrayApp.cs" ^
  "%ROOT%src\Program.cs"

if errorlevel 1 (
  echo.
  echo BUILD FAILED
  set "RC=1"
  goto :done
)

echo Built: %ROOT%NoActivateClick.exe
set "RC=0"

:done
if /i not "%~1"=="-nopause" pause
exit /b %RC%