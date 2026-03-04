@echo off
title KocurConsole Installer
color 0A
echo.
echo  ======================================
echo    KocurConsole Installer
echo  ======================================
echo.

:: Check for admin rights
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo  [!] Administrator privileges required.
    echo  [!] Right-click this file and select "Run as Administrator"
    echo.
    pause
    exit /b 1
)

set "INSTALL_DIR=C:\Program Files\KocurConsole"
set "EXE_NAME=KocurConsole.exe"
set "DOWNLOAD_URL=https://github.com/Kocurowy96/KocurConsole/releases/latest/download/%EXE_NAME%"
set "TEMP_EXE=%TEMP%\%EXE_NAME%"

:: Check if already installed
if exist "%INSTALL_DIR%\%EXE_NAME%" (
    echo  [i] KocurConsole is already installed.
    echo  [i] Updating to the latest version...
    echo.
    set "IS_UPDATE=1"
) else (
    echo  [i] Fresh installation.
    echo.
    set "IS_UPDATE=0"
)

:: Download latest exe from GitHub Releases
echo  [..] Downloading latest KocurConsole from GitHub...
powershell -NoProfile -Command "[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; try { (New-Object Net.WebClient).DownloadFile('%DOWNLOAD_URL%', '%TEMP_EXE%') } catch { Write-Host '  [!!] Download failed:' $_.Exception.Message; exit 1 }"
if %errorlevel% neq 0 (
    echo  [!!] Could not download KocurConsole.
    echo  [!!] Check your internet connection or visit:
    echo  [!!] https://github.com/Kocurowy96/KocurConsole/releases
    echo.
    pause
    exit /b 1
)
echo  [OK] Downloaded successfully.

:: Create installation directory
if not exist "%INSTALL_DIR%" (
    mkdir "%INSTALL_DIR%"
    echo  [OK] Created directory: %INSTALL_DIR%
)

:: Copy exe to install directory
echo  [..] Installing...
copy /Y "%TEMP_EXE%" "%INSTALL_DIR%\%EXE_NAME%" >nul
if errorlevel 1 (
    echo  [!!] Failed to copy %EXE_NAME%
    echo  [!!] Make sure KocurConsole is not running.
    pause
    exit /b 1
)
del "%TEMP_EXE%" >nul 2>&1
echo  [OK] Installed %EXE_NAME%

:: If update — skip shortcut/PATH creation
if "%IS_UPDATE%"=="1" (
    echo.
    echo  ======================================
    echo    Update complete!
    echo  ======================================
    echo.
    echo  Location: %INSTALL_DIR%\%EXE_NAME%
    echo.
    pause
    exit /b 0
)

:: Create Start Menu shortcut
set "SHORTCUT_DIR=%ProgramData%\Microsoft\Windows\Start Menu\Programs"
set "SHORTCUT=%SHORTCUT_DIR%\KocurConsole.lnk"
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%SHORTCUT%'); $s.TargetPath = '%INSTALL_DIR%\%EXE_NAME%'; $s.WorkingDirectory = '%USERPROFILE%'; $s.Description = 'KocurConsole Terminal'; $s.Save()" >nul 2>&1
if %errorlevel% equ 0 (
    echo  [OK] Created Start Menu shortcut
) else (
    echo  [--] Could not create Start Menu shortcut
)

:: Create Desktop shortcut
set "DESKTOP_SHORTCUT=%USERPROFILE%\Desktop\KocurConsole.lnk"
powershell -NoProfile -Command "$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('%DESKTOP_SHORTCUT%'); $s.TargetPath = '%INSTALL_DIR%\%EXE_NAME%'; $s.WorkingDirectory = '%USERPROFILE%'; $s.Description = 'KocurConsole Terminal'; $s.Save()" >nul 2>&1
if %errorlevel% equ 0 (
    echo  [OK] Created Desktop shortcut
) else (
    echo  [--] Could not create Desktop shortcut
)

:: Add to PATH
echo  [..] Adding to PATH...
powershell -NoProfile -Command "$path = [Environment]::GetEnvironmentVariable('PATH', 'Machine'); if ($path -notlike '*KocurConsole*') { [Environment]::SetEnvironmentVariable('PATH', $path + ';%INSTALL_DIR%', 'Machine'); Write-Host '  [OK] Added to system PATH' } else { Write-Host '  [OK] Already in PATH' }"

:: Create uninstaller
echo @echo off > "%INSTALL_DIR%\uninstall.bat"
echo title KocurConsole Uninstaller >> "%INSTALL_DIR%\uninstall.bat"
echo color 0C >> "%INSTALL_DIR%\uninstall.bat"
echo echo. >> "%INSTALL_DIR%\uninstall.bat"
echo echo  Uninstalling KocurConsole... >> "%INSTALL_DIR%\uninstall.bat"
echo echo. >> "%INSTALL_DIR%\uninstall.bat"
echo net session ^>nul 2^>^&1 >> "%INSTALL_DIR%\uninstall.bat"
echo if %%errorlevel%% neq 0 ( >> "%INSTALL_DIR%\uninstall.bat"
echo     echo  [!] Run as Administrator >> "%INSTALL_DIR%\uninstall.bat"
echo     pause >> "%INSTALL_DIR%\uninstall.bat"
echo     exit /b 1 >> "%INSTALL_DIR%\uninstall.bat"
echo ) >> "%INSTALL_DIR%\uninstall.bat"
echo del "%SHORTCUT%" ^>nul 2^>^&1 >> "%INSTALL_DIR%\uninstall.bat"
echo del "%DESKTOP_SHORTCUT%" ^>nul 2^>^&1 >> "%INSTALL_DIR%\uninstall.bat"
echo powershell -NoProfile -Command "$p = [Environment]::GetEnvironmentVariable('PATH','Machine'); $p = $p.Replace(';%INSTALL_DIR%','').Replace('%INSTALL_DIR%;','').Replace('%INSTALL_DIR%',''); [Environment]::SetEnvironmentVariable('PATH',$p,'Machine')" >> "%INSTALL_DIR%\uninstall.bat"
echo echo  [OK] Removed from PATH >> "%INSTALL_DIR%\uninstall.bat"
echo echo  [OK] Removed shortcuts >> "%INSTALL_DIR%\uninstall.bat"
echo echo  [OK] Delete this folder manually: %INSTALL_DIR% >> "%INSTALL_DIR%\uninstall.bat"
echo echo. >> "%INSTALL_DIR%\uninstall.bat"
echo pause >> "%INSTALL_DIR%\uninstall.bat"
echo  [OK] Created uninstaller

echo.
echo  ======================================
echo    Installation complete!
echo  ======================================
echo.
echo  Location:  %INSTALL_DIR%
echo  Shortcuts: Start Menu + Desktop
echo  PATH:      Added (restart terminal to use)
echo.
echo  Run: KocurConsole (from any terminal)
echo  Or:  Double-click Desktop shortcut
echo.
pause
