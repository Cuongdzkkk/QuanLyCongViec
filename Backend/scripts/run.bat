@echo off
title Start Task Management System

echo =======================================
echo KHỞI ĐỘNG HỆ THỐNG TASK MANAGEMENT
echo =======================================

echo.
set /p resetDB="Ban co muon reset Database va chay Db Migrations + Seed Data khong? (Y/N): "
if /I "%resetDB%"=="Y" (
    echo.
    echo --- DANG RESET DATABASE ---
    cd Backend\src\TaskManagement.API
    
    echo 1. Drop Database cu...
    sqlcmd -S "KHOI\SQLEXPRESS" -Q "IF DB_ID('TaskManagementDB') IS NOT NULL BEGIN ALTER DATABASE [TaskManagementDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TaskManagementDB]; END" -E -C
    
    echo 2. Xoa cac migration cu...
    if exist "..\TaskManagement.Infrastructure\Migrations" rd /s /q "..\TaskManagement.Infrastructure\Migrations"
    
    echo 3. Tao migration moi 'PlaneRenovation'...
    dotnet ef migrations add PlaneRenovation --project ../TaskManagement.Infrastructure --startup-project .
    
    echo 4. Cap nhat Database...
    dotnet ef database update --project ../TaskManagement.Infrastructure --startup-project .
    
    echo 5. Dang chay data ban dau seed_data.sql va cac bang moi...
    sqlcmd -S "KHOI\SQLEXPRESS" -d "TaskManagementDB" -i "..\..\seed_data.sql" -E -C
    
    cd ..\..\..
    echo --- RESET DATABASE THANH CONG ---
    echo.
) else (
    echo.
    echo --- KIEM TRA VA TAO DATABASE NẾU CHƯA CÓ ---
    cd Backend\src\TaskManagement.API
    
    if not exist "..\TaskManagement.Infrastructure\Migrations" (
        echo Chua co Migrations, dang tao moi de chuan bi tao DB...
        dotnet ef migrations add InitialCreate --project ../TaskManagement.Infrastructure --startup-project .
    )
    
    echo Cap nhat / Tao moi Database...
    dotnet ef database update --project ../TaskManagement.Infrastructure --startup-project .
    cd ..\..\..
    echo --- HOAN TAT KIEM TRA ---
    echo.
)

echo 1. Khởi động Backend (.NET Web API)...
start "Backend API" cmd /k "cd Backend\src\TaskManagement.API && title Backend API && dotnet run"

echo 2. Khởi động Frontend (Vue 3)...
start "Frontend Vue" cmd /k "cd Frontend && title Frontend Vue && if not exist node_modules (echo Cai dat dependencies bang npm... && npm install) && npm run dev"

echo Da gui lenh khoi dong cho ca Backend va Frontend o cac cua so rieng biet!
echo =======================================
