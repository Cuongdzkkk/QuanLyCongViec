@echo off
echo =======================================
echo KHOI TAO LAI TOAN BO DATABASE (FORCE RESET)
echo =======================================
echo.
echo 1. Xoa Database hien tai bang SQLCMD (ngat toan bo ket noi)...
sqlcmd -S "KIETNGO" -Q "IF DB_ID('TaskManagementDB') IS NOT NULL BEGIN ALTER DATABASE [TaskManagementDB] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [TaskManagementDB]; END" -E -C

echo.
echo 2. Xoa cac file Migrations cu trong code...
cd Backend\src\TaskManagement.API
if exist "..\TaskManagement.Infrastructure\Migrations" rd /s /q "..\TaskManagement.Infrastructure\Migrations"

echo.
echo 3. Tao lai Migrations tu dau (InitialCreate)...
dotnet ef migrations add InitialCreate --project ../TaskManagement.Infrastructure --startup-project .

echo.
echo 4. Chay Update de tao lai Database moi tinh...
dotnet ef database update --project ../TaskManagement.Infrastructure --startup-project .

echo.
echo =======================================
echo HOAN TAT! DATABASE DA DUOC TAO MOI VA TRONG RONG.
echo Ban bay gio da co the nhan [X] de tat cua so nay. 
echo SAU DO BAN DI RA NGOAI VA CHAY FILE run.bat NHU BINH THUONG DE SEED.
echo =======================================
pause
