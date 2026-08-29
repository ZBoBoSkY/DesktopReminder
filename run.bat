@echo off
chcp 65001 >nul
cd /d "%~dp0"
rem 优先使用打包版，否则用源码方式启动
if exist "dist\DesktopReminder\DesktopReminder.exe" (
    start "" "dist\DesktopReminder\DesktopReminder.exe"
) else (
    if exist "pythonw.exe" (
        start "" "pythonw.exe" main.py
    ) else (
        start "" pythonw main.py
    )
)
