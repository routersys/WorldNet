@echo off
setlocal

set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"

if not exist world-src (
  git clone --depth 1 https://github.com/mmorise/World.git world-src || exit /b 1
)

set "VSWHERE=%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\vswhere.exe"
if not exist "%VSWHERE%" (
  echo vswhere.exe not found.
  exit /b 1
)

set "VSPATH_FILE=%TEMP%\worldnet_vspath.txt"
"%VSWHERE%" -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath > "%VSPATH_FILE%"
set "VSPATH="
set /p VSPATH=<"%VSPATH_FILE%"
del "%VSPATH_FILE%"
if not defined VSPATH (
  echo MSVC toolset not found.
  exit /b 1
)

call "%VSPATH%\VC\Auxiliary\Build\vcvars64.bat" >nul || exit /b 1

set "INPUT_WAV=%~1"
if "%INPUT_WAV%"=="" set "INPUT_WAV=world-src\test\vaiueo2d.wav"
set "OUTPUT_DIR=%~2"
if "%OUTPUT_DIR%"=="" set "OUTPUT_DIR=data"

if not exist obj mkdir obj
if not exist "%OUTPUT_DIR%" mkdir "%OUTPUT_DIR%"

cl /nologo /O2 /EHsc /I world-src\src /I world-src\tools /I . /Fo:obj\ /Fe:worldref.exe main.cpp world-src\src\*.cpp world-src\tools\audioio.cpp world-src\tools\parameterio.cpp || exit /b 1

.\worldref.exe "%INPUT_WAV%" "%OUTPUT_DIR%" || exit /b 1

endlocal
