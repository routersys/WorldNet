@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

set "VSINSTALLER=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer"
if not exist "%VSINSTALLER%\vswhere.exe" (
  echo vswhere.exe not found in "%VSINSTALLER%".
  exit /b 1
)
set "PATH=%VSINSTALLER%;%PATH%"

set "VSPATH_FILE=%TEMP%\worldnet_vspath_publish.txt"
vswhere.exe -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath > "%VSPATH_FILE%"
set "VSPATH="
set /p VSPATH=<"%VSPATH_FILE%"
del "%VSPATH_FILE%"
if not defined VSPATH (
  echo MSVC toolset not found.
  exit /b 1
)

call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1

dotnet publish WorldNet.Examples\WorldNet.Examples.csproj -r win-x64 -c Release --nologo || exit /b 1

endlocal
