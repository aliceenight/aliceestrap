@echo off
setlocal

cd /d "%~dp0"

if not exist "wpfui\src\Wpf.Ui\Wpf.Ui.csproj" (
    echo.
    echo   The wpfui submodule is missing, nothing can build without it.
    echo   Run:  git submodule update --init
    echo.
    exit /b 1
)

echo Building, the first run takes a few minutes...
echo.

dotnet publish Aliceestrap\Aliceestrap.csproj ^
    -c Release ^
    -r win-x64 ^
    --self-contained false ^
    -p:PublishSingleFile=true ^
    -o build

if errorlevel 1 (
    echo.
    echo   Build failed. See the errors above.
    echo.
    call :keepopen
    exit /b 1
)

echo.
echo   Done.  %~dp0build\aliceestrap.exe
echo.

if /i "%~1"=="open" start "" "%~dp0build"

call :keepopen
endlocal
exit /b 0

:keepopen
echo %cmdcmdline% | find /i "%~nx0" >nul && pause
exit /b 0
