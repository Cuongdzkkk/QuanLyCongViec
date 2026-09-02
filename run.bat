@echo off
title Start Task Management System
cd /d "%~dp0"

if not defined DEV_SQL_DATABASE set "DEV_SQL_DATABASE=TaskManagementDB_V4"
if not defined DEV_SQL_SERVER set "DEV_SQL_SERVER=.\SQLEXPRESS"
for /f "usebackq delims=" %%s in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\resolve-sql-server.ps1" -PreferredServer "%DEV_SQL_SERVER%"`) do set "DEV_SQL_SERVER=%%s"
if not defined DEV_SQL_SERVER (
    echo Khong tim thay SQL Server local dang ket noi duoc.
    echo Hay start SQL Server hoac set DEV_SQL_SERVER, vi du: set DEV_SQL_SERVER=.\SQL2022
    pause
    exit /b 1
)
set "ConnectionStrings__DefaultConnection=Server=%DEV_SQL_SERVER%;Database=%DEV_SQL_DATABASE%;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False;Connection Timeout=60;"
set "DOTNET_EF_VERSION=10.0.9"
set "RUN_LOCK=%TEMP%\sprinta-task-management-startup.lock"
set "RUN_LOCK_OWNER=%RUN_LOCK%\owner.pid"

if exist "%RUN_LOCK%" (
    powershell -NoProfile -ExecutionPolicy Bypass -Command "$lock='%RUN_LOCK%'; $ownerFile='%RUN_LOCK_OWNER%'; $owner=$null; if (Test-Path -LiteralPath $ownerFile) { $owner=[int](Get-Content -LiteralPath $ownerFile -ErrorAction SilentlyContinue | Select-Object -First 1) }; $process=$null; if ($owner) { $process=Get-CimInstance Win32_Process -Filter ('ProcessId=' + $owner) -ErrorAction SilentlyContinue }; if ($process -and $process.CommandLine -match '(?i)run\.bat') { exit 1 }; $item=Get-Item -LiteralPath $lock -ErrorAction SilentlyContinue; if ($item -and ((Get-Date) - $item.LastWriteTime).TotalSeconds -lt 30) { exit 1 }; Remove-Item -LiteralPath $lock -Recurse -Force -ErrorAction SilentlyContinue; exit 0"
    if errorlevel 1 (
        echo Mot phien run.bat khac dang khoi dong he thong. Vui long doi phien do hoan tat.
        pause
        exit /b 1
    )
)

