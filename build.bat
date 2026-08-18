@echo off
setlocal
cd /d "%~dp0"

set "ISCC=C:\Users\tutot\AppData\Local\Temp\opencode\innosetup\ISCC.exe"

echo === Publishing ObbyistMacro (Release, self-contained single file) ===
dotnet publish src\ObbyistMacro.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o src\publish
if errorlevel 1 (
    echo.
    echo PUBLISH FAILED
    pause
    exit /b 1
)

echo.
echo === Compiling installer ===
if not exist "%ISCC%" (
    echo ISCC.exe not found: %ISCC%
    pause
    exit /b 1
)
"%ISCC%" installer\ObbyistMacro.iss
if errorlevel 1 (
    echo.
    echo INSTALLER COMPILE FAILED
    pause
    exit /b 1
)

echo.
echo === Done ===
echo App:       src\publish\ObbyistMacro.exe
echo Installer: installer\output\ObbyistMacro-Setup.exe
pause