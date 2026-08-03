@echo off
setlocal
cd /d "%~dp0"

if exist bin rmdir /s /q bin
if exist obj rmdir /s /q obj
if exist Publish\WalkerMediaManager rmdir /s /q Publish\WalkerMediaManager

dotnet restore WalkerMediaManager.UI.csproj
if errorlevel 1 goto :error

dotnet publish WalkerMediaManager.UI.csproj -c Release -r win-x64 --self-contained true -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true -p:PublishSingleFile=false -p:PublishTrimmed=false -p:PublishReadyToRun=false -o Publish\WalkerMediaManager
if errorlevel 1 goto :error

echo.
echo Publish completed successfully.
echo Output: %CD%\Publish\WalkerMediaManager
explorer "%CD%\Publish\WalkerMediaManager"
exit /b 0

:error
echo.
echo Publish failed. Review the messages above.
pause
exit /b 1