2>nul mkdir "%RUN_LOCK%"
if errorlevel 1 (
    echo Mot phien run.bat khac dang khoi dong he thong. Vui long doi phien do hoan tat.
    pause
    exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -Command "$current=Get-CimInstance Win32_Process -Filter ('ProcessId=' + $PID); if ($current.ParentProcessId) { Set-Content -LiteralPath '%RUN_LOCK_OWNER%' -Value $current.ParentProcessId -NoNewline }"

echo =======================================
echo KHỞI ĐỘNG HỆ THỐNG TASK MANAGEMENT
echo =======================================
echo.
echo Dang dong cac phien ban Backend API cu (neu co)...
taskkill /FI "WINDOWTITLE eq Backend API*" /T /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq Frontend Vue*" /T /F >nul 2>&1
taskkill /IM dotnet.exe /F >nul 2>&1
dotnet build-server shutdown >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\stop-dev-processes.ps1" -RepositoryRoot "%~dp0."
if errorlevel 1 (
    echo Khong the dong tien trinh Backend/Frontend cu.
    pause
    goto :startup_failed
)

echo.
set /p resetDB="Ban co muon reset Database va chay Db Migrations + Seed Data khong? (Y/N): "
if /I "%resetDB%"=="Y" (
    echo.
    echo --- DANG RESET DATABASE ---
    cd /d "%~dp0Backend\src\TaskManagement.API"
    
    echo Restoring NuGet packages...
    dotnet restore
    if errorlevel 1 (
        echo NuGet package restore that bai.
        pause
        goto :startup_failed
    )
    call :ensure_dotnet_ef
    if errorlevel 1 (
        pause
        goto :startup_failed
    )
    echo 1. Drop Database cu...
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-sql.ps1" -Server "%DEV_SQL_SERVER%" -Database "master" -Query "IF DB_ID('%DEV_SQL_DATABASE%') IS NOT NULL BEGIN ALTER DATABASE [%DEV_SQL_DATABASE%] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [%DEV_SQL_DATABASE%]; END"
    if errorlevel 1 (
        echo Drop database that bai.
        pause
        goto :startup_failed
    )
    
    echo 2. Cap nhat Database bang migrations hien co...
    dotnet build
    if errorlevel 1 (
        echo Build backend that bai.
        pause
        goto :startup_failed
    )
    powershell -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '..\..' -Recurse -Filter '*.dll' | Unblock-File"
    dotnet ef database update --project ../TaskManagement.Infrastructure --startup-project . --no-build
    if errorlevel 1 (
        echo WARNING: Cap nhat database that bai ^(Co the do Application Control^). Bo qua va tiep tuc...
    )
    
    echo 3. Dang nap demo data doanh nghiep cho admin dev@sprinta.local...
    if exist "%~dp0scripts\seed-demo-data.sql" (
        powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-sql.ps1" -Server "%DEV_SQL_SERVER%" -Database "%DEV_SQL_DATABASE%" -InputFile "%~dp0scripts\seed-demo-data.sql"
        if errorlevel 1 (
            echo Seed demo data that bai.
            pause
            goto :startup_failed
        )
    ) else (
        echo Khong tim thay scripts\seed-demo-data.sql, bo qua demo seed.
    )
    cd /d "%~dp0"
    echo --- RESET DATABASE THANH CONG ---
    echo.
) else (
    echo.
    echo --- KIEM TRA VA TAO DATABASE NẾU CHƯA CÓ ---
    cd /d "%~dp0Backend\src\TaskManagement.API"
    
    echo Restoring NuGet packages...
    dotnet restore
    if errorlevel 1 (
        echo NuGet package restore that bai.
        pause
        goto :startup_failed
    )
    call :ensure_dotnet_ef
    if errorlevel 1 (
        pause
        goto :startup_failed
    )
    if not exist "..\TaskManagement.Infrastructure\Migrations" (
        echo Chua co Migrations, dang tao moi de chuan bi tao DB...
        dotnet ef migrations add InitialCreate --project ../TaskManagement.Infrastructure --startup-project .
    )
    
    echo Cap nhat / Tao moi Database...
    dotnet build
    if errorlevel 1 (
        echo Build backend that bai.
        pause
        goto :startup_failed
    )
    powershell -ExecutionPolicy Bypass -Command "Get-ChildItem -Path '..\..' -Recurse -Filter '*.dll' | Unblock-File"
    dotnet ef database update --project ../TaskManagement.Infrastructure --startup-project . --no-build
    if errorlevel 1 (
        echo WARNING: Cap nhat database that bai ^(Co the do Application Control^). Bo qua va tiep tuc khoi dong...
    )

    echo Dang nap demo data cho admin dev@sprinta.local...
    if exist "%~dp0scripts\seed-demo-data.sql" (
        powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-sql.ps1" -Server "%DEV_SQL_SERVER%" -Database "%DEV_SQL_DATABASE%" -InputFile "%~dp0scripts\seed-demo-data.sql"
        if errorlevel 1 (
            echo Seed demo data that bai.
            pause
            goto :startup_failed
        )
    ) else (
        echo Khong tim thay scripts\seed-demo-data.sql, bo qua demo seed.
    )
    cd /d "%~dp0"
    echo --- HOAN TAT KIEM TRA ---
    echo.
)

echo 1. Khởi động Backend (.NET Web API)...
start "Backend API" cmd /k "pushd ""%~dp0Backend\src\TaskManagement.API"" && title Backend API && dotnet run --no-build --launch-profile http"

echo 2. Khởi động Frontend (Vue 3)...
start "Frontend Vue" cmd /k "pushd ""%~dp0Frontend"" && title Frontend Vue && if not exist node_modules (echo Cai dat dependencies bang npm... && npm install) && npm run dev"

echo Da gui lenh khoi dong cho ca Backend va Frontend o cac cua so rieng biet!
echo =======================================
goto :startup_complete

:startup_failed
rd /s /q "%RUN_LOCK%" >nul 2>&1
exit /b 1

:startup_complete
rd /s /q "%RUN_LOCK%" >nul 2>&1
exit /b 0

:ensure_dotnet_ef
for /f "tokens=2" %%v in ('dotnet tool list -g ^| findstr /R /C:"^dotnet-ef " 2^>nul') do set "CURRENT_DOTNET_EF=%%v"
if "%CURRENT_DOTNET_EF%"=="%DOTNET_EF_VERSION%" exit /b 0
echo Dang cap nhat dotnet-ef len phien ban %DOTNET_EF_VERSION%...
dotnet tool update --global dotnet-ef --version %DOTNET_EF_VERSION%
if errorlevel 1 (
    echo Khong the cap nhat dotnet-ef. Hay chay: dotnet tool update --global dotnet-ef --version %DOTNET_EF_VERSION%
    exit /b 1
)
exit /b 0
