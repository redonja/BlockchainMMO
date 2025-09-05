@echo off
:: Git Commit & Push to main Script
:: Usage: push.bat "commit message"

:: Check if a commit message was provided
if "%~1"=="" (
    echo You must provide a commit message.
    echo Example: push.bat "Fixed bug in terrain system"
    exit /b 1
)

:: Stage all changes
git add -A

:: Commit with the provided message
git commit -m "%~1"

:: Push to the main branch
git push origin main

echo.
echo Commit and push to main complete!
pause