-- ============================================================================
-- SPRINTA DEMO SEED DATA — NovaTech Solutions
-- ============================================================================
-- File: seed-demo-data.sql
-- Purpose: Professional demo data for SprintA presentation
-- IDEMPOTENT: Safe to run multiple times. Uses IF NOT EXISTS guards.
-- WARNING: Does NOT modify schema, UI, backend, or delete existing data.
-- Password for all demo users: Demo@123
-- BCrypt hash: $2a$11$K5F.nGm4rVxfWuAX3CiOCOx5dCcnNEzSX0xGVJTB4xfQHKJBfZGEi
-- (This is a valid BCrypt hash of "Demo@123" with cost=11)
-- ============================================================================

SET NOCOUNT ON;
GO

-- ============================================================================
-- 0. ORGANIZATION
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM Organizations WHERE Id = 'org_novatech_001')
BEGIN
    INSERT INTO Organizations (Id, Name, Email, Website, CompanySize, Domain, IsDomainVerified, CreatedAt, UpdatedAt)
    VALUES ('org_novatech_001', N'NovaTech Solutions', 'contact@novatech.vn', 'https://novatech.vn', '51-100', 'novatech.vn', 1, GETUTCDATE(), GETUTCDATE());
END
GO

-- ============================================================================
-- 1. USERS (15 professional Vietnamese users)
-- ============================================================================
-- Required system roles for a fresh demo database
DECLARE @RoleAdmin UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000001';
DECLARE @RolePM UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000002';
DECLARE @RoleLead UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000003';
DECLARE @RolePO UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000004';
DECLARE @RoleDev UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000005';
DECLARE @RoleQA UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000006';
DECLARE @RoleAccountant UNIQUEIDENTIFIER = 'A1000001-0001-0001-0001-000000000007';

IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Admin')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RoleAdmin, 'Admin', 'System Administrator');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'PM')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RolePM, 'PM', 'Project Manager');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Project Lead')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RoleLead, 'Project Lead', 'Project lead access');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'PO')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RolePO, 'PO', 'Product Owner');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Developer')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RoleDev, 'Developer', 'Developer access');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'QA')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RoleQA, 'QA', 'Quality Assurance');
IF NOT EXISTS (SELECT 1 FROM Roles WHERE Name = 'Accountant')
    INSERT INTO Roles (Id, Name, [Description]) VALUES (@RoleAccountant, 'Accountant', 'Accounting access');

DECLARE @AdminConfigPermissions TABLE (Module NVARCHAR(128), Code NVARCHAR(256), Description NVARCHAR(512));
INSERT INTO @AdminConfigPermissions (Module, Code, Description) VALUES
('admin.roles', 'admin.roles.can_view', 'View admin roles'),
('admin.roles', 'admin.roles.create', 'Create admin roles'),
('admin.roles', 'admin.roles.edit', 'Edit admin roles'),
('admin.roles', 'admin.roles.delete', 'Delete admin roles'),
('admin.roles', 'admin.roles.manage_permissions', 'Manage permissions admin roles'),
('teams.dashboard', 'teams.dashboard.can_view', 'View teams dashboard'),
('teams.dashboard', 'teams.dashboard.create', 'Create teams dashboard'),
('teams.dashboard', 'teams.dashboard.edit', 'Edit teams dashboard'),
('teams.dashboard', 'teams.dashboard.delete', 'Delete teams dashboard');

INSERT INTO Permissions (Id, Module, Code, Description)
SELECT NEWID(), seed.Module, seed.Code, seed.Description
FROM @AdminConfigPermissions seed
WHERE NOT EXISTS (SELECT 1 FROM Permissions permission WHERE permission.Code = seed.Code);

UPDATE permission
SET Module = seed.Module,
    Description = seed.Description
FROM Permissions permission
JOIN @AdminConfigPermissions seed ON seed.Code = permission.Code;

INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT role.Id, permission.Id
FROM Roles role
JOIN Permissions permission ON permission.Code IN (SELECT Code FROM @AdminConfigPermissions)
WHERE role.Name IN ('Admin', 'SuperAdmin', 'System Admin', 'Organization Admin', 'AccessAdmin', 'Access Admin')
  AND NOT EXISTS (
      SELECT 1
      FROM RolePermissions existing
      WHERE existing.RoleId = role.Id AND existing.PermissionId = permission.Id
  );

-- BCrypt hash for password "Demo@123" (cost=11)
DECLARE @PwdHash NVARCHAR(200) = '$2a$11$ycr0ueY4j/IfTPLoc43RbeokkEUNw0hXP6D3hOxZH5xuM2v7BzvTa';
DECLARE @Now DATETIME2 = GETUTCDATE();

-- User 1: CEO
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'khoi.nguyen@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000001', 'khoi.nguyen@novatech.vn', @PwdHash, N'Nguyễn Minh Khôi',
    N'CEO & Founder của NovaTech Solutions. 10 năm kinh nghiệm trong lĩnh vực công nghệ và quản lý sản phẩm.',
    N'CEO & Founder', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 2: Product Owner
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'han.tran@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000002', 'han.tran@novatech.vn', @PwdHash, N'Trần Gia Hân',
    N'Product Owner phụ trách SprintA Enterprise. Đam mê xây dựng sản phẩm SaaS đẳng cấp.',
    N'Product Owner', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 3: Project Manager
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'phuc.le@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000003', 'phuc.le@novatech.vn', @PwdHash, N'Lê Hoàng Phúc',
    N'Project Manager với 7 năm kinh nghiệm Agile/Scrum. Chuyên quản lý dự án phần mềm quy mô lớn.',
    N'Project Manager', N'Hà Nội', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 4: Scrum Master
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'thu.pham@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000004', 'thu.pham@novatech.vn', @PwdHash, N'Phạm Anh Thư',
    N'Scrum Master. PSM II certified. Facilitator cho các team Engineering.',
    N'Scrum Master', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 5: Business Analyst
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'nam.vo@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000005', 'nam.vo@novatech.vn', @PwdHash, N'Võ Đức Nam',
    N'Business Analyst. Chuyên phân tích yêu cầu và viết user stories.',
    N'Business Analyst', N'Đà Nẵng', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 6: Frontend Developer
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'ngoc.dang@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000006', 'ngoc.dang@novatech.vn', @PwdHash, N'Đặng Bảo Ngọc',
    N'Frontend Developer. Vue 3, React, TypeScript expert. UI/UX enthusiast.',
    N'Frontend Developer', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 7: Backend Developer
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'bao.huynh@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000007', 'bao.huynh@novatech.vn', @PwdHash, N'Huỳnh Quốc Bảo',
    N'Backend Developer. .NET Core, EF Core, SQL Server, SignalR specialist.',
    N'Backend Developer', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 8: Fullstack Developer
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'tam.nguyen@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000008', 'tam.nguyen@novatech.vn', @PwdHash, N'Nguyễn Thanh Tâm',
    N'Fullstack Developer. Vue 3 + .NET Core. Kinh nghiệm mobile với Flutter.',
    N'Fullstack Developer', N'Hà Nội', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 9: QA Engineer
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'minhanh.bui@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000009', 'minhanh.bui@novatech.vn', @PwdHash, N'Bùi Minh Anh',
    N'QA Engineer. ISTQB certified. Automation testing với Selenium và Cypress.',
    N'QA Engineer', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 10: UI/UX Designer
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'huy.do@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000010', 'huy.do@novatech.vn', @PwdHash, N'Đỗ Quang Huy',
    N'UI/UX Designer. Figma, Sketch expert. Design System advocate.',
    N'UI/UX Designer', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 11: DevOps Engineer
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'long.truong@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000011', 'long.truong@novatech.vn', @PwdHash, N'Trương Thành Long',
    N'DevOps Engineer. Docker, Kubernetes, CI/CD pipeline specialist.',
    N'DevOps Engineer', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 12: Data Analyst
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'linh.ho@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000012', 'linh.ho@novatech.vn', @PwdHash, N'Hồ Khánh Linh',
    N'Data Analyst. SQL, Python, Power BI. Chuyên phân tích dữ liệu kinh doanh.',
    N'Data Analyst', N'Đà Nẵng', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 13: Customer Success
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'trang.le@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000013', 'trang.le@novatech.vn', @PwdHash, N'Lê Thanh Trang',
    N'Customer Success Manager. Chăm sóc khách hàng và hỗ trợ onboarding.',
    N'Customer Success Manager', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 14: Marketing Specialist
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'duc.ngo@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000014', 'duc.ngo@novatech.vn', @PwdHash, N'Ngô Minh Đức',
    N'Marketing Specialist. SEO, Content Marketing, Growth Hacking.',
    N'Marketing Specialist', N'Hà Nội', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- User 15: HR / Operations
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'vy.tran@novatech.vn')
INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
VALUES ('D0000001-0001-0001-0001-000000000015', 'vy.tran@novatech.vn', @PwdHash, N'Trần Tuyết Vy',
    N'HR & Operations Manager. Quản lý nhân sự và quy trình nội bộ.',
    N'HR & Operations Manager', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');

-- Dev admin account used by run.bat demos
UPDATE Users
SET PasswordHash = @PwdHash,
    IsActive = 1,
    IsDeleted = 0,
    UpdatedAt = @Now
WHERE Id = 'D0000001-0001-0001-0001-000000000001'
  AND Email = 'khoi.nguyen@novatech.vn'
  AND OrganizationId = 'org_novatech_001';

DECLARE @DevAdminId UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @DevPwdHash NVARCHAR(200) = '$2a$11$6omz2fbVj58ViA6n1ImIa.P5HIr82ygbY7SpuRny1sHU5KnyG1nEq'; -- dev123

IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'dev@sprinta.local')
BEGIN
    INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
    VALUES (@DevAdminId, 'dev@sprinta.local', @DevPwdHash, N'Dev Admin',
        N'Tai khoan admin demo de chay nhanh SprintA tren may local.',
        N'Administrator', N'TP. Ho Chi Minh', 'Asia/Ho_Chi_Minh', 1, @Now, @Now, 0, 0, 'org_novatech_001');
END
ELSE
BEGIN
    UPDATE Users
    SET PasswordHash = @DevPwdHash,
        FullName = COALESCE(NULLIF(FullName, ''), N'Dev Admin'),
        IsActive = 1,
        IsDeleted = 0,
        UpdatedAt = @Now,
        OrganizationId = COALESCE(OrganizationId, 'org_novatech_001')
    WHERE Email = 'dev@sprinta.local';
END
GO

-- ============================================================================
-- 2. ROLES & USER ROLES
-- ============================================================================
-- Assign Admin role to CEO (khoi.nguyen)
DECLARE @AdminRoleId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE Name = 'Admin');
IF @AdminRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000001' AND RoleId = @AdminRoleId)
    INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000001', @AdminRoleId);

DECLARE @DevAdminId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'dev@sprinta.local');
IF @AdminRoleId IS NOT NULL AND @DevAdminId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = @DevAdminId AND RoleId = @AdminRoleId)
    INSERT INTO UserRoles (UserId, RoleId) VALUES (@DevAdminId, @AdminRoleId);

-- Assign PM role to Project Manager (phuc.le)
DECLARE @PMRoleId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE Name = 'PM');
IF @PMRoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000003' AND RoleId = @PMRoleId)
    INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000003', @PMRoleId);

-- Assign PO role to Product Owner (han.tran)
DECLARE @PORoleId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE Name = 'PO');
IF @PORoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000002' AND RoleId = @PORoleId)
    INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000002', @PORoleId);

-- Assign Developer role
DECLARE @DevRoleId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE Name = 'Developer');
IF @DevRoleId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000006' AND RoleId = @DevRoleId)
        INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000006', @DevRoleId);
    IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000007' AND RoleId = @DevRoleId)
        INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000007', @DevRoleId);
    IF NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000008' AND RoleId = @DevRoleId)
        INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000008', @DevRoleId);
END

-- Assign QA role
DECLARE @QARoleId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Roles WHERE Name = 'QA');
IF @QARoleId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM UserRoles WHERE UserId = 'D0000001-0001-0001-0001-000000000009' AND RoleId = @QARoleId)
    INSERT INTO UserRoles (UserId, RoleId) VALUES ('D0000001-0001-0001-0001-000000000009', @QARoleId);
GO

-- ============================================================================
-- 3. WORKSPACES
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM Workspaces WHERE Slug = 'novatech')
INSERT INTO Workspaces (Id, Slug, Name, Timezone, OwnerId, CreatedAt, UpdatedAt, IsDeleted)
VALUES ('A0000001-0001-0001-0001-000000000001', 'novatech', N'NovaTech Workspace', 'Asia/Ho_Chi_Minh',
    'D0000001-0001-0001-0001-000000000001', GETUTCDATE(), GETUTCDATE(), 0);
GO

-- ============================================================================
-- 4. WORKSPACE MEMBERS (all 15 users)
-- ============================================================================
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';
DECLARE @Now2 DATETIME2 = GETUTCDATE();

-- Owner
IF NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @WsId AND UserId = 'D0000001-0001-0001-0001-000000000001')
INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive) VALUES (@WsId, 'D0000001-0001-0001-0001-000000000001', 'OWNER', @Now2, 1);

DECLARE @DevAdminId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'dev@sprinta.local');
IF @DevAdminId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @WsId AND UserId = @DevAdminId)
INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive) VALUES (@WsId, @DevAdminId, 'ADMIN', @Now2, 1);

-- Admin
IF NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @WsId AND UserId = 'D0000001-0001-0001-0001-000000000002')
INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive) VALUES (@WsId, 'D0000001-0001-0001-0001-000000000002', 'ADMIN', @Now2, 1);

IF NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @WsId AND UserId = 'D0000001-0001-0001-0001-000000000003')
INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive) VALUES (@WsId, 'D0000001-0001-0001-0001-000000000003', 'ADMIN', @Now2, 1);

-- Members (4-15)
DECLARE @i INT = 4;
WHILE @i <= 15
BEGIN
    DECLARE @uid UNIQUEIDENTIFIER = CAST('D0000001-0001-0001-0001-0000000000' + RIGHT('00' + CAST(@i AS VARCHAR), 2) AS UNIQUEIDENTIFIER);
    IF NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @WsId AND UserId = @uid)
        INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive) VALUES (@WsId, @uid, 'MEMBER', @Now2, 1);
    SET @i = @i + 1;
END
GO

-- ============================================================================
-- 5. DEPARTMENTS / TEAMS (10 teams)
-- ============================================================================
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';
DECLARE @Now3 DATETIME2 = GETUTCDATE();

-- Ban Điều Hành
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000001')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000001', N'Ban Điều Hành', 'D0000001-0001-0001-0001-000000000001', @Now3, 1, 0, 0, N'Ban lãnh đạo công ty, định hướng chiến lược và ra quyết định cấp cao.');

-- Product Management
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000002')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000002', N'Product Management', 'D0000001-0001-0001-0001-000000000002', @Now3, 1, 0, 0, N'Quản lý sản phẩm, roadmap và ưu tiên tính năng.');

-- Engineering
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000003')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000003', N'Engineering', 'D0000001-0001-0001-0001-000000000003', @Now3, 1, 0, 0, N'Đội ngũ kỹ sư phát triển phần mềm.');

-- Frontend Team (child of Engineering)
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000004')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, ParentId, [Description])
VALUES ('B0000001-0001-0001-0001-000000000004', N'Frontend Team', 'D0000001-0001-0001-0001-000000000006', @Now3, 1, 0, 0, 'B0000001-0001-0001-0001-000000000003', N'Phát triển giao diện người dùng với Vue 3, Element Plus.');

-- Backend Team (child of Engineering)
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000005')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, ParentId, [Description])
VALUES ('B0000001-0001-0001-0001-000000000005', N'Backend Team', 'D0000001-0001-0001-0001-000000000007', @Now3, 1, 0, 0, 'B0000001-0001-0001-0001-000000000003', N'Phát triển API, database và hạ tầng backend với .NET Core.');

-- QA & Testing
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000006')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000006', N'QA & Testing', 'D0000001-0001-0001-0001-000000000009', @Now3, 1, 0, 0, N'Kiểm thử chất lượng phần mềm, viết test case và automation testing.');

-- UI/UX Design
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000007')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000007', N'UI/UX Design', 'D0000001-0001-0001-0001-000000000010', @Now3, 1, 0, 0, N'Thiết kế giao diện, trải nghiệm người dùng và Design System.');

-- DevOps & Infrastructure
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000008')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000008', N'DevOps & Infrastructure', 'D0000001-0001-0001-0001-000000000011', @Now3, 1, 0, 0, N'Quản lý hạ tầng, CI/CD, deployment và monitoring.');

-- Customer Success
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000009')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000009', N'Customer Success', 'D0000001-0001-0001-0001-000000000013', @Now3, 1, 0, 0, N'Hỗ trợ khách hàng, onboarding và đảm bảo sự hài lòng.');

-- Marketing & Growth
IF NOT EXISTS (SELECT 1 FROM Departments WHERE Id = 'B0000001-0001-0001-0001-000000000010')
INSERT INTO Departments (Id, Name, ManagerId, CreatedAt, IsActive, IsDeleted, Require2FA, [Description])
VALUES ('B0000001-0001-0001-0001-000000000010', N'Marketing & Growth', 'D0000001-0001-0001-0001-000000000014', @Now3, 1, 0, 0, N'Marketing, SEO, content và chiến lược tăng trưởng.');
GO

-- ============================================================================
-- 6. DEPARTMENT MEMBERS
-- ============================================================================
DECLARE @Now4 DATETIME2 = GETUTCDATE();

-- Ban Điều Hành: CEO, PO, PM
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000001')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000002')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000002', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000003')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000003', @Now4);

-- Product Management: PO, BA, Scrum Master
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000002')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000002', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000004')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000004', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000005')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000005', @Now4);

-- Frontend Team: FE Dev, Fullstack, Designer
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000006')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000006', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000008')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000008', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000010')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000010', @Now4);

-- Backend Team: BE Dev, Fullstack, DevOps
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000007')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000007', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000008')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000008', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000011')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000011', @Now4);

-- QA: QA Engineer, BA
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000009')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000009', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000005')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000005', @Now4);

-- UI/UX Design: Designer
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000007' AND UserId = 'D0000001-0001-0001-0001-000000000010')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000007', 'D0000001-0001-0001-0001-000000000010', @Now4);

-- DevOps: DevOps Eng
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000008' AND UserId = 'D0000001-0001-0001-0001-000000000011')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000008', 'D0000001-0001-0001-0001-000000000011', @Now4);

-- Customer Success
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000009' AND UserId = 'D0000001-0001-0001-0001-000000000013')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000009', 'D0000001-0001-0001-0001-000000000013', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000009' AND UserId = 'D0000001-0001-0001-0001-000000000012')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000009', 'D0000001-0001-0001-0001-000000000012', @Now4);

-- Marketing & Growth
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000010' AND UserId = 'D0000001-0001-0001-0001-000000000014')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000010', 'D0000001-0001-0001-0001-000000000014', @Now4);
IF NOT EXISTS (SELECT 1 FROM DepartmentMembers WHERE DepartmentId = 'B0000001-0001-0001-0001-000000000010' AND UserId = 'D0000001-0001-0001-0001-000000000015')
INSERT INTO DepartmentMembers (DepartmentId, UserId, JoinedAt) VALUES ('B0000001-0001-0001-0001-000000000010', 'D0000001-0001-0001-0001-000000000015', @Now4);
GO

-- ============================================================================
-- 7. PROJECTS (6 projects)
-- ============================================================================
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';
DECLARE @Now5 DATETIME2 = GETUTCDATE();

-- Project 1: SprintA Enterprise Platform
IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = 'C0000001-0001-0001-0001-000000000001')
INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType, DepartmentId, Why, SuccessCriteria)
VALUES ('C0000001-0001-0001-0001-000000000001', N'SprintA Enterprise Platform',
    N'Nâng cấp nền tảng SprintA thành hệ thống quản lý công việc cấp doanh nghiệp với đầy đủ tính năng Kanban, Sprint, Goal, OKR, Wiki, AI Assistant.',
    'SPRINT', 50, DATEADD(MONTH, -3, @Now5), DATEADD(MONTH, 3, @Now5), 1,
    'D0000001-0001-0001-0001-000000000002', @WsId, @Now5, @Now5, 0, 0, 'Public',
    'B0000001-0001-0001-0001-000000000003',
    N'Xây dựng nền tảng quản lý công việc hoàn chỉnh, đủ tính năng để thay thế Jira/Plane cho doanh nghiệp SME.',
    N'Hoàn thiện 90% tính năng core, đạt 95% test coverage, xử lý được 1000 concurrent users.');

-- Project 2: Integration Hub & Unified Inbox
IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = 'C0000001-0001-0001-0001-000000000002')
INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType, DepartmentId)
VALUES ('C0000001-0001-0001-0001-000000000002', N'Integration Hub & Unified Inbox',
    N'Kết nối Google Calendar, GitHub, Slack, Microsoft Mail và gom thông báo về một inbox chung cho mỗi user.',
    'INTHUB', 25, DATEADD(MONTH, -2, @Now5), DATEADD(MONTH, 2, @Now5), 1,
    'D0000001-0001-0001-0001-000000000007', @WsId, @Now5, @Now5, 0, 0, 'Public',
    'B0000001-0001-0001-0001-000000000005');

-- Project 3: Mobile App MVP
IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = 'C0000001-0001-0001-0001-000000000003')
INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType)
VALUES ('C0000001-0001-0001-0001-000000000003', N'Mobile App MVP',
    N'Phát triển ứng dụng mobile cho quản lý task, sprint và notification bằng Flutter.',
    'MOBILE', 15, DATEADD(MONTH, -1, @Now5), DATEADD(MONTH, 4, @Now5), 1,
    'D0000001-0001-0001-0001-000000000008', @WsId, @Now5, @Now5, 0, 0, 'Public');

-- Project 4: Customer Success Portal
IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = 'C0000001-0001-0001-0001-000000000004')
INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType, DepartmentId)
VALUES ('C0000001-0001-0001-0001-000000000004', N'Customer Success Portal',
    N'Xây dựng cổng hỗ trợ khách hàng, tài liệu hướng dẫn và quy trình phản hồi.',
    'CSPORT', 12, DATEADD(MONTH, -2, @Now5), DATEADD(MONTH, 1, @Now5), 1,
    'D0000001-0001-0001-0001-000000000013', @WsId, @Now5, @Now5, 0, 0, 'Public',
    'B0000001-0001-0001-0001-000000000009');

-- Project 5: Website Landing & Growth Campaign
IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = 'C0000001-0001-0001-0001-000000000005')
INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType, DepartmentId)
VALUES ('C0000001-0001-0001-0001-000000000005', N'Website Landing & Growth Campaign',
    N'Tối ưu landing page, SEO, tracking conversion và chiến dịch marketing.',
    'GROWTH', 10, DATEADD(MONTH, -1, @Now5), DATEADD(MONTH, 2, @Now5), 1,
    'D0000001-0001-0001-0001-000000000014', @WsId, @Now5, @Now5, 0, 0, 'Public',
    'B0000001-0001-0001-0001-000000000010');

-- Project 6: Internal Operations Automation
IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = 'C0000001-0001-0001-0001-000000000006')
INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType)
VALUES ('C0000001-0001-0001-0001-000000000006', N'Internal Operations Automation',
    N'Tự động hóa quy trình nội bộ: phê duyệt, báo cáo, audit và quản lý nhân sự.',
    'OPS', 8, DATEADD(MONTH, 0, @Now5), DATEADD(MONTH, 5, @Now5), 1,
    'D0000001-0001-0001-0001-000000000015', @WsId, @Now5, @Now5, 0, 0, 'Public');
GO

-- ============================================================================
-- 8. PROJECT MEMBERS
-- ============================================================================
DECLARE @Now6 DATETIME2 = GETUTCDATE();

-- Project 1 (SPRINT): 10 members
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000002')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000002', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000003')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000003', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000004')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000004', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000005')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000005', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000006')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000006', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000007')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000007', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000008')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000008', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000009')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000009', 'TESTER', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000010')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000010', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000011')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000011', 'DEV', @Now6, NULL, 1);

-- Project 2 (INTHUB): 6 members
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000007')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000007', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000006')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000008')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000008', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000009')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000009', 'TESTER', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000003')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000003', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000011')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000011', 'DEV', @Now6, NULL, 1);

-- Project 3 (MOBILE): 5 members
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000008')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000008', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000006')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000006', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000010')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000010', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000009')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000009', 'TESTER', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000002')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000002', 'PM', @Now6, NULL, 1);

-- Project 4 (CSPORT): 4 members
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000013')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000013', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000012')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000012', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000006')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000006', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000002')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000002', 'PM', @Now6, NULL, 1);

-- Project 5 (GROWTH): 4 members
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000014')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000014', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000010')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000010', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000006')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000006', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000012')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000012', 'DEV', @Now6, NULL, 1);

-- Project 6 (OPS): 4 members
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000015')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000015', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000001')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000001', 'PM', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000007')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000007', 'DEV', @Now6, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000011')
INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000011', 'DEV', @Now6, NULL, 1);

DECLARE @DevAdminId UNIQUEIDENTIFIER = (SELECT TOP 1 Id FROM Users WHERE Email = 'dev@sprinta.local');
IF @DevAdminId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000001' AND UserId = @DevAdminId)
        INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000001', @DevAdminId, 'PM', @Now6, NULL, 1);
    IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000002' AND UserId = @DevAdminId)
        INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000002', @DevAdminId, 'PM', @Now6, NULL, 1);
    IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000003' AND UserId = @DevAdminId)
        INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000003', @DevAdminId, 'PM', @Now6, NULL, 1);
    IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000004' AND UserId = @DevAdminId)
        INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000004', @DevAdminId, 'PM', @Now6, NULL, 1);
    IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000005' AND UserId = @DevAdminId)
        INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000005', @DevAdminId, 'PM', @Now6, NULL, 1);
    IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = 'C0000001-0001-0001-0001-000000000006' AND UserId = @DevAdminId)
        INSERT INTO ProjectMembers VALUES ('C0000001-0001-0001-0001-000000000006', @DevAdminId, 'PM', @Now6, NULL, 1);
END
GO

PRINT N'✅ Part 1 complete: Organization, Users, Workspaces, Departments, Projects, Members';
GO

-- ============================================================================
-- 9. TASK STATUSES (5 statuses per project for all 6 projects)
-- ============================================================================
DECLARE @NowStatuses DATETIME2 = GETUTCDATE();

-- Project 1 (SPRINT)
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0001-0001-0001-000000000001')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0001-0001-0001-000000000002')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0001-0001-0001-000000000003')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0001-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000001', 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0001-0001-0001-000000000004')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0001-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000001', 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0001-0001-0001-000000000005')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0001-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000001', 'DONE', '#10b981', 4);

-- Project 2 (INTHUB)
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0002-0001-0001-000000000001')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0002-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000002', 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0002-0001-0001-000000000002')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0002-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000002', 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0002-0001-0001-000000000003')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0002-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000002', 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0002-0001-0001-000000000004')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0002-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000002', 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0002-0001-0001-000000000005')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0002-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000002', 'DONE', '#10b981', 4);

-- Project 3 (MOBILE)
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0003-0001-0001-000000000001')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0003-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000003', 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0003-0001-0001-000000000002')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0003-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000003', 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0003-0001-0001-000000000003')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0003-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000003', 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0003-0001-0001-000000000004')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0003-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000003', 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0003-0001-0001-000000000005')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0003-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000003', 'DONE', '#10b981', 4);

-- Project 4 (CSPORT)
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0004-0001-0001-000000000001')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0004-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000004', 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0004-0001-0001-000000000002')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0004-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000004', 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0004-0001-0001-000000000003')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0004-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000004', 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0004-0001-0001-000000000004')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0004-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000004', 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0004-0001-0001-000000000005')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0004-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000004', 'DONE', '#10b981', 4);

-- Project 5 (GROWTH)
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0005-0001-0001-000000000001')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0005-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000005', 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0005-0001-0001-000000000002')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0005-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000005', 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0005-0001-0001-000000000003')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0005-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000005', 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0005-0001-0001-000000000004')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0005-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000005', 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0005-0001-0001-000000000005')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0005-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000005', 'DONE', '#10b981', 4);

-- Project 6 (OPS)
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0006-0001-0001-000000000001')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0006-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000006', 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0006-0001-0001-000000000002')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0006-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000006', 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0006-0001-0001-000000000003')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0006-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000006', 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0006-0001-0001-000000000004')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0006-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000006', 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'E0000001-0006-0001-0001-000000000005')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('E0000001-0006-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000006', 'DONE', '#10b981', 4);
GO

-- ============================================================================
-- 10. TASK TYPES (Task, Bug, Story, Epic per project)
-- ============================================================================
-- Project 1
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0001-0001-0001-000000000001')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', 'Task', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0001-0001-0001-000000000002')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', 'Bug', '#ef4444', 'bug');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0001-0001-0001-000000000003')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0001-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000001', 'Story', '#10b981', 'bookmark');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0001-0001-0001-000000000004')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0001-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000001', 'Epic', '#8b5cf6', 'crown');

-- Project 2
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0002-0001-0001-000000000001')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0002-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000002', 'Task', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0002-0001-0001-000000000002')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0002-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000002', 'Bug', '#ef4444', 'bug');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0002-0001-0001-000000000003')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0002-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000002', 'Story', '#10b981', 'bookmark');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0002-0001-0001-000000000004')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0002-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000002', 'Epic', '#8b5cf6', 'crown');

-- Project 3
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0003-0001-0001-000000000001')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0003-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000003', 'Task', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0003-0001-0001-000000000002')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0003-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000003', 'Bug', '#ef4444', 'bug');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0003-0001-0001-000000000003')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0003-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000003', 'Story', '#10b981', 'bookmark');

-- Project 4
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0004-0001-0001-000000000001')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0004-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000004', 'Task', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0004-0001-0001-000000000002')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0004-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000004', 'Bug', '#ef4444', 'bug');

-- Project 5
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0005-0001-0001-000000000001')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0005-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000005', 'Task', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0005-0001-0001-000000000002')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0005-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000005', 'Bug', '#ef4444', 'bug');

-- Project 6
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0006-0001-0001-000000000001')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0006-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000006', 'Task', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'F0000001-0006-0001-0001-000000000002')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('F0000001-0006-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000006', 'Bug', '#ef4444', 'bug');
GO

-- ============================================================================
-- 11. SPRINTS (2-3 Sprints per project)
-- ============================================================================
DECLARE @NowSprints DATETIME2 = GETUTCDATE();

-- Project 1 (SPRINT)
-- Sprint 1 (Past, Completed)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0001-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', N'Sprint 1: Core Architecture & Setup', DATEADD(DAY, -30, @NowSprints), DATEADD(DAY, -16, @NowSprints), 0, 0, DATEADD(DAY, -35, @NowSprints));

-- Sprint 2 (Active, In progress)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0001-0001-0001-000000000002')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', N'Sprint 2: Kanban & Task Management Board', DATEADD(DAY, -15, @NowSprints), DATEADD(DAY, -1, @NowSprints), 1, 1, DATEADD(DAY, -35, @NowSprints));

-- Sprint 3 (Future, Planning)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0001-0001-0001-000000000003')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0001-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000001', N'Sprint 3: AI Integration & Wiki Docs', DATEADD(DAY, 0, @NowSprints), DATEADD(DAY, 14, @NowSprints), 0, 0, DATEADD(DAY, -35, @NowSprints));


-- Project 2 (INTHUB)
-- Sprint 1 (Active)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0002-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0002-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000002', N'Sprint 1: Google Calendar & OAuth', DATEADD(DAY, -10, @NowSprints), DATEADD(DAY, 4, @NowSprints), 1, 0, DATEADD(DAY, -12, @NowSprints));

-- Sprint 2 (Future)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0002-0001-0001-000000000002')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0002-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000002', N'Sprint 2: GitHub & Slack Webhooks', DATEADD(DAY, 5, @NowSprints), DATEADD(DAY, 19, @NowSprints), 0, 0, DATEADD(DAY, -12, @NowSprints));


-- Project 3 (MOBILE)
-- Sprint 1 (Active)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0003-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0003-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000003', N'Sprint 1: Flutter Setup & Auth Flow', DATEADD(DAY, -7, @NowSprints), DATEADD(DAY, 7, @NowSprints), 1, 0, DATEADD(DAY, -8, @NowSprints));


-- Project 4 (CSPORT)
-- Sprint 1 (Active)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0004-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0004-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000004', N'Sprint 1: Portal Dashboard & Tickets', DATEADD(DAY, -5, @NowSprints), DATEADD(DAY, 9, @NowSprints), 1, 0, DATEADD(DAY, -6, @NowSprints));


-- Project 5 (GROWTH)
-- Sprint 1 (Active)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0005-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0005-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000005', N'Sprint 1: SEO & Conversion Funnel', DATEADD(DAY, -3, @NowSprints), DATEADD(DAY, 11, @NowSprints), 1, 0, DATEADD(DAY, -4, @NowSprints));


-- Project 6 (OPS)
-- Sprint 1 (Active)
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '30000001-0006-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('30000001-0006-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000006', N'Sprint 1: Approval Workflow Config', DATEADD(DAY, -1, @NowSprints), DATEADD(DAY, 13, @NowSprints), 1, 0, DATEADD(DAY, -2, @NowSprints));
GO

-- ============================================================================
-- 12. MODULES (Epic/Feature clusters per project)
-- ============================================================================
DECLARE @NowMods DATETIME2 = GETUTCDATE();

-- Project 1 (SPRINT)
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'c0000001-0001-0001-0001-000000000001')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('c0000001-0001-0001-0001-000000000001', N'Authentication & Access Management', N'Xây dựng phân quyền chi tiết, SSO và cơ chế refresh token an toàn.', 'C0000001-0001-0001-0001-000000000001', DATEADD(MONTH, -3, @NowMods), DATEADD(MONTH, -1, @NowMods), 'Completed', 'D0000001-0001-0001-0001-000000000007', @NowMods, @NowMods);

IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'c0000001-0001-0001-0001-000000000002')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('c0000001-0001-0001-0001-000000000002', N'Kanban Board & LexoRank Reordering', N'Phát triển thuật toán LexoRank hỗ trợ kéo thả Kanban mượt mà, phản hồi realtime qua SignalR.', 'C0000001-0001-0001-0001-000000000001', DATEADD(MONTH, -2, @NowMods), DATEADD(MONTH, 0, @NowMods), 'InProgress', 'D0000001-0001-0001-0001-000000000006', @NowMods, @NowMods);

IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'c0000001-0001-0001-0001-000000000003')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('c0000001-0001-0001-0001-000000000003', N'AI Intelligence & Chatbot integration', N'Hỗ trợ gợi ý mô tả công việc, tổng hợp tiến độ và phân tích hiệu suất bằng trí tuệ nhân tạo.', 'C0000001-0001-0001-0001-000000000001', DATEADD(MONTH, -1, @NowMods), DATEADD(MONTH, 2, @NowMods), 'Planned', 'D0000001-0001-0001-0001-000000000008', @NowMods, @NowMods);


-- Project 2 (INTHUB)
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'c0000001-0002-0001-0001-000000000001')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('c0000001-0002-0001-0001-000000000001', N'OAuth 2.0 Integration Gateways', N'Cổng xác thực tích hợp cho Google, Microsoft và GitHub.', 'C0000001-0001-0001-0001-000000000002', DATEADD(MONTH, -2, @NowMods), DATEADD(MONTH, -1, @NowMods), 'Completed', 'D0000001-0001-0001-0001-000000000007', @NowMods, @NowMods);

IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'c0000001-0002-0001-0001-000000000002')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('c0000001-0002-0001-0001-000000000002', N'Realtime Inbox Notification Hub', N'Hệ thống thu thập và phân bổ thông báo realtime.', 'C0000001-0001-0001-0001-000000000002', DATEADD(MONTH, -1, @NowMods), DATEADD(MONTH, 1, @NowMods), 'InProgress', 'D0000001-0001-0001-0001-000000000011', @NowMods, @NowMods);


-- Project 3 (MOBILE)
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'c0000001-0003-0001-0001-000000000001')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('c0000001-0003-0001-0001-000000000001', N'Flutter Mobile Core Shell', N'Thiết lập cấu trúc thư mục, router và state management cho mobile app.', 'C0000001-0001-0001-0001-000000000003', DATEADD(MONTH, -1, @NowMods), DATEADD(MONTH, 1, @NowMods), 'InProgress', 'D0000001-0001-0001-0001-000000000008', @NowMods, @NowMods);
GO

-- ============================================================================
-- 13. LABELS (Task Classification Labels per project)
-- ============================================================================
DECLARE @NowLabels DATETIME2 = GETUTCDATE();

-- Project 1 (SPRINT)
IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0001-0001-0001-000000000001')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0001-0001-0001-000000000001', 'Bug', '#ef4444', N'Lỗi hệ thống cần sửa đổi.', 'C0000001-0001-0001-0001-000000000001', 'A0000001-0001-0001-0001-000000000001', @NowLabels);

IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0001-0001-0001-000000000002')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0001-0001-0001-000000000002', 'Feature', '#3b82f6', N'Tính năng mới cần phát triển.', 'C0000001-0001-0001-0001-000000000001', 'A0000001-0001-0001-0001-000000000001', @NowLabels);

IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0001-0001-0001-000000000003')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0001-0001-0001-000000000003', 'High-Priority', '#f59e0b', N'Công việc có độ ưu tiên cao.', 'C0000001-0001-0001-0001-000000000001', 'A0000001-0001-0001-0001-000000000001', @NowLabels);

IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0001-0001-0001-000000000004')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0001-0001-0001-000000000004', 'Backend', '#8b5cf6', N'Công việc liên quan đến Backend/API/Database.', 'C0000001-0001-0001-0001-000000000001', 'A0000001-0001-0001-0001-000000000001', @NowLabels);

IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0001-0001-0001-000000000005')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0001-0001-0001-000000000005', 'Frontend', '#10b981', N'Công việc liên quan đến giao diện/CSS/Pinia.', 'C0000001-0001-0001-0001-000000000001', 'A0000001-0001-0001-0001-000000000001', @NowLabels);


-- Project 2 (INTHUB)
IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0002-0001-0001-000000000001')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0002-0001-0001-000000000001', 'Integration', '#00b8d9', N'Liên quan đến kết nối API bên thứ ba.', 'C0000001-0001-0001-0001-000000000002', 'A0000001-0001-0001-0001-000000000001', @NowLabels);

IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'b0000001-0002-0001-0001-000000000002')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt)
    VALUES ('b0000001-0002-0001-0001-000000000002', 'API-Gateway', '#c97cf4', N'Xây dựng endpoint API gateway.', 'C0000001-0001-0001-0001-000000000002', 'A0000001-0001-0001-0001-000000000001', @NowLabels);
GO

PRINT N'✅ Part 2 complete: TaskStatuses, TaskTypes, Sprints, Modules, Labels';
GO

-- ============================================================================
-- 14. WORKTASKS (Tasks and Bugs across all 6 projects)
-- ============================================================================
DECLARE @NowTasks DATETIME2 = GETUTCDATE();
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

-- Project 1 (SPRINT) - 25 tasks
-- Status Done
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000001', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000005',
    N'Khảo sát và thiết lập cấu trúc database Clean Architecture', N'Nghiên cứu các thực thể chính của dự án, quan hệ 1-N, N-N và thiết lập DbContext.', 4, 5, DATEADD(DAY, -28, @NowTasks), DATEADD(DAY, -25, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, -25, @NowTasks), DATEADD(DAY, -30, @NowTasks), DATEADD(DAY, -25, @NowTasks), 0, 0, 16, 18, 10000, 'SPRINT-1', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000001', 'F0000001-0001-0001-0001-000000000003', 'E0000001-0001-0001-0001-000000000005',
    N'Thiết lập giao diện khung và cấu hình Tailwind CSS', N'Tạo khung dự án Vue 3 + Element Plus + Tailwind, cấu hình các biến CSS biến màu sắc cơ bản.', 3, 3, DATEADD(DAY, -27, @NowTasks), DATEADD(DAY, -24, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, -24, @NowTasks), DATEADD(DAY, -30, @NowTasks), DATEADD(DAY, -24, @NowTasks), 0, 0, 12, 10, 20000, 'SPRINT-2', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000003')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000001', 'F0000001-0001-0001-0001-000000000002', 'E0000001-0001-0001-0001-000000000005',
    N'Lỗi: Token hết hạn không tự động làm mới (refresh token)', N'Fix lỗi interceptor của axiosClient không gọi được API refresh token khi nhận HTTP 401.', 1, 2, DATEADD(DAY, -23, @NowTasks), DATEADD(DAY, -22, @NowTasks), 'D0000001-0001-0001-0001-000000000009', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, -22, @NowTasks), DATEADD(DAY, -25, @NowTasks), DATEADD(DAY, -22, @NowTasks), 0, 0, 8, 6, 30000, 'SPRINT-3', @WsId);

-- Status In Progress
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000004')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000003',
    N'Phát triển API kéo thả Kanban dựa trên thuật toán LexoRank', N'Viết thuật toán tính sortOrder trung bình của phần tử trước và sau để hỗ trợ xếp hạng vô hạn.', 4, 8, DATEADD(DAY, -14, @NowTasks), DATEADD(DAY, -1, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, -1, @NowTasks), DATEADD(DAY, -15, @NowTasks), DATEADD(DAY, 0, @NowTasks), 0, 0, 24, 16, 40000, 'SPRINT-4', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000005')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000003', 'E0000001-0001-0001-0001-000000000003',
    N'Giao diện kéo thả Kanban mượt mà với Smooth-dnd và Vue-draggable', N'Tạo component Kanbanboard, tích hợp kéo thả card, tự động call API update SortOrder.', 3, 5, DATEADD(DAY, -13, @NowTasks), DATEADD(DAY, -2, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, -15, @NowTasks), DATEADD(DAY, 0, @NowTasks), 0, 0, 20, 15, 50000, 'SPRINT-5', @WsId);

-- Status In Review
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000006')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000006', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000004',
    N'Tích hợp SignalR push notify khi có thay đổi trạng thái Task', N'KanbanHub gửi broadcast event để toàn bộ member trong dự án thấy card dịch chuyển tức thời.', 3, 3, DATEADD(DAY, -10, @NowTasks), DATEADD(DAY, -3, @NowTasks), 'D0000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, -12, @NowTasks), DATEADD(DAY, -3, @NowTasks), 0, 0, 16, 12, 60000, 'SPRINT-6', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000007')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000007', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000002', 'E0000001-0001-0001-0001-000000000004',
    N'Lỗi: UI bị giật khi kéo thả card quá nhanh trên Firefox', N'Khắc phục CSS transition và lớp phủ khi drag trên Firefox khiến layout bị lệch 1-2px.', 2, 1, DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, -2, @NowTasks), 'D0000001-0001-0001-0001-000000000009', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, -6, @NowTasks), DATEADD(DAY, -2, @NowTasks), 0, 0, 4, 3, 70000, 'SPRINT-7', @WsId);

-- Status To Do
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000008')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000008', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000003', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000002',
    N'Xây dựng công cụ soạn thảo Wiki Markdown dạng Notion Block editor', N'Tạo component editor sử dụng Tiptap hoặc Editor.js, hỗ trợ tạo block kéo thả và format phong phú.', 3, 8, DATEADD(DAY, 0, @NowTasks), DATEADD(DAY, 10, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, 12, @NowTasks), @NowTasks, @NowTasks, 0, 0, 32, 0, 80000, 'SPRINT-8', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000009')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000009', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000003', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000002',
    N'Phát triển API kết nối mô hình ngôn ngữ lớn để gợi ý mô tả Task', N'Viết service kết nối Gemini API để tự động gen tả chi tiết công việc dựa trên tiêu đề.', 2, 5, DATEADD(DAY, 1, @NowTasks), DATEADD(DAY, 8, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, 10, @NowTasks), @NowTasks, @NowTasks, 0, 0, 20, 0, 90000, 'SPRINT-9', @WsId);

-- Status Backlog
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000010')
    INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000010', 'C0000001-0001-0001-0001-000000000001', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000001',
    N'Hệ thống Gamification tích lũy điểm thưởng khi đóng góp code', N'Tạo ví cá nhân cho từng user, ghi lại lịch sử cộng điểm kudo và đổi quà.', 2, 5, 'D0000001-0001-0001-0001-000000000001', @NowTasks, @NowTasks, 0, 0, 24, 0, 100000, 'SPRINT-10', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000011')
    INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000011', 'C0000001-0001-0001-0001-000000000001', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000001',
    N'Báo cáo tự động OKR định kỳ gửi qua Slack', N'Schedule job chạy mỗi chiều thứ Sáu, tự quét tiến độ OKR/Goal và gửi tóm tắt vào channel Slack.', 1, 3, 'D0000001-0001-0001-0001-000000000002', @NowTasks, @NowTasks, 0, 0, 12, 0, 110000, 'SPRINT-11', @WsId);


-- Project 2 (INTHUB) - 10 tasks
-- Done
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0002-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0002-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000002', '30000001-0002-0001-0001-000000000001', 'F0000001-0002-0001-0001-000000000001', 'E0000001-0002-0001-0001-000000000005',
    N'Tích hợp Google OAuth và lưu trữ Token an toàn', N'Xây dựng luồng xác thực đăng nhập Google, lưu trữ AccessToken và RefreshToken mã hóa trong database.', 4, 3, DATEADD(DAY, -9, @NowTasks), DATEADD(DAY, -6, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, -6, @NowTasks), DATEADD(DAY, -12, @NowTasks), DATEADD(DAY, -6, @NowTasks), 0, 0, 12, 12, 10000, 'INTHUB-1', @WsId);

-- In Progress
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0002-0001-0001-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0002-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000002', '30000001-0002-0001-0001-000000000001', 'F0000001-0002-0001-0001-000000000001', 'E0000001-0002-0001-0001-000000000003',
    N'API Webhook GitHub lắng nghe Pull Request và Commits', N'Tạo Endpoint đón event github. Khi PR merged, tự động đổi status task liên quan.', 3, 5, DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, 2, @NowTasks), 'D0000001-0001-0001-0001-000000000007', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, 2, @NowTasks), DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, 2, @NowTasks), 0, 0, 16, 8, 20000, 'INTHUB-2', @WsId);

-- To Do
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0002-0001-0001-000000000003')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0002-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000002', '30000001-0002-0001-0001-000000000002', 'F0000001-0002-0001-0001-000000000001', 'E0000001-0002-0001-0001-000000000002',
    N'Tích hợp thông báo Slack qua Incoming Webhook', N'Cấu hình bot tự động thông báo vào channel Slack khi dự án có Project Update mới.', 2, 3, DATEADD(DAY, 6, @NowTasks), DATEADD(DAY, 12, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000011', DATEADD(DAY, 15, @NowTasks), @NowTasks, @NowTasks, 0, 0, 10, 0, 30000, 'INTHUB-3', @WsId);


-- Project 3 (MOBILE) - 10 tasks
-- In Progress
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0003-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0003-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000003', '30000001-0003-0001-0001-000000000001', 'F0000001-0003-0001-0001-000000000001', 'E0000001-0003-0001-0001-000000000003',
    N'Thiết lập khung dự án Flutter và quản lý state với Riverpod', N'Tạo base project, cấu hình theme sáng tối, cài đặt thư viện dio và phân vùng thư mục tính năng.', 4, 5, DATEADD(DAY, -6, @NowTasks), DATEADD(DAY, 3, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, 5, @NowTasks), DATEADD(DAY, -8, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 20, 16, 10000, 'MOBILE-1', @WsId);


-- Project 4 (CSPORT) - 10 tasks
-- In Progress
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0004-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0004-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000004', '30000001-0004-0001-0001-000000000001', 'F0000001-0004-0001-0001-000000000001', 'E0000001-0004-0001-0001-000000000003',
    N'Tạo form gửi ticket hỗ trợ khách hàng', N'Thiết kế giao diện form sạch, cho phép upload ảnh đính kèm lỗi và ghi nhận vào bảng Intakes.', 3, 3, DATEADD(DAY, -4, @NowTasks), DATEADD(DAY, 5, @NowTasks), 'D0000001-0001-0001-0001-000000000013', 'D0000001-0001-0001-0001-000000000012', DATEADD(DAY, 8, @NowTasks), DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 12, 8, 10000, 'CSPORT-1', @WsId);


-- Project 5 (GROWTH) - 10 tasks
-- In Progress
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0005-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0005-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000005', '30000001-0005-0001-0001-000000000001', 'F0000001-0005-0001-0001-000000000001', 'E0000001-0005-0001-0001-000000000003',
    N'Thiết lập cấu trúc SEO Meta và sitemap.xml', N'Tạo cấu trúc thẻ meta động theo từng trang tài liệu Wiki và sitemap để tăng khả năng index.', 3, 2, DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, 5, @NowTasks), 'D0000001-0001-0001-0001-000000000014', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, 8, @NowTasks), DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 8, 6, 10000, 'GROWTH-1', @WsId);


-- Project 6 (OPS) - 10 tasks
-- In Progress
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0006-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0006-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000006', '30000001-0006-0001-0001-000000000001', 'F0000001-0006-0001-0001-000000000001', 'E0000001-0006-0001-0001-000000000003',
    N'Thiết lập quy trình phê duyệt yêu cầu nghỉ phép và mua sắm thiết bị', N'Xây dựng các bước phê duyệt từ Line Manager đến HR và Director qua trạng thái duyệt.', 3, 3, DATEADD(DAY, -1, @NowTasks), DATEADD(DAY, 6, @NowTasks), 'D0000001-0001-0001-0001-000000000015', 'D0000001-0001-0001-0001-000000000011', DATEADD(DAY, 10, @NowTasks), DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 12, 4, 10000, 'OPS-1', @WsId);
GO

-- Project 1 (SPRINT) - Continued tasks
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';
DECLARE @NowTasks DATETIME2 = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000012')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000012', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000003',
    N'Tối ưu hóa các truy vấn SQL bằng Index trên WorkspaceId và ProjectId', N'Cải thiện hiệu năng load Kanban board bằng cách đánh Index cho các cột ForeignKey.', 4, 3, DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, -1, @NowTasks), 'D0000001-0001-0001-0001-000000000007', 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, -1, @NowTasks), DATEADD(DAY, -6, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 10, 8, 120000, 'SPRINT-12', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000013')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000013', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000002', 'E0000001-0001-0001-0001-000000000003',
    N'Lỗi: Thiếu phân trang API Audit Log làm chậm trang quản trị viên', N'API log đang trả về toàn bộ dữ liệu mà không phân trang. Sửa thành Skip/Take (Limit/Offset).', 2, 2, DATEADD(DAY, -4, @NowTasks), DATEADD(DAY, -2, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, -2, @NowTasks), 0, 0, 6, 6, 130000, 'SPRINT-13', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000014')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000014', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000004',
    N'Tạo trang quản lý hồ sơ cá nhân và cài đặt thông báo', N'Thiết kế giao diện cập nhật thông tin cá nhân, đổi avatar và bật tắt 2FA.', 3, 3, DATEADD(DAY, -8, @NowTasks), DATEADD(DAY, -4, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, -10, @NowTasks), DATEADD(DAY, -4, @NowTasks), 0, 0, 16, 14, 140000, 'SPRINT-14', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0001-0001-0001-000000000015')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0001-0001-0001-000000000015', 'C0000001-0001-0001-0001-000000000001', '30000001-0001-0001-0001-000000000002', 'F0000001-0001-0001-0001-000000000002', 'E0000001-0001-0001-0001-000000000004',
    N'Lỗi: Kudo hiển thị sai người gửi khi tải nhiều tin cùng lúc', N'Fix lỗi map dữ liệu trong component KudoCard làm sai lệch hiển thị avatar.', 2, 1, DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, -1, @NowTasks), 'D0000001-0001-0001-0001-000000000009', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, -1, @NowTasks), DATEADD(DAY, -4, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 4, 3, 150000, 'SPRINT-15', @WsId);

-- Project 2 (INTHUB) - More tasks
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0002-0001-0001-000000000004')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0002-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000002', '30000001-0002-0001-0001-000000000001', 'F0000001-0002-0001-0001-000000000001', 'E0000001-0002-0001-0001-000000000003',
    N'Đồng bộ lịch Google Calendar với Dashboard cá nhân', N'API tự quét sự kiện trong ngày và ghim lên mục "My Calendar" ở trang For You.', 3, 5, DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, 4, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, 5, @NowTasks), DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 16, 8, 40000, 'INTHUB-4', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0002-0001-0001-000000000005')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0002-0001-0001-000000000005', 'C0000001-0001-0001-0001-000000000002', '30000001-0002-0001-0001-000000000001', 'F0000001-0002-0001-0001-000000000002', 'E0000001-0002-0001-0001-000000000003',
    N'Lỗi: Webhook GitHub bị timeout khi nhận payload dung lượng lớn', N'Tối ưu hóa parser JSON và chuyển xử lý payload sang Background Job bằng Queue để tránh block main thread.', 2, 3, DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, 1, @NowTasks), 'D0000001-0001-0001-0001-000000000009', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, 1, @NowTasks), DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 10, 6, 50000, 'INTHUB-5', @WsId);

-- Project 3 (MOBILE) - More tasks
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0003-0001-0001-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0003-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000003', '30000001-0003-0001-0001-000000000001', 'F0000001-0003-0001-0001-000000000001', 'E0000001-0003-0001-0001-000000000003',
    N'Xây dựng luồng Login và Lưu trữ JWT trong Secure Storage', N'Tích hợp flutter_secure_storage để lưu AccessToken/RefreshToken an toàn trên thiết bị iOS/Android.', 4, 3, DATEADD(DAY, -5, @NowTasks), DATEADD(DAY, 2, @NowTasks), 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000008', DATEADD(DAY, 4, @NowTasks), DATEADD(DAY, -6, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 12, 10, 20000, 'MOBILE-2', @WsId);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0003-0001-0001-000000000003')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0003-0001-0001-000000000003', 'C0000001-0001-0001-0001-000000000003', '30000001-0003-0001-0001-000000000001', 'F0000001-0003-0001-0001-000000000001', 'E0000001-0003-0001-0001-000000000003',
    N'Màn hình danh sách dự án và chi tiết Kanban board', N'Hiển thị danh sách project dạng card, khi nhấn vào hiển thị Kanban board dạng cột scroll ngang.', 3, 5, DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, 5, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, 6, @NowTasks), DATEADD(DAY, -4, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 20, 12, 30000, 'MOBILE-3', @WsId);

-- Project 4 (CSPORT) - More tasks
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0004-0001-0001-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0004-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000004', '30000001-0004-0001-0001-000000000001', 'F0000001-0004-0001-0001-000000000001', 'E0000001-0004-0001-0001-000000000003',
    N'Trang quản trị duyệt Ticket của nhân viên CS', N'PM/PO/CS Leader xem danh sách Intake đang Pending, chọn "Accept" để tạo Task, hoặc "Decline".', 3, 5, DATEADD(DAY, -3, @NowTasks), DATEADD(DAY, 6, @NowTasks), 'D0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000013', DATEADD(DAY, 8, @NowTasks), DATEADD(DAY, -4, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 16, 8, 20000, 'CSPORT-2', @WsId);

-- Project 5 (GROWTH) - More tasks
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0005-0001-0001-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0005-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000005', '30000001-0005-0001-0001-000000000001', 'F0000001-0005-0001-0001-000000000001', 'E0000001-0005-0001-0001-000000000003',
    N'Tối ưu hóa tốc độ tải trang Landing page (Core Web Vitals)', N'Nén ảnh, cấu hình lazyload component và chuyển sang render static HTML bằng SSR.', 4, 3, DATEADD(DAY, -1, @NowTasks), DATEADD(DAY, 6, @NowTasks), 'D0000001-0001-0001-0001-000000000014', 'D0000001-0001-0001-0001-000000000010', DATEADD(DAY, 8, @NowTasks), DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 12, 6, 20000, 'GROWTH-2', @WsId);

-- Project 6 (OPS) - More tasks
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '70000001-0006-0001-0001-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('70000001-0006-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000006', '30000001-0006-0001-0001-000000000001', 'F0000001-0006-0001-0001-000000000001', 'E0000001-0006-0001-0001-000000000003',
    N'Viết script backup tự động SQL Server hàng ngày', N'Schedule sqlcmd backup định kỳ 2h sáng lên lưu trữ Azure Blob Storage.', 3, 2, DATEADD(DAY, -1, @NowTasks), DATEADD(DAY, 4, @NowTasks), 'D0000001-0001-0001-0001-000000000015', 'D0000001-0001-0001-0001-000000000011', DATEADD(DAY, 5, @NowTasks), DATEADD(DAY, -2, @NowTasks), DATEADD(DAY, -1, @NowTasks), 0, 0, 8, 4, 20000, 'OPS-2', @WsId);
GO

PRINT N'✅ Part 3 complete: WorkTasks';
GO

-- ============================================================================
-- 15. TASK ASSIGNMENTS (Connecting users to tasks with estimation & actual hours)
-- ============================================================================
DECLARE @NowAssign DATETIME2 = GETUTCDATE();

-- SPRINT-1: Nguyễn Minh Khôi assigned to help Huỳnh Quốc Bảo
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000007')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000007', 1, 100, 1.0, 16, 18, DATEADD(DAY, -28, @NowAssign), DATEADD(DAY, -25, @NowAssign), N'Phụ trách chính thiết kế DB.', 4);

-- SPRINT-2: Đặng Bảo Ngọc
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000002' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000006', 1, 100, 1.0, 12, 10, DATEADD(DAY, -27, @NowAssign), DATEADD(DAY, -24, @NowAssign), N'Phát triển giao diện nền tảng.', 3);

-- SPRINT-3: Nguyễn Thanh Tâm
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000008')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000008', 1, 100, 1.0, 8, 6, DATEADD(DAY, -23, @NowAssign), DATEADD(DAY, -22, @NowAssign), N'Fix lỗi interceptor.', 1);

-- SPRINT-4: Huỳnh Quốc Bảo
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000007')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000004', 'D0000001-0001-0001-0001-000000000007', 1, 70, 1.0, 24, 16, DATEADD(DAY, -14, @NowAssign), DATEADD(DAY, -1, @NowAssign), N'Viết các endpoint Lexorank.', 4);

-- SPRINT-5: Đặng Bảo Ngọc
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000005', 'D0000001-0001-0001-0001-000000000006', 1, 80, 1.0, 20, 15, DATEADD(DAY, -13, @NowAssign), DATEADD(DAY, -2, @NowAssign), N'Lắp ráp thư viện draggable.', 3);

-- SPRINT-6: Nguyễn Thanh Tâm
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000006' AND UserId = 'D0000001-0001-0001-0001-000000000008')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000006', 'D0000001-0001-0001-0001-000000000008', 1, 95, 1.0, 16, 12, DATEADD(DAY, -10, @NowAssign), DATEADD(DAY, -3, @NowAssign), N'Tích hợp hub SignalR.', 3);

-- SPRINT-7: Đặng Bảo Ngọc
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000007' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000007', 'D0000001-0001-0001-0001-000000000006', 1, 90, 1.0, 4, 3, DATEADD(DAY, -5, @NowAssign), DATEADD(DAY, -2, @NowAssign), N'Fix lỗi CSS Firefox.', 2);

-- SPRINT-12: Huỳnh Quốc Bảo
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000012' AND UserId = 'D0000001-0001-0001-0001-000000000007')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000012', 'D0000001-0001-0001-0001-000000000007', 1, 80, 1.0, 10, 8, DATEADD(DAY, -5, @NowAssign), DATEADD(DAY, -1, @NowAssign), N'Đánh chỉ mục DB.', 4);

-- SPRINT-13: Nguyễn Thanh Tâm
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000013' AND UserId = 'D0000001-0001-0001-0001-000000000008')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000013', 'D0000001-0001-0001-0001-000000000008', 1, 100, 1.0, 6, 6, DATEADD(DAY, -4, @NowAssign), DATEADD(DAY, -2, @NowAssign), N'Sửa phân trang logs.', 2);

-- SPRINT-14: Đặng Bảo Ngọc
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000014' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000014', 'D0000001-0001-0001-0001-000000000006', 1, 85, 1.0, 16, 14, DATEADD(DAY, -8, @NowAssign), DATEADD(DAY, -4, @NowAssign), N'Tạo trang profile settings.', 3);

-- SPRINT-15: Đặng Bảo Ngọc
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '70000001-0001-0001-0001-000000000015' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('70000001-0001-0001-0001-000000000015', 'D0000001-0001-0001-0001-000000000006', 1, 100, 1.0, 4, 3, DATEADD(DAY, -3, @NowAssign), DATEADD(DAY, -1, @NowAssign), N'Sửa lỗi map sender kudo.', 2);
GO

-- ============================================================================
-- 16. COMMENTS (Social conversation updates on active tasks)
-- ============================================================================
DECLARE @NowComment DATETIME2 = GETUTCDATE();

-- Comments on SPRINT-4 (LexoRank API)
IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'a0000001-0001-0001-0001-000000000001')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted)
    VALUES ('a0000001-0001-0001-0001-000000000001', '70000001-0001-0001-0001-000000000004', 'WorkTask', 'D0000001-0001-0001-0001-000000000006', N'Tôi đã test thử thuật toán Lexorank của anh Bảo trên frontend, kéo thả rất mượt và không bị nhảy sortOrder trùng lặp nữa!', DATEADD(DAY, -5, @NowComment), DATEADD(DAY, -5, @NowComment), 0);

IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'a0000001-0001-0001-0001-000000000002')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted, ParentCommentId)
    VALUES ('a0000001-0001-0001-0001-000000000002', '70000001-0001-0001-0001-000000000004', 'WorkTask', 'D0000001-0001-0001-0001-000000000007', N'Cảm ơn Ngọc, anh đã tối ưu hóa thêm index để khi load bảng có hàng ngàn task vẫn phản hồi <50ms nhé.', DATEADD(DAY, -4, @NowComment), DATEADD(DAY, -4, @NowComment), 0, 'a0000001-0001-0001-0001-000000000001');

-- Comments on SPRINT-5 (Kanban Vue components)
IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'a0000001-0001-0001-0001-000000000003')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted)
    VALUES ('a0000001-0001-0001-0001-000000000003', '70000001-0001-0001-0001-000000000005', 'WorkTask', 'D0000001-0001-0001-0001-000000000010', N'Ngọc ơi, em đổi màu viền của các card khi drag sang màu xanh chủ đạo của hệ thống #3b82f6 để tạo cảm giác focus tốt hơn nhé.', DATEADD(DAY, -6, @NowComment), DATEADD(DAY, -6, @NowComment), 0);

IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'a0000001-0001-0001-0001-000000000004')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted, ParentCommentId)
    VALUES ('a0000001-0001-0001-0001-000000000004', '70000001-0001-0001-0001-000000000005', 'WorkTask', 'D0000001-0001-0001-0001-000000000006', N'Em đã cập nhật màu sắc viền và thêm hiệu ứng đổ bóng nhẹ rồi ạ. Trông rất xịn sò anh Huy nhé.', DATEADD(DAY, -5, @NowComment), DATEADD(DAY, -5, @NowComment), 0, 'a0000001-0001-0001-0001-000000000003');

-- Comments on SPRINT-3 (Token renewal bug)
IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'a0000001-0001-0001-0001-000000000005')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted)
    VALUES ('a0000001-0001-0001-0001-000000000005', '70000001-0001-0001-0001-000000000003', 'WorkTask', 'D0000001-0001-0001-0001-000000000009', N'Đã confirm lỗi này hết sạch sau bản vá mới. Authen chạy cực kỳ mượt mà, reload trang không bị văng nữa.', DATEADD(DAY, -21, @NowComment), DATEADD(DAY, -21, @NowComment), 0);
GO

PRINT N'✅ Part 4 complete: TaskAssignments, Comments';
GO

-- ============================================================================
-- 17. GOALS (OKR, corporate goals, team milestones)
-- ============================================================================
DECLARE @NowGoals DATETIME2 = GETUTCDATE();
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

-- Goal 1: Xây dựng nền tảng SaaS hoàn chỉnh
IF NOT EXISTS (SELECT 1 FROM Goals WHERE Id = '60000001-0001-0001-0001-000000000001')
    INSERT INTO Goals (Id, Title, Description, Status, DueDate, Progress, IsArchived, OwnerId, DepartmentId, WorkspaceId, CreatedAt, UpdatedAt)
    VALUES ('60000001-0001-0001-0001-000000000001', N'Xây dựng nền tảng SaaS quản lý công việc thế hệ mới',
    N'Hoàn thiện toàn bộ các tính năng Kanban, OKR, Wiki, Gamification cho SprintA và chạy thử nghiệm thành công.',
    'On Track', DATEADD(MONTH, 3, @NowGoals), 60, 0, 'D0000001-0001-0001-0001-000000000001', 'B0000001-0001-0001-0001-000000000001', @WsId, @NowGoals, @NowGoals);

-- Goal 2: Tăng cường tính năng AI trong Workspace (Sub goal of Goal 1)
IF NOT EXISTS (SELECT 1 FROM Goals WHERE Id = '60000001-0001-0001-0001-000000000002')
    INSERT INTO Goals (Id, Title, Description, Status, DueDate, Progress, IsArchived, OwnerId, DepartmentId, WorkspaceId, ParentGoalId, CreatedAt, UpdatedAt)
    VALUES ('60000001-0001-0001-0001-000000000002', N'Tích hợp Trợ lý Trí tuệ Nhân tạo (SprintA AI Assistant)',
    N'Cung cấp tính năng chat với chatbot, tự động tạo mô tả và gợi ý phân bổ task dựa trên lịch sử làm việc.',
    'On Track', DATEADD(MONTH, 2, @NowGoals), 40, 0, 'D0000001-0001-0001-0001-000000000002', 'B0000001-0001-0001-0001-000000000002', @WsId, '60000001-0001-0001-0001-000000000001', @NowGoals, @NowGoals);

-- Goal 3: Tối ưu hiệu năng Backend & Core API
IF NOT EXISTS (SELECT 1 FROM Goals WHERE Id = '60000001-0001-0001-0001-000000000003')
    INSERT INTO Goals (Id, Title, Description, Status, DueDate, Progress, IsArchived, OwnerId, DepartmentId, WorkspaceId, CreatedAt, UpdatedAt)
    VALUES ('60000001-0001-0001-0001-000000000003', N'Tối ưu hóa Response Time của API Backend đạt dưới 100ms',
    N'Tối ưu các câu lệnh EF Core, viết store procedure cho các tác vụ nặng, đánh chỉ mục DB và bật cache Redis.',
    'At Risk', DATEADD(MONTH, 1, @NowGoals), 75, 0, 'D0000001-0001-0001-0001-000000000003', 'B0000001-0001-0001-0001-000000000005', @WsId, @NowGoals, @NowGoals);

-- Goal 4: Hoàn thiện ứng dụng Mobile MVP (Sub goal of Goal 1)
IF NOT EXISTS (SELECT 1 FROM Goals WHERE Id = '60000001-0001-0001-0001-000000000004')
    INSERT INTO Goals (Id, Title, Description, Status, DueDate, Progress, IsArchived, OwnerId, DepartmentId, WorkspaceId, ParentGoalId, CreatedAt, UpdatedAt)
    VALUES ('60000001-0001-0001-0001-000000000004', N'Phát hành phiên bản thử nghiệm Mobile App MVP',
    N'Tạo app Flutter xem danh sách task, nhận thông báo đẩy realtime và cập nhật tiến độ công việc.',
    'On Track', DATEADD(MONTH, 4, @NowGoals), 30, 0, 'D0000001-0001-0001-0001-000000000008', 'B0000001-0001-0001-0001-000000000003', @WsId, '60000001-0001-0001-0001-000000000001', @NowGoals, @NowGoals);

-- Goal 5: Xây dựng hệ thống tài liệu và Onboarding khách hàng
IF NOT EXISTS (SELECT 1 FROM Goals WHERE Id = '60000001-0001-0001-0001-000000000005')
    INSERT INTO Goals (Id, Title, Description, Status, DueDate, Progress, IsArchived, OwnerId, DepartmentId, WorkspaceId, CreatedAt, UpdatedAt)
    VALUES ('60000001-0001-0001-0001-000000000005', N'Onboarding 50 doanh nghiệp SME chạy thử nghiệm',
    N'Tổ chức demo, viết tài liệu hướng dẫn sử dụng Wiki đầy đủ và hỗ trợ vận hành 24/7.',
    'On Track', DATEADD(MONTH, 2, @NowGoals), 50, 0, 'D0000001-0001-0001-0001-000000000013', 'B0000001-0001-0001-0001-000000000009', @WsId, @NowGoals, @NowGoals);
GO

-- ============================================================================
-- 18. GOAL UPDATES, RISKS, DECISIONS, LESSONS, TEAM GOALS
-- ============================================================================
DECLARE @NowGoalMeta DATETIME2 = GETUTCDATE();

-- Goal Updates for Goal 3 (Backend Performance)
IF NOT EXISTS (SELECT 1 FROM GoalUpdates WHERE Id = '65000001-0001-0001-0001-000000000001')
    INSERT INTO GoalUpdates (Id, GoalId, Content, Status, OldStatus, NewStatus, OldProgress, NewProgress, UserId, CreatedAt)
    VALUES ('65000001-0001-0001-0001-000000000001', '60000001-0001-0001-0001-000000000003', N'Đã tối ưu hóa các câu lệnh LINQ sang SQL tốt hơn. Tiến trình cải thiện rõ rệt.', 'On Track', 'On Track', 'On Track', 50, 75, 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, -5, @NowGoalMeta));

IF NOT EXISTS (SELECT 1 FROM GoalUpdates WHERE Id = '65000001-0001-0001-0001-000000000002')
    INSERT INTO GoalUpdates (Id, GoalId, Content, Status, OldStatus, NewStatus, OldProgress, NewProgress, UserId, CreatedAt)
    VALUES ('65000001-0001-0001-0001-000000000002', '60000001-0001-0001-0001-000000000003', N'Gặp lỗi rò rỉ bộ nhớ (Memory Leak) khi chạy SignalR Hub với số lượng kết nối cao. Chuyển trạng thái sang At Risk để xử lý gấp.', 'At Risk', 'On Track', 'At Risk', 75, 75, 'D0000001-0001-0001-0001-000000000003', DATEADD(DAY, -1, @NowGoalMeta));

-- Goal Risks
IF NOT EXISTS (SELECT 1 FROM GoalRisks WHERE Id = '62000001-0001-0001-0001-000000000001')
    INSERT INTO GoalRisks (Id, GoalId, Text, Severity, CreatorId, CreatedAt)
    VALUES ('62000001-0001-0001-0001-000000000001', '60000001-0001-0001-0001-000000000003', N'SignalR Connection Leak có thể làm sập server API nếu có >500 users kết nối đồng thời.', 'High', 'D0000001-0001-0001-0001-000000000007', @NowGoalMeta);

-- Goal Decisions
IF NOT EXISTS (SELECT 1 FROM GoalDecisions WHERE Id = '6D000001-0001-0001-0001-000000000001')
    INSERT INTO GoalDecisions (Id, GoalId, Text, DecisionDate, CreatorId, CreatedAt)
    VALUES ('6D000001-0001-0001-0001-000000000001', '60000001-0001-0001-0001-000000000003', N'Quyết định tích hợp Redis Backplane cho SignalR để scaleout ngang.', DATEADD(DAY, -1, @NowGoalMeta), 'D0000001-0001-0001-0001-000000000003', @NowGoalMeta);

-- Goal Lessons
IF NOT EXISTS (SELECT 1 FROM GoalLessons WHERE Id = '6b000001-0001-0001-0001-000000000001')
    INSERT INTO GoalLessons (Id, GoalId, Text, CreatorId, CreatedAt)
    VALUES ('6b000001-0001-0001-0001-000000000001', '60000001-0001-0001-0001-000000000003', N'Luôn phải dispose Connection trong các event Hub để tránh nghẽn luồng.', 'D0000001-0001-0001-0001-000000000007', @NowGoalMeta);

-- Team Goals (Connecting Goals to Departments)
IF NOT EXISTS (SELECT 1 FROM TeamGoals WHERE Id = '46000001-0001-0001-0001-000000000001')
    INSERT INTO TeamGoals (Id, DepartmentId, GoalId, CreatedByUserId, CreatedAt)
    VALUES ('46000001-0001-0001-0001-000000000001', 'B0000001-0001-0001-0001-000000000003', '60000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000003', @NowGoalMeta);

IF NOT EXISTS (SELECT 1 FROM TeamGoals WHERE Id = '46000001-0001-0001-0001-000000000002')
    INSERT INTO TeamGoals (Id, DepartmentId, GoalId, CreatedByUserId, CreatedAt)
    VALUES ('46000001-0001-0001-0001-000000000002', 'B0000001-0001-0001-0001-000000000002', '60000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000002', @NowGoalMeta);
GO

-- ============================================================================
-- 19. PAGES (Wiki documentation pages inside projects)
-- ============================================================================
DECLARE @NowPages DATETIME2 = GETUTCDATE();

-- Project 1 (SPRINT) Wiki Pages
IF NOT EXISTS (SELECT 1 FROM Pages WHERE Id = 'f0000001-0001-0001-0001-000000000001')
    INSERT INTO Pages (Id, Title, Content, ProjectId, CreatedById, SortOrder, IsLocked, IsArchived, IsPrivate, IsStarred, CreatedAt, UpdatedAt)
    VALUES ('f0000001-0001-0001-0001-000000000001', N'Tài liệu phát triển: Cấu trúc cơ sở dữ liệu',
    N'{"type":"doc","content":[{"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"Tổng quan Database Schema"}]},{"type":"paragraph","content":[{"type":"text","text":"Hệ thống sử dụng SQL Server làm CSDL quan hệ chính. Các mối quan hệ nhiều-nhiều được liên kết thông qua các bảng nối như WorkspaceMember, ProjectMember, TaskAssignment để lưu trữ thêm dữ liệu phụ."}]}]}',
    'C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000003', 0, 0, 0, 0, 1, DATEADD(DAY, -15, @NowPages), @NowPages);

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Id = 'f0000001-0001-0001-0001-000000000002')
    INSERT INTO Pages (Id, Title, Content, ProjectId, CreatedById, SortOrder, IsLocked, IsArchived, IsPrivate, IsStarred, CreatedAt, UpdatedAt)
    VALUES ('f0000001-0001-0001-0001-000000000002', N'Hướng dẫn Onboarding cho Lập trình viên mới',
    N'{"type":"doc","content":[{"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"Quy trình Onboarding"}]},{"type":"paragraph","content":[{"type":"text","text":"1. Clone source code từ repo Git.\\n2. Mở file run.bat để khởi chạy nhanh cả backend và frontend.\\n3. Sử dụng tài khoản dev@sprinta.local để bắt đầu làm việc."}]}]}',
    'C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000002', 1, 0, 0, 0, 0, DATEADD(DAY, -10, @NowPages), @NowPages);

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Id = 'f0000001-0001-0001-0001-000000000003')
    INSERT INTO Pages (Id, Title, Content, ProjectId, CreatedById, SortOrder, IsLocked, IsArchived, IsPrivate, IsStarred, CreatedAt, UpdatedAt)
    VALUES ('f0000001-0001-0001-0001-000000000003', N'Agile & Scrum Guidelines nội bộ',
    N'{"type":"doc","content":[{"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"Agile Scrum Rules"}]},{"type":"paragraph","content":[{"type":"text","text":"Mỗi sprint kéo dài 2 tuần. Daily meeting diễn ra lúc 9:30 AM hàng ngày. Demo và Sprint Retrospective tổ chức vào chiều thứ Sáu cuối sprint."}]}]}',
    'C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000004', 2, 1, 0, 0, 0, DATEADD(DAY, -5, @NowPages), @NowPages);
GO

PRINT N'✅ Part 5 & 6 complete: Goals, OKRs, Pages';
GO

-- ============================================================================
-- 20. PROJECT UPDATES, RISKS, DECISIONS, LESSONS, LINKS
-- ============================================================================
DECLARE @NowProjMeta DATETIME2 = GETUTCDATE();

-- Project Updates for Project 1 (SPRINT)
IF NOT EXISTS (SELECT 1 FROM ProjectUpdates WHERE Id = 'f5000001-0001-0001-0001-000000000001')
    INSERT INTO ProjectUpdates (Id, ProjectId, Content, Status, OldStatus, NewStatus, CreatorId, CreatedAt)
    VALUES ('f5000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', N'Đã hoàn thành thiết lập cấu trúc database và boilerplate backend. Team Frontend đã lên khung CSS.', 'On Track', 'On Track', 'On Track', 'D0000001-0001-0001-0001-000000000003', DATEADD(DAY, -20, @NowProjMeta));

IF NOT EXISTS (SELECT 1 FROM ProjectUpdates WHERE Id = 'f5000001-0001-0001-0001-000000000002')
    INSERT INTO ProjectUpdates (Id, ProjectId, Content, Status, OldStatus, NewStatus, CreatorId, CreatedAt)
    VALUES ('f5000001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', N'Bắt đầu triển khai Kanban Board và tích hợp SignalR realtime. Có phát sinh một số lỗi drag-drop trên Firefox.', 'On Track', 'On Track', 'On Track', 'D0000001-0001-0001-0001-000000000003', DATEADD(DAY, -5, @NowProjMeta));

-- Project Risks
IF NOT EXISTS (SELECT 1 FROM ProjectRisks WHERE Id = 'f2000001-0001-0001-0001-000000000001')
    INSERT INTO ProjectRisks (Id, ProjectId, Text, Severity, CreatorId, CreatedAt)
    VALUES ('f2000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', N'Kéo thả Kanban LexoRank có thể bị lỗi xung đột vị trí (rank collision) khi 2 user drag cùng một thời điểm.', 'Medium', 'D0000001-0001-0001-0001-000000000006', @NowProjMeta);

-- Project Decisions
IF NOT EXISTS (SELECT 1 FROM ProjectDecisions WHERE Id = 'fD000001-0001-0001-0001-000000000001')
    INSERT INTO ProjectDecisions (Id, ProjectId, Text, CreatorId, CreatedAt)
    VALUES ('fD000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', N'Quyết định sử dụng thư viện LexoRank dạng chuỗi ký tự thay vì double float để tránh giới hạn chữ số thập phân.', 'D0000001-0001-0001-0001-000000000007', @NowProjMeta);

-- Project Lessons
IF NOT EXISTS (SELECT 1 FROM ProjectLessons WHERE Id = 'fb000001-0001-0001-0001-000000000001')
    INSERT INTO ProjectLessons (Id, ProjectId, Text, CreatorId, CreatedAt)
    VALUES ('fb000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', N'Cần viết tích hợp Unit Test cho thuật toán LexoRank ngay từ đầu để tránh lỗi phân chia cho 0.', 'D0000001-0001-0001-0001-000000000007', @NowProjMeta);

-- Project Links (Linking SPRINT project to Strategic Goal 1)
IF NOT EXISTS (SELECT 1 FROM ProjectLinks WHERE Id = 'fba00001-0001-0001-0001-000000000001')
    INSERT INTO ProjectLinks (Id, ProjectId, LinkedType, LinkedId, CreatorId, CreatedAt)
    VALUES ('fba00001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', 'Goal', '60000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', @NowProjMeta);
GO

-- ============================================================================
-- 21. NOTIFICATIONS (Simulating unread & read alerts for CEO/Managers)
-- ============================================================================
DECLARE @NowNotif DATETIME2 = GETUTCDATE();

-- Notifications for CEO (khoi.nguyen)
IF NOT EXISTS (SELECT 1 FROM Notifications WHERE Id = 'd0000001-0001-0001-0001-000000000001')
    INSERT INTO Notifications (Id, UserId, Title, Content, LinkUrl, IsRead, CreatedAt, NotificationType, RelatedTaskId, RelatedProjectId, TriggeredByUserId)
    VALUES ('d0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', N'Bạn đã được gán vào một công việc mới', N'Lê Hoàng Phúc đã gán bạn vào task "Hệ thống Gamification tích lũy điểm thưởng khi đóng góp code".', '/projects/C0000001-0001-0001-0001-000000000001/tasks/70000001-0001-0001-0001-000000000010', 0, DATEADD(MINUTE, -30, @NowNotif), 'TASK_ASSIGNED', '70000001-0001-0001-0001-000000000010', 'C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000003');

IF NOT EXISTS (SELECT 1 FROM Notifications WHERE Id = 'd0000001-0001-0001-0001-000000000002')
    INSERT INTO Notifications (Id, UserId, Title, Content, LinkUrl, IsRead, CreatedAt, NotificationType, RelatedTaskId, RelatedProjectId, TriggeredByUserId)
    VALUES ('d0000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000001', N'Bình luận mới trong SPRINT-4', N'Huỳnh Quốc Bảo đã phản hồi: "Cảm ơn Ngọc, anh đã tối ưu hóa thêm index..."', '/projects/C0000001-0001-0001-0001-000000000001/tasks/70000001-0001-0001-0001-000000000004', 0, DATEADD(HOUR, -2, @NowNotif), 'COMMENT_ADDED', '70000001-0001-0001-0001-000000000004', 'C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000007');

-- Notifications for Project Manager (phuc.le)
IF NOT EXISTS (SELECT 1 FROM Notifications WHERE Id = 'd0000001-0001-0001-0001-000000000003')
    INSERT INTO Notifications (Id, UserId, Title, Content, LinkUrl, IsRead, CreatedAt, NotificationType, RelatedTaskId, RelatedProjectId, TriggeredByUserId)
    VALUES ('d0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000003', N'Task chuyển sang trạng thái IN REVIEW', N'Đặng Bảo Ngọc đã chuyển task SPRINT-7 sang trạng thái IN REVIEW.', '/projects/C0000001-0001-0001-0001-000000000001/tasks/70000001-0001-0001-0001-000000000007', 0, DATEADD(HOUR, -5, @NowNotif), 'TASK_STATUS_CHANGED', '70000001-0001-0001-0001-000000000007', 'C0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000006');
GO

-- ============================================================================
-- 22. STARRED ITEMS, RECENT VIEWS, FOLLOWERS
-- ============================================================================
DECLARE @NowRecent DATETIME2 = GETUTCDATE();
DECLARE @WsId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';

-- Starred Items for CEO (khoi.nguyen)
IF NOT EXISTS (SELECT 1 FROM StarredItems WHERE Id = '34000001-0001-0001-0001-000000000001')
    INSERT INTO StarredItems (Id, UserId, WorkspaceId, ItemType, ItemId, CreatedAt)
    VALUES ('34000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', @WsId, 'Project', 'C0000001-0001-0001-0001-000000000001', @NowRecent);

IF NOT EXISTS (SELECT 1 FROM StarredItems WHERE Id = '34000001-0001-0001-0001-000000000002')
    INSERT INTO StarredItems (Id, UserId, WorkspaceId, ItemType, ItemId, CreatedAt)
    VALUES ('34000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000001', @WsId, 'Goal', '60000001-0001-0001-0001-000000000001', @NowRecent);

-- Recent Views for CEO (khoi.nguyen)
IF NOT EXISTS (SELECT 1 FROM RecentViews WHERE Id = '26000001-0001-0001-0001-000000000001')
    INSERT INTO RecentViews (Id, UserId, EntityType, EntityId, Title, Subtitle, Url, Icon, ViewedAt)
    VALUES ('26000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', 'Project', 'C0000001-0001-0001-0001-000000000001', N'SprintA Enterprise Platform', N'SPRINT', '/projects/C0000001-0001-0001-0001-000000000001', 'folder', @NowRecent);

IF NOT EXISTS (SELECT 1 FROM RecentViews WHERE Id = '26000001-0001-0001-0001-000000000002')
    INSERT INTO RecentViews (Id, UserId, EntityType, EntityId, Title, Subtitle, Url, Icon, ViewedAt)
    VALUES ('26000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000001', 'Goal', '60000001-0001-0001-0001-000000000001', N'Xây dựng nền tảng SaaS quản lý công việc thế hệ mới', N'OKR Company', '/goals/60000001-0001-0001-0001-000000000001', 'target', @NowRecent);

-- Entity Followers (Subscribers)
IF NOT EXISTS (SELECT 1 FROM EntityFollowers WHERE Id = 'EF000001-0001-0001-0001-000000000001')
    INSERT INTO EntityFollowers (Id, EntityId, EntityType, UserId, CreatedAt)
    VALUES ('EF000001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', 'Project', 'D0000001-0001-0001-0001-000000000001', @NowRecent);

IF NOT EXISTS (SELECT 1 FROM EntityFollowers WHERE Id = 'EF000001-0001-0001-0001-000000000002')
    INSERT INTO EntityFollowers (Id, EntityId, EntityType, UserId, CreatedAt)
    VALUES ('EF000001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', 'Project', 'D0000001-0001-0001-0001-000000000003', @NowRecent);
GO

PRINT N'✅ Part 7 & 8 complete: Project Updates, Notifications, History';
GO

-- ============================================================================
-- 23. GAMIFICATION (Kudos, reactions, points wallet and transactions)
-- ============================================================================
DECLARE @NowGami DATETIME2 = GETUTCDATE();

-- Kudos
IF NOT EXISTS (SELECT 1 FROM Kudos WHERE Id = 'aD000001-0001-0001-0001-000000000001')
    INSERT INTO Kudos (Id, SenderId, ReceiverId, Message, Icon, CreatedAt)
    VALUES ('aD000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000006', N'Tuyệt vời! Cảm ơn Ngọc đã tích hợp kéo thả Kanban board cực kỳ nhanh và mượt mà.', '🚀', DATEADD(DAY, -2, @NowGami));

IF NOT EXISTS (SELECT 1 FROM Kudos WHERE Id = 'aD000001-0001-0001-0001-000000000002')
    INSERT INTO Kudos (Id, SenderId, ReceiverId, Message, Icon, CreatedAt)
    VALUES ('aD000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000007', N'Cảm ơn Bảo đã tối ưu hóa database giúp hệ thống chạy nhanh hơn gấp 3 lần.', '🙌', DATEADD(DAY, -1, @NowGami));

-- Kudo Reactions
IF NOT EXISTS (SELECT 1 FROM KudoReactions WHERE Id = 'a2000001-0001-0001-0001-000000000001')
    INSERT INTO KudoReactions (Id, KudoId, UserId, ReactionType, CreatedAt)
    VALUES ('a2000001-0001-0001-0001-000000000001', 'aD000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', 'Clap', @NowGami);

IF NOT EXISTS (SELECT 1 FROM KudoReactions WHERE Id = 'a2000001-0001-0001-0001-000000000002')
    INSERT INTO KudoReactions (Id, KudoId, UserId, ReactionType, CreatedAt)
    VALUES ('a2000001-0001-0001-0001-000000000002', 'aD000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000002', 'Heart', @NowGami);

-- Wallets for users (Level and Points)
DECLARE @j INT = 1;
WHILE @j <= 15
BEGIN
    DECLARE @uid UNIQUEIDENTIFIER = CAST('D0000001-0001-0001-0001-0000000000' + RIGHT('00' + CAST(@j AS VARCHAR), 2) AS UNIQUEIDENTIFIER);
    DECLARE @Points INT = CASE WHEN @j = 6 THEN 1250 WHEN @j = 7 THEN 1500 WHEN @j = 1 THEN 800 ELSE 300 END;
    DECLARE @Lvl INT = CASE WHEN @j = 6 THEN 3 WHEN @j = 7 THEN 4 WHEN @j = 1 THEN 2 ELSE 1 END;
    
    IF NOT EXISTS (SELECT 1 FROM UserWallets WHERE UserId = @uid)
        INSERT INTO UserWallets (UserId, TotalPoints, Level) VALUES (@uid, @Points, @Lvl);
    SET @j = @j + 1;
END

-- Point Transactions
IF NOT EXISTS (SELECT 1 FROM PointTransactions WHERE Id = 'f4000001-0001-0001-0001-000000000001')
    INSERT INTO PointTransactions (Id, UserWalletUserId, WorkTaskId, Amount, Reason, TransactionType, CreatedAt)
    VALUES ('f4000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000006', '70000001-0001-0001-0001-000000000005', 200, N'Hoàn thành Task đúng tiến độ & chất lượng cao.', 'Reward', DATEADD(DAY, -2, @NowGami));

IF NOT EXISTS (SELECT 1 FROM PointTransactions WHERE Id = 'f4000001-0001-0001-0001-000000000002')
    INSERT INTO PointTransactions (Id, UserWalletUserId, WorkTaskId, Amount, Reason, TransactionType, CreatedAt)
    VALUES ('f4000001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000007', '70000001-0001-0001-0001-000000000012', 300, N'Tối ưu hóa DB Index giúp hệ thống chạy nhanh hơn.', 'Reward', DATEADD(DAY, -1, @NowGami));

-- Performance Reviews
IF NOT EXISTS (SELECT 1 FROM PerformanceReviews WHERE Id = 'f2600001-0001-0001-0001-000000000001')
    INSERT INTO PerformanceReviews (Id, ReviewerId, RevieweeId, Score, Feedback, ReviewPeriod, CreatedAt)
    VALUES ('f2600001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000003', 'D0000001-0001-0001-0001-000000000006', 9.5, N'Ngọc làm việc xuất sắc, giao diện Vue 3 hoàn thiện nhanh, UI mượt và không phát sinh bug nghiêm trọng.', N'Q2/2026', DATEADD(DAY, -1, @NowGami));
GO

-- ============================================================================
-- 24. UNIFIED INBOX DEMO (NO FAKE OAUTH CONNECTIONS)
-- ============================================================================
DECLARE @NowInteg DATETIME2 = GETUTCDATE();

-- Integration Accounts for CEO (khoi.nguyen)
UPDATE InboxItems
SET IntegrationAccountId = NULL,
    Provider = 'Demo',
    Source = 'Demo Calendar',
    UpdatedAt = @NowInteg
WHERE Id = '88000001-0001-0001-0001-000000000001';

DELETE FROM SyncHistories WHERE Id = '37000001-0001-0001-0001-000000000001';
DELETE FROM IntegrationAccounts
WHERE Id IN ('8A000001-0001-0001-0001-000000000001', '8A000001-0001-0001-0001-000000000002');

-- Legacy mock sync data intentionally disabled: demo inbox is not an OAuth connection.
IF 1 = 0
    INSERT INTO SyncHistories (Id, UserId, IntegrationAccountId, Provider, Status, ItemsImported, Message, StartedAt, CompletedAt)
    VALUES ('37000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', '8A000001-0001-0001-0001-000000000001', 'Google', 'Success', 5, N'Đồng bộ Google Calendar thành công.', DATEADD(MINUTE, -5, @NowInteg), @NowInteg);

-- Inbox Items (Unified Inbox)
IF 1 = 0
    INSERT INTO InboxItems (Id, UserId, IntegrationAccountId, Source, Provider, ExternalId, Title, Content, StartsAt, EndsAt, IsRead, CreatedAt, UpdatedAt)
    VALUES ('88000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', '8A000001-0001-0001-0001-000000000001', 'Google Calendar', 'Google', 'evt_999888777', N'Họp chiến lược phát triển sản phẩm Q3', N'Thảo luận về lộ trình nâng cấp SprintA và kế hoạch marketing.', DATEADD(HOUR, 2, @NowInteg), DATEADD(HOUR, 3, @NowInteg), 0, @NowInteg, @NowInteg);
GO

-- ============================================================================
-- 25. AI INTEGRATION (AI Prompt Templates, tokens & feedbacks)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM InboxItems WHERE Id = '88000001-0001-0001-0001-000000000001')
    INSERT INTO InboxItems (Id, UserId, IntegrationAccountId, Source, Provider, ExternalId, Title, Content, StartsAt, EndsAt, IsRead, CreatedAt, UpdatedAt)
    VALUES ('88000001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', NULL, 'Demo Calendar', 'Demo', 'demo_calendar_q3_strategy', N'Q3 product strategy review', N'Demo Unified Inbox item. No OAuth account is connected.', DATEADD(HOUR, 2, GETUTCDATE()), DATEADD(HOUR, 3, GETUTCDATE()), 0, GETUTCDATE(), GETUTCDATE());

DECLARE @NowAI DATETIME2 = GETUTCDATE();

-- AI Prompt Templates
IF NOT EXISTS (SELECT 1 FROM AIPromptTemplates WHERE Id = 'A8f00001-0001-0001-0001-000000000001')
    INSERT INTO AIPromptTemplates (Id, Code, TemplateContent, IsActive)
    VALUES ('A8f00001-0001-0001-0001-000000000001', 'TASK_DESC_GEN', N'Viết mô tả công việc chi tiết cho task có tiêu đề: "{title}". Hãy viết dạng Markdown chuyên nghiệp gồm: Mục tiêu, Các bước thực hiện, Kết quả mong đợi.', 1);

IF NOT EXISTS (SELECT 1 FROM AIPromptTemplates WHERE Id = 'A8f00001-0001-0001-0001-000000000002')
    INSERT INTO AIPromptTemplates (Id, Code, TemplateContent, IsActive)
    VALUES ('A8f00001-0001-0001-0001-000000000002', 'BUG_ANALYSIS', N'Phân tích log lỗi sau đây và đưa ra nguyên nhân, cách khắc phục trong dự án .NET Core + Entity Framework: "{log}"', 1);

-- AI Token Usage
IF NOT EXISTS (SELECT 1 FROM AITokenUsages WHERE Id = 'A4500001-0001-0001-0001-000000000001')
    INSERT INTO AITokenUsages (Id, UserId, FeatureCode, TokensUsed, CreatedAt)
    VALUES ('A4500001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000002', 'TASK_DESC_GEN', 1540, @NowAI);

-- AI Feedback
IF NOT EXISTS (SELECT 1 FROM AIFeedbacks WHERE Id = 'AFB00001-0001-0001-0001-000000000001')
    INSERT INTO AIFeedbacks (Id, UserId, PromptContent, AIResponse, Rating, CreatedAt)
    VALUES ('AFB00001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000002', N'Viết mô tả cho task: Phát triển API Lexorank', N'**Mục tiêu**: Xây dựng thuật toán xếp hạng LexoRank... **Các bước**: 1. Khảo sát... **Kết quả**: API phản hồi...', 5, @NowAI);
GO

-- ============================================================================
-- 26. AUDIT LOGS (Site and System Actions)
-- ============================================================================
DECLARE @NowAudit DATETIME2 = GETUTCDATE();

-- Site Audit Logs
IF NOT EXISTS (SELECT 1 FROM SiteAuditLogs WHERE Id = '3Ab00001-0001-0001-0001-000000000001')
    INSERT INTO SiteAuditLogs (Id, EntityId, EntityType, Action, OldValue, NewValue, UserId, CreatedAt)
    VALUES ('3Ab00001-0001-0001-0001-000000000001', 'C0000001-0001-0001-0001-000000000001', 'Project', 'Create', '{}', '{"Name":"SprintA Enterprise Platform","Identifier":"SPRINT"}', 'D0000001-0001-0001-0001-000000000002', DATEADD(DAY, -30, @NowAudit));

IF NOT EXISTS (SELECT 1 FROM SiteAuditLogs WHERE Id = '3Ab00001-0001-0001-0001-000000000002')
    INSERT INTO SiteAuditLogs (Id, EntityId, EntityType, Action, OldValue, NewValue, UserId, CreatedAt)
    VALUES ('3Ab00001-0001-0001-0001-000000000002', 'C0000001-0001-0001-0001-000000000001', 'Project', 'AddMember', '{}', '{"UserId":"D0000001-0001-0001-0001-000000000006","Role":"DEV"}', 'D0000001-0001-0001-0001-000000000003', DATEADD(DAY, -29, @NowAudit));

-- System Audit Logs
IF NOT EXISTS (SELECT 1 FROM SystemAuditLogs WHERE Id = '39b00001-0001-0001-0001-000000000001')
    INSERT INTO SystemAuditLogs (Id, UserId, Action, Resource, Status, IPAddress, Details, CreatedAt)
    VALUES ('39b00001-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000001', 'UserLogin', 'Authentication', 'Success', '192.168.1.100', N'Đăng nhập thành công qua trình duyệt Chrome.', DATEADD(MINUTE, -10, @NowAudit));

IF NOT EXISTS (SELECT 1 FROM SystemAuditLogs WHERE Id = '39b00001-0001-0001-0001-000000000002')
    INSERT INTO SystemAuditLogs (Id, UserId, Action, Resource, Status, IPAddress, Details, CreatedAt)
    VALUES ('39b00001-0001-0001-0001-000000000002', 'D0000001-0001-0001-0001-000000000002', 'ExportData', 'ProjectManagement', 'Success', '192.168.1.105', N'Xuất dữ liệu Excel danh sách task dự án SPRINT.', DATEADD(HOUR, -1, @NowAudit));
GO

-- ============================================================================
-- 27. BOOKING CENTER SAMPLE PROJECT (Driving center booking system)
-- ============================================================================
DECLARE @NowBook DATETIME2 = GETUTCDATE();
DECLARE @BookWorkspaceId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';
DECLARE @BookAdminId UNIQUEIDENTIFIER = ISNULL((SELECT TOP 1 Id FROM Users WHERE Email = 'dev@sprinta.local'), 'D0000001-0001-0001-0001-000000000001');
DECLARE @BookTeacherId UNIQUEIDENTIFIER = 'bbbbbbbb-0000-0000-0000-000000000002';
DECLARE @BookStudentId UNIQUEIDENTIFIER = 'bbbbbbbb-0000-0000-0000-000000000003';
DECLARE @BookProjectId UNIQUEIDENTIFIER = 'bbbbbbbb-0000-0000-0000-000000000011';
DECLARE @BookPwdHash NVARCHAR(200) = '$2a$11$K5F.nGm4rVxfWuAX3CiOCOx5dCcnNEzSX0xGVJTB4xfQHKJBfZGEi';

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @BookTeacherId)
    INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
    VALUES (@BookTeacherId, 'giaovien.booking@example.com', @BookPwdHash, N'Lê Minh Quân',
        N'Giáo viên hướng dẫn lái xe hạng B2, phụ trách lịch thực hành và đánh giá buổi học.',
        N'Giáo viên hướng dẫn', N'TP. Hồ Chí Minh', 'Asia/Ho_Chi_Minh', 1, @NowBook, @NowBook, 0, 0, 'org_novatech_001');

IF NOT EXISTS (SELECT 1 FROM Users WHERE Id = @BookStudentId)
    INSERT INTO Users (Id, Email, PasswordHash, FullName, Bio, JobTitle, [Location], Timezone, IsActive, CreatedAt, UpdatedAt, IsDeleted, Is2FAEnabled, OrganizationId)
    VALUES (@BookStudentId, 'hocvien.booking@example.com', @BookPwdHash, N'Nguyễn Thảo My',
        N'Học viên đăng ký lịch học thực hành, theo dõi tiến độ học phí và lịch thi sát hạch.',
        N'Học viên lái xe', N'Bình Dương', 'Asia/Ho_Chi_Minh', 1, @NowBook, @NowBook, 0, 0, 'org_novatech_001');

IF NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @BookWorkspaceId AND UserId = @BookTeacherId)
    INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive)
    VALUES (@BookWorkspaceId, @BookTeacherId, 'MEMBER', @NowBook, 1);

IF NOT EXISTS (SELECT 1 FROM WorkspaceMembers WHERE WorkspaceId = @BookWorkspaceId AND UserId = @BookStudentId)
    INSERT INTO WorkspaceMembers (WorkspaceId, UserId, WorkspaceRole, JoinedAt, IsActive)
    VALUES (@BookWorkspaceId, @BookStudentId, 'GUEST', @NowBook, 1);

IF NOT EXISTS (SELECT 1 FROM Projects WHERE Id = @BookProjectId)
    INSERT INTO Projects (Id, Name, [Description], Identifier, IssueSequence, StartDate, EndDate, Status, CreatorId, WorkspaceId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, NetworkType, DepartmentId, Why, SuccessCriteria)
    VALUES (@BookProjectId, N'Hệ thống đặt lịch book xe trung tâm dạy lái',
        N'Dự án mẫu quản lý tài khoản, xe tập lái, dịch vụ, đặt lịch, thanh toán, hóa đơn và báo cáo vận hành trung tâm.',
        'BOOKXE', 30, DATEADD(DAY, -20, @NowBook), DATEADD(DAY, 70, @NowBook), 1,
        @BookAdminId, @BookWorkspaceId, @NowBook, @NowBook, 0, 0, 'Public',
        'B0000001-0001-0001-0001-000000000003',
        N'Chuẩn hóa quy trình đặt xe thực hành để giảm trùng lịch, giảm gọi điện thủ công và giúp học viên theo dõi lịch học rõ ràng.',
        N'Học viên tự đặt lịch được trong 3 bước, giáo viên xác nhận lịch dưới 5 phút, báo cáo xe và doanh thu cập nhật hằng ngày.');

UPDATE Projects
SET IssueSequence = CASE WHEN IssueSequence < 30 THEN 30 ELSE IssueSequence END,
    Name = N'Hệ thống đặt lịch book xe trung tâm dạy lái',
    [Description] = N'Dự án mẫu quản lý tài khoản, xe tập lái, dịch vụ, đặt lịch, thanh toán, hóa đơn và báo cáo vận hành trung tâm.',
    UpdatedAt = @NowBook
WHERE Id = @BookProjectId;

IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = @BookAdminId)
    INSERT INTO ProjectMembers VALUES (@BookProjectId, @BookAdminId, 'PM', @NowBook, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = @BookTeacherId)
    INSERT INTO ProjectMembers VALUES (@BookProjectId, @BookTeacherId, N'Giáo viên hướng dẫn', @NowBook, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = @BookStudentId)
    INSERT INTO ProjectMembers VALUES (@BookProjectId, @BookStudentId, N'Học viên kiểm thử', @NowBook, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = 'D0000001-0001-0001-0001-000000000005')
    INSERT INTO ProjectMembers VALUES (@BookProjectId, 'D0000001-0001-0001-0001-000000000005', 'BA', @NowBook, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO ProjectMembers VALUES (@BookProjectId, 'D0000001-0001-0001-0001-000000000006', 'DEV', @NowBook, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = 'D0000001-0001-0001-0001-000000000007')
    INSERT INTO ProjectMembers VALUES (@BookProjectId, 'D0000001-0001-0001-0001-000000000007', 'DEV', @NowBook, NULL, 1);
IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @BookProjectId AND UserId = 'D0000001-0001-0001-0001-000000000009')
    INSERT INTO ProjectMembers VALUES (@BookProjectId, 'D0000001-0001-0001-0001-000000000009', 'TESTER', @NowBook, NULL, 1);

IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000020')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('bbbbbbbb-0000-0000-0000-000000000020', @BookProjectId, 'BACKLOG', '#64748b', 0);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000021')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('bbbbbbbb-0000-0000-0000-000000000021', @BookProjectId, 'TO DO', '#3b82f6', 1);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000022')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('bbbbbbbb-0000-0000-0000-000000000022', @BookProjectId, 'IN PROGRESS', '#f59e0b', 2);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000023')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('bbbbbbbb-0000-0000-0000-000000000023', @BookProjectId, 'IN REVIEW', '#8b5cf6', 3);
IF NOT EXISTS (SELECT 1 FROM TaskStatuses WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000024')
    INSERT INTO TaskStatuses (Id, ProjectId, Name, ColorCode, Position) VALUES ('bbbbbbbb-0000-0000-0000-000000000024', @BookProjectId, 'DONE', '#10b981', 4);

IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000030')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('bbbbbbbb-0000-0000-0000-000000000030', @BookProjectId, 'Feature', '#3b82f6', 'check-square');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000031')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('bbbbbbbb-0000-0000-0000-000000000031', @BookProjectId, 'API', '#8b5cf6', 'plug');
IF NOT EXISTS (SELECT 1 FROM TaskTypes WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000032')
    INSERT INTO TaskTypes (Id, ProjectId, Name, ColorCode, Icon) VALUES ('bbbbbbbb-0000-0000-0000-000000000032', @BookProjectId, 'UI', '#10b981', 'palette');

IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000012')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000012', @BookProjectId, N'Sprint 1: Đặt lịch và quản lý xe', DATEADD(DAY, -14, @NowBook), DATEADD(DAY, 0, @NowBook), 1, 1, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000013')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000013', @BookProjectId, N'Sprint 2: Thanh toán và báo cáo', DATEADD(DAY, 1, @NowBook), DATEADD(DAY, 14, @NowBook), 0, 0, @NowBook);

IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000040')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000040', N'Tài khoản và phân quyền', N'Quản lý admin trung tâm, giáo viên, học viên và quyền truy cập theo vai trò.', @BookProjectId, DATEADD(DAY, -20, @NowBook), DATEADD(DAY, -8, @NowBook), 'Completed', 'D0000001-0001-0001-0001-000000000005', @NowBook, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000041')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000041', N'Quản lý xe tập lái', N'Lưu hồ sơ xe, biển số, trạng thái bảo trì và lịch sử sử dụng.', @BookProjectId, DATEADD(DAY, -16, @NowBook), DATEADD(DAY, -2, @NowBook), 'InProgress', 'D0000001-0001-0001-0001-000000000007', @NowBook, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000042')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000042', N'Đặt lịch thực hành', N'Học viên chọn xe, giáo viên, khung giờ và nhận xác nhận lịch học.', @BookProjectId, DATEADD(DAY, -10, @NowBook), DATEADD(DAY, 8, @NowBook), 'InProgress', 'D0000001-0001-0001-0001-000000000006', @NowBook, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000043')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000043', N'Thanh toán và hóa đơn', N'Ghi nhận học phí, công nợ, biên lai và trạng thái thanh toán.', @BookProjectId, DATEADD(DAY, 0, @NowBook), DATEADD(DAY, 20, @NowBook), 'Planned', 'D0000001-0001-0001-0001-000000000012', @NowBook, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000044')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('bbbbbbbb-0000-0000-0000-000000000044', N'Báo cáo vận hành', N'Tổng hợp tỷ lệ sử dụng xe, số buổi học, doanh thu và lịch giáo viên.', @BookProjectId, DATEADD(DAY, 7, @NowBook), DATEADD(DAY, 30, @NowBook), 'Planned', 'D0000001-0001-0001-0001-000000000012', @NowBook, @NowBook);

IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000050')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt) VALUES ('bbbbbbbb-0000-0000-0000-000000000050', N'Tài khoản', '#3b82f6', N'Luồng đăng nhập, hồ sơ người dùng và phân quyền.', @BookProjectId, @BookWorkspaceId, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000051')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt) VALUES ('bbbbbbbb-0000-0000-0000-000000000051', N'Xe tập lái', '#10b981', N'Quản lý xe, bảo trì và lịch sử sử dụng.', @BookProjectId, @BookWorkspaceId, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000052')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt) VALUES ('bbbbbbbb-0000-0000-0000-000000000052', N'Đặt lịch', '#f59e0b', N'Luồng booking, xác nhận và hủy lịch.', @BookProjectId, @BookWorkspaceId, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000053')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt) VALUES ('bbbbbbbb-0000-0000-0000-000000000053', N'Thanh toán', '#8b5cf6', N'Học phí, biên lai và công nợ.', @BookProjectId, @BookWorkspaceId, @NowBook);
IF NOT EXISTS (SELECT 1 FROM Labels WHERE Id = 'bbbbbbbb-0000-0000-0000-000000000054')
    INSERT INTO Labels (Id, Name, ColorCode, Description, ProjectId, WorkspaceId, CreatedAt) VALUES ('bbbbbbbb-0000-0000-0000-000000000054', N'Báo cáo', '#06b6d4', N'Báo cáo vận hành trung tâm.', @BookProjectId, @BookWorkspaceId, @NowBook);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000001', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000012', 'bbbbbbbb-0000-0000-0000-000000000030', 'bbbbbbbb-0000-0000-0000-000000000024', N'Tạo tài khoản admin, giáo viên và học viên', N'Hoàn thiện luồng tạo tài khoản, gán vai trò và kích hoạt hồ sơ người dùng cho trung tâm dạy lái.', 3, 3, DATEADD(DAY, -14, @NowBook), DATEADD(DAY, -11, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000005', DATEADD(DAY, -11, @NowBook), DATEADD(DAY, -16, @NowBook), DATEADD(DAY, -11, @NowBook), 0, 0, 12, 12, 10000, 'BOOKXE-1', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000002')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000002', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000012', 'bbbbbbbb-0000-0000-0000-000000000031', 'bbbbbbbb-0000-0000-0000-000000000024', N'API quản lý danh sách xe tập lái', N'Xây dựng endpoint thêm xe, cập nhật trạng thái bảo trì, lọc theo hạng bằng lái và tình trạng khả dụng.', 4, 5, DATEADD(DAY, -13, @NowBook), DATEADD(DAY, -8, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, -8, @NowBook), DATEADD(DAY, -15, @NowBook), DATEADD(DAY, -8, @NowBook), 0, 0, 20, 18, 20000, 'BOOKXE-2', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000003')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000003', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000012', 'bbbbbbbb-0000-0000-0000-000000000032', 'bbbbbbbb-0000-0000-0000-000000000023', N'Màn hình lịch trống theo xe và giáo viên', N'Thiết kế lịch tuần giúp điều phối viên thấy khung giờ còn trống, xe đang bảo trì và giáo viên bận.', 3, 5, DATEADD(DAY, -7, @NowBook), DATEADD(DAY, -1, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, -1, @NowBook), DATEADD(DAY, -8, @NowBook), DATEADD(DAY, -1, @NowBook), 0, 0, 18, 14, 30000, 'BOOKXE-3', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000004')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000004', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000012', 'bbbbbbbb-0000-0000-0000-000000000030', 'bbbbbbbb-0000-0000-0000-000000000022', N'Luồng học viên đặt lịch thực hành trong 3 bước', N'Học viên chọn gói học, ngày giờ, xe hoặc giáo viên mong muốn rồi gửi yêu cầu xác nhận.', 4, 8, DATEADD(DAY, -4, @NowBook), DATEADD(DAY, 4, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, 4, @NowBook), DATEADD(DAY, -4, @NowBook), @NowBook, 0, 0, 28, 12, 40000, 'BOOKXE-4', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000005')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000005', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000012', 'bbbbbbbb-0000-0000-0000-000000000031', 'bbbbbbbb-0000-0000-0000-000000000022', N'Kiểm tra trùng lịch xe, giáo viên và học viên', N'Backend cần chặn booking nếu cùng xe, cùng giáo viên hoặc cùng học viên đã có lịch ở khung giờ giao nhau.', 1, 5, DATEADD(DAY, -3, @NowBook), DATEADD(DAY, 3, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, 3, @NowBook), DATEADD(DAY, -3, @NowBook), @NowBook, 0, 0, 18, 8, 50000, 'BOOKXE-5', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000006')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000006', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000012', 'bbbbbbbb-0000-0000-0000-000000000032', 'bbbbbbbb-0000-0000-0000-000000000021', N'Trang chi tiết phiếu đặt lịch', N'Hiển thị thông tin học viên, giáo viên, xe, địa điểm tập, trạng thái xác nhận và ghi chú của điều phối viên.', 3, 3, DATEADD(DAY, 1, @NowBook), DATEADD(DAY, 5, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, 5, @NowBook), @NowBook, @NowBook, 0, 0, 12, 0, 60000, 'BOOKXE-6', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000007')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000007', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000013', 'bbbbbbbb-0000-0000-0000-000000000031', 'bbbbbbbb-0000-0000-0000-000000000021', N'Tích hợp thanh toán học phí và đặt cọc', N'Tạo API ghi nhận thanh toán tiền mặt/chuyển khoản, đối soát công nợ và khóa lịch khi chưa đặt cọc.', 2, 8, DATEADD(DAY, 2, @NowBook), DATEADD(DAY, 10, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000007', DATEADD(DAY, 10, @NowBook), @NowBook, @NowBook, 0, 0, 32, 0, 70000, 'BOOKXE-7', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000008')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000008', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000013', 'bbbbbbbb-0000-0000-0000-000000000030', 'bbbbbbbb-0000-0000-0000-000000000021', N'Xuất hóa đơn và biên lai cho học viên', N'Tạo mẫu biên lai, mã giao dịch, trạng thái đã thu và chức năng tải PDF cho học viên.', 3, 5, DATEADD(DAY, 4, @NowBook), DATEADD(DAY, 12, @NowBook), @BookAdminId, 'D0000001-0001-0001-0001-000000000012', DATEADD(DAY, 12, @NowBook), @NowBook, @NowBook, 0, 0, 20, 0, 80000, 'BOOKXE-8', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000009')
    INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000009', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000030', 'bbbbbbbb-0000-0000-0000-000000000020', N'Nhắc lịch học qua email và thông báo trong app', N'Tự động gửi nhắc lịch trước 24 giờ và trước 2 giờ cho học viên, giáo viên và điều phối viên.', 2, 5, @BookAdminId, @NowBook, @NowBook, 0, 0, 16, 0, 90000, 'BOOKXE-9', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000010')
    INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000010', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000031', 'bbbbbbbb-0000-0000-0000-000000000020', N'Báo cáo tỷ lệ sử dụng xe theo tuần', N'Tổng hợp số giờ xe được đặt, số giờ bảo trì, tỷ lệ hủy lịch và danh sách xe cần kiểm tra.', 3, 5, @BookAdminId, @NowBook, @NowBook, 0, 0, 18, 0, 100000, 'BOOKXE-10', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000011')
    INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000011', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000032', 'bbbbbbbb-0000-0000-0000-000000000020', N'Giao diện lịch dạy cá nhân cho giáo viên', N'Giáo viên xem lịch theo ngày, xác nhận đã dạy, ghi chú lỗi xe hoặc đánh giá kỹ năng học viên.', 3, 5, @BookAdminId, @NowBook, @NowBook, 0, 0, 18, 0, 110000, 'BOOKXE-11', @BookWorkspaceId);
IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = 'bbbbbbbb-1000-0000-0000-000000000012')
    INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000012', @BookProjectId, 'bbbbbbbb-0000-0000-0000-000000000031', 'bbbbbbbb-0000-0000-0000-000000000020', N'API đánh giá buổi học sau khi hoàn thành', N'Lưu nhận xét của giáo viên, số km thực hành, kỹ năng cần cải thiện và đề xuất lịch học tiếp theo.', 3, 3, @BookAdminId, @NowBook, @NowBook, 0, 0, 12, 0, 120000, 'BOOKXE-12', @BookWorkspaceId);

IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = 'bbbbbbbb-1000-0000-0000-000000000004' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000004', 'D0000001-0001-0001-0001-000000000006', 1, 55, 1.0, 28, 12, DATEADD(DAY, -4, @NowBook), @NowBook, N'Phát triển wizard đặt lịch 3 bước.', 4);
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = 'bbbbbbbb-1000-0000-0000-000000000005' AND UserId = 'D0000001-0001-0001-0001-000000000007')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000005', 'D0000001-0001-0001-0001-000000000007', 1, 45, 1.0, 18, 8, DATEADD(DAY, -3, @NowBook), @NowBook, N'Viết rule kiểm tra trùng lịch.', 1);
IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = 'bbbbbbbb-1000-0000-0000-000000000003' AND UserId = 'D0000001-0001-0001-0001-000000000009')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('bbbbbbbb-1000-0000-0000-000000000003', 'D0000001-0001-0001-0001-000000000009', 1, 90, 1.0, 10, 8, DATEADD(DAY, -4, @NowBook), @NowBook, N'Kiểm thử lịch tuần và trạng thái xe.', 3);

IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'bbbbbbbb-2000-0000-0000-000000000001')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted)
    VALUES ('bbbbbbbb-2000-0000-0000-000000000001', 'bbbbbbbb-1000-0000-0000-000000000004', 'WorkTask', @BookTeacherId, N'Nên cho giáo viên khóa một số khung giờ bận trước khi học viên đặt lịch, tránh phải hủy thủ công.', DATEADD(DAY, -1, @NowBook), DATEADD(DAY, -1, @NowBook), 0);
IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = 'bbbbbbbb-2000-0000-0000-000000000002')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted)
    VALUES ('bbbbbbbb-2000-0000-0000-000000000002', 'bbbbbbbb-1000-0000-0000-000000000005', 'WorkTask', 'D0000001-0001-0001-0001-000000000007', N'Đã thêm kiểm tra giao nhau theo StartTime/EndTime, bước tiếp theo là viết test cho trường hợp đổi xe sát giờ.', DATEADD(HOUR, -8, @NowBook), DATEADD(HOUR, -8, @NowBook), 0);

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Id = 'bbbbbbbb-3000-0000-0000-000000000001')
    INSERT INTO Pages (Id, Title, Content, ProjectId, CreatedById, SortOrder, IsLocked, IsArchived, IsPrivate, IsStarred, CreatedAt, UpdatedAt)
    VALUES ('bbbbbbbb-3000-0000-0000-000000000001', N'Quy trình vận hành đặt lịch xe tập lái',
    N'{"type":"doc","content":[{"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"Quy trình đặt lịch book xe"}]},{"type":"paragraph","content":[{"type":"text","text":"Học viên chọn gói học, khung giờ, giáo viên hoặc xe mong muốn. Hệ thống kiểm tra trùng lịch, trạng thái bảo trì và yêu cầu đặt cọc trước khi xác nhận."}]},{"type":"paragraph","content":[{"type":"text","text":"Điều phối viên theo dõi dashboard để xử lý lịch chờ xác nhận, xe quá tải và các buổi học cần đổi giáo viên."}]}]}',
    @BookProjectId, @BookAdminId, 3, 0, 0, 0, 1, DATEADD(DAY, -2, @NowBook), @NowBook);

IF NOT EXISTS (SELECT 1 FROM Notifications WHERE Id = 'bbbbbbbb-4000-0000-0000-000000000001')
    INSERT INTO Notifications (Id, UserId, Title, Content, LinkUrl, IsRead, CreatedAt, NotificationType, RelatedTaskId, RelatedProjectId, TriggeredByUserId)
    VALUES ('bbbbbbbb-4000-0000-0000-000000000001', @BookAdminId, N'Lịch học thực hành cần xác nhận', N'Học viên Nguyễn Thảo My vừa gửi yêu cầu đặt xe cho buổi học cuối tuần.', '/projects/bbbbbbbb-0000-0000-0000-000000000011/tasks/bbbbbbbb-1000-0000-0000-000000000004', 0, DATEADD(MINUTE, -45, @NowBook), 'BOOKING_REQUESTED', 'bbbbbbbb-1000-0000-0000-000000000004', @BookProjectId, @BookStudentId);

IF NOT EXISTS (SELECT 1 FROM SiteAuditLogs WHERE Id = 'bbbbbbbb-5000-0000-0000-000000000001')
    INSERT INTO SiteAuditLogs (Id, EntityId, EntityType, Action, OldValue, NewValue, UserId, CreatedAt)
    VALUES ('bbbbbbbb-5000-0000-0000-000000000001', @BookProjectId, 'Project', 'Create', '{}', '{"Name":"Hệ thống đặt lịch book xe trung tâm dạy lái","Identifier":"BOOKXE"}', @BookAdminId, DATEADD(DAY, -20, @NowBook));
GO

PRINT N'✅ Part 9 to 12 complete: Gamification, Integrations, AI, Audit logs';
GO

-- ============================================================================
-- 26. CUSTOM FIELD DEFINITIONS (For Demo Project)
-- ============================================================================
DECLARE @NowCf DATETIME2 = GETUTCDATE();
DECLARE @Proj1Id UNIQUEIDENTIFIER = 'C0000001-0001-0001-0001-000000000001';

-- Field 1: Mức ảnh hưởng (Select)
IF NOT EXISTS (SELECT 1 FROM CustomFieldDefinitions WHERE Id = 'CFD00001-0001-0001-0001-000000000001')
    INSERT INTO CustomFieldDefinitions (Id, ProjectId, Name, [Key], Type, IsRequired, OptionsJson, IsVisible, SortOrder, IsDeleted, CreatedAt, UpdatedAt)
    VALUES ('CFD00001-0001-0001-0001-000000000001', @Proj1Id, N'Mức ảnh hưởng', 'muc_anh_huong', 'Select', 0, '["Thấp", "Trung bình", "Cao"]', 1, 0, 0, @NowCf, @NowCf);

-- Field 2: Mã khách hàng (Text)
IF NOT EXISTS (SELECT 1 FROM CustomFieldDefinitions WHERE Id = 'CFD00001-0001-0001-0001-000000000002')
    INSERT INTO CustomFieldDefinitions (Id, ProjectId, Name, [Key], Type, IsRequired, OptionsJson, IsVisible, SortOrder, IsDeleted, CreatedAt, UpdatedAt)
    VALUES ('CFD00001-0001-0001-0001-000000000002', @Proj1Id, N'Mã khách hàng', 'ma_khach_hang', 'Text', 0, NULL, 1, 1, 0, @NowCf, @NowCf);

-- Field 3: Ngày nghiệm thu (Date)
IF NOT EXISTS (SELECT 1 FROM CustomFieldDefinitions WHERE Id = 'CFD00001-0001-0001-0001-000000000003')
    INSERT INTO CustomFieldDefinitions (Id, ProjectId, Name, [Key], Type, IsRequired, OptionsJson, IsVisible, SortOrder, IsDeleted, CreatedAt, UpdatedAt)
    VALUES ('CFD00001-0001-0001-0001-000000000003', @Proj1Id, N'Ngày nghiệm thu', 'ngay_nghiem_thu', 'Date', 0, NULL, 1, 2, 0, @NowCf, @NowCf);

-- Field 4: Cần QA xác nhận (Checkbox)
IF NOT EXISTS (SELECT 1 FROM CustomFieldDefinitions WHERE Id = 'CFD00001-0001-0001-0001-000000000004')
    INSERT INTO CustomFieldDefinitions (Id, ProjectId, Name, [Key], Type, IsRequired, OptionsJson, IsVisible, SortOrder, IsDeleted, CreatedAt, UpdatedAt)
    VALUES ('CFD00001-0001-0001-0001-000000000004', @Proj1Id, N'Cần QA xác nhận', 'can_qa_xac_nhan', 'Checkbox', 0, NULL, 1, 3, 0, @NowCf, @NowCf);
GO

-- ============================================================================
-- 27. CUSTOM FIELD VALUES (For Demo Tasks)
-- ============================================================================
DECLARE @NowCfv DATETIME2 = GETUTCDATE();
DECLARE @Task1Id UNIQUEIDENTIFIER = '70000001-0001-0001-0001-000000000001'; -- SPRINT-1
DECLARE @Task2Id UNIQUEIDENTIFIER = '70000001-0001-0001-0001-000000000002'; -- SPRINT-2

-- Values for SPRINT-1
IF NOT EXISTS (SELECT 1 FROM CustomFieldValues WHERE WorkTaskId = @Task1Id AND FieldDefinitionId = 'CFD00001-0001-0001-0001-000000000001')
    INSERT INTO CustomFieldValues (Id, WorkTaskId, FieldDefinitionId, Value, CreatedAt, UpdatedAt)
    VALUES (NEWID(), @Task1Id, 'CFD00001-0001-0001-0001-000000000001', N'Cao', @NowCfv, @NowCfv);

IF NOT EXISTS (SELECT 1 FROM CustomFieldValues WHERE WorkTaskId = @Task1Id AND FieldDefinitionId = 'CFD00001-0001-0001-0001-000000000002')
    INSERT INTO CustomFieldValues (Id, WorkTaskId, FieldDefinitionId, Value, CreatedAt, UpdatedAt)
    VALUES (NEWID(), @Task1Id, 'CFD00001-0001-0001-0001-000000000002', N'KH-NOVATECH', @NowCfv, @NowCfv);

-- Values for SPRINT-2
IF NOT EXISTS (SELECT 1 FROM CustomFieldValues WHERE WorkTaskId = @Task2Id AND FieldDefinitionId = 'CFD00001-0001-0001-0001-000000000001')
    INSERT INTO CustomFieldValues (Id, WorkTaskId, FieldDefinitionId, Value, CreatedAt, UpdatedAt)
    VALUES (NEWID(), @Task2Id, 'CFD00001-0001-0001-0001-000000000001', N'Trung bình', @NowCfv, @NowCfv);

IF NOT EXISTS (SELECT 1 FROM CustomFieldValues WHERE WorkTaskId = @Task2Id AND FieldDefinitionId = 'CFD00001-0001-0001-0001-000000000004')
    INSERT INTO CustomFieldValues (Id, WorkTaskId, FieldDefinitionId, Value, CreatedAt, UpdatedAt)
    VALUES (NEWID(), @Task2Id, 'CFD00001-0001-0001-0001-000000000004', N'true', @NowCfv, @NowCfv);

-- Additional active work for the primary SprintA demo project. The fixed
-- SequenceIds make this section safe to run repeatedly.
DECLARE @PrimaryProjectId UNIQUEIDENTIFIER = 'C0000001-0001-0001-0001-000000000001';
DECLARE @PrimaryWorkspaceId UNIQUEIDENTIFIER = 'A0000001-0001-0001-0001-000000000001';
DECLARE @PrimaryNow DATETIME2 = GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM ProjectMembers WHERE ProjectId = @PrimaryProjectId AND UserId = 'D0000001-0001-0001-0001-000000000001')
    INSERT INTO ProjectMembers (ProjectId, UserId, ProjectRole, JoinedAt, Status)
    VALUES (@PrimaryProjectId, 'D0000001-0001-0001-0001-000000000001', 'PROJECT_MANAGER', @PrimaryNow, 1);

INSERT INTO WorkTasks (Id, ProjectId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
SELECT item.Id, @PrimaryProjectId, item.TaskTypeId, item.TaskStatusId, item.Title, item.Description, item.Priority, item.StoryPoints,
       'D0000001-0001-0001-0001-000000000003', item.AssignedUserId, DATEADD(DAY, item.DueOffset, @PrimaryNow), @PrimaryNow, @PrimaryNow,
       0, 0, item.EstimatedHours, item.ActualHours, item.SortOrder, item.SequenceId, @PrimaryWorkspaceId
FROM (VALUES
    (CAST('70000001-0001-0001-0001-000000000016' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), N'Define Q3 product analytics baseline', N'Collect current activation, retention, and conversion metrics for the Q3 planning baseline.', 3, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000012' AS UNIQUEIDENTIFIER), -8, 10, 6, 160000, N'SPRINT-16'),
    (CAST('70000001-0001-0001-0001-000000000017' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), N'Write acceptance criteria for AI Copilot', N'Document acceptance criteria for context, permissions, and failure states.', 2, CAST(5.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), -3, 14, 4, 170000, N'SPRINT-17'),
    (CAST('70000001-0001-0001-0001-000000000018' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000002' AS UNIQUEIDENTIFIER), N'Review design tokens for mobile navigation', N'Prioritize small-screen navigation and touch target consistency.', 3, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000010' AS UNIQUEIDENTIFIER), 1, 12, 0, 180000, N'SPRINT-18'),
    (CAST('70000001-0001-0001-0001-000000000019' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000002' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000002' AS UNIQUEIDENTIFIER), N'Fix duplicate notification delivery', N'Investigate duplicate SignalR notification events reported by pilot users.', 1, CAST(2.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000007' AS UNIQUEIDENTIFIER), -2, 8, 2, 190000, N'SPRINT-19'),
    (CAST('70000001-0001-0001-0001-000000000020' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000002' AS UNIQUEIDENTIFIER), N'Prepare customer onboarding checklist', N'Create a repeatable onboarding checklist for the first five enterprise customers.', 3, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000013' AS UNIQUEIDENTIFIER), 3, 10, 0, 200000, N'SPRINT-20'),
    (CAST('70000001-0001-0001-0001-000000000021' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), N'Implement project activity digest', N'Build the daily project digest with overdue, review, and blocked work summaries.', 2, CAST(5.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000008' AS UNIQUEIDENTIFIER), -1, 18, 7, 210000, N'SPRINT-21'),
    (CAST('70000001-0001-0001-0001-000000000022' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), N'Add audit trail for permission changes', N'Record permission preset changes with actor and timestamp for compliance review.', 2, CAST(5.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000007' AS UNIQUEIDENTIFIER), 2, 16, 5, 220000, N'SPRINT-22'),
    (CAST('70000001-0001-0001-0001-000000000023' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), N'Validate custom field migration', N'Validate all custom field types and values after project migration.', 2, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000009' AS UNIQUEIDENTIFIER), 0, 12, 3, 230000, N'SPRINT-23'),
    (CAST('70000001-0001-0001-0001-000000000024' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), N'Optimize project reports query', N'Reduce report load time for projects with more than one thousand tasks.', 2, CAST(8.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000012' AS UNIQUEIDENTIFIER), 4, 24, 8, 240000, N'SPRINT-24'),
    (CAST('70000001-0001-0001-0001-000000000025' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000002' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), N'Fix unread inbox badge mismatch', N'Correct the unread item count after filtering the Unified Inbox.', 1, CAST(2.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000006' AS UNIQUEIDENTIFIER), -4, 6, 4, 250000, N'SPRINT-25'),
    (CAST('70000001-0001-0001-0001-000000000026' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000004' AS UNIQUEIDENTIFIER), N'Review release checklist', N'Confirm quality, security, PWA, and documentation gates before release.', 2, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000004' AS UNIQUEIDENTIFIER), -1, 8, 6, 260000, N'SPRINT-26'),
    (CAST('70000001-0001-0001-0001-000000000027' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000004' AS UNIQUEIDENTIFIER), N'QA regression for Kanban drag and drop', N'Run regression coverage for ordering, filters, and status transitions.', 2, CAST(5.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000009' AS UNIQUEIDENTIFIER), 1, 16, 9, 270000, N'SPRINT-27'),
    (CAST('70000001-0001-0001-0001-000000000028' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000004' AS UNIQUEIDENTIFIER), N'Finalize Vietnamese localization review', N'Review Vietnamese copy for common task and project workflows.', 3, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), 2, 10, 8, 280000, N'SPRINT-28'),
    (CAST('70000001-0001-0001-0001-000000000029' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Publish SprintA demo walkthrough', N'Prepare the product walkthrough recording and presenter notes.', 3, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000010' AS UNIQUEIDENTIFIER), -1, 8, 8, 290000, N'SPRINT-29'),
    (CAST('70000001-0001-0001-0001-000000000030' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Complete incident response runbook', N'Document ownership, escalation, and recovery steps for production incidents.', 2, CAST(5.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000011' AS UNIQUEIDENTIFIER), 0, 16, 16, 300000, N'SPRINT-30'),
    (CAST('70000001-0001-0001-0001-000000000031' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Archive completed beta feedback', N'Classify completed feedback and retain decisions for future roadmap planning.', 4, CAST(2.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000013' AS UNIQUEIDENTIFIER), -10, 6, 6, 310000, N'SPRINT-31'),
    (CAST('70000001-0001-0001-0001-000000000032' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Document demo data reset procedure', N'Write the repeatable local reset and validation procedure for demo data.', 3, CAST(2.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), -6, 6, 6, 320000, N'SPRINT-32'),
    (CAST('70000001-0001-0001-0001-000000000033' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Create customer success health dashboard', N'Expose renewal risk and account health signals for customer success review.', 3, CAST(5.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000013' AS UNIQUEIDENTIFIER), -5, 18, 18, 330000, N'SPRINT-33'),
    (CAST('70000001-0001-0001-0001-000000000034' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Analyze support request trends', N'Identify recurring support topics and propose self-service improvements.', 3, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000012' AS UNIQUEIDENTIFIER), -7, 10, 10, 340000, N'SPRINT-34'),
    (CAST('70000001-0001-0001-0001-000000000035' AS UNIQUEIDENTIFIER), CAST('f0000001-0001-0001-0001-000000000001' AS UNIQUEIDENTIFIER), CAST('e0000001-0001-0001-0001-000000000005' AS UNIQUEIDENTIFIER), N'Close SprintA demo readiness review', N'Capture final risks, owners, and release decision for the demo.', 2, CAST(3.0 AS FLOAT), CAST('D0000001-0001-0001-0001-000000000003' AS UNIQUEIDENTIFIER), -1, 8, 8, 350000, N'SPRINT-35')
) AS item(Id, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, AssignedUserId, DueOffset, EstimatedHours, ActualHours, SortOrder, SequenceId)
WHERE NOT EXISTS (SELECT 1 FROM WorkTasks task WHERE task.SequenceId = item.SequenceId AND task.ProjectId = @PrimaryProjectId);
GO

-- ============================================================================
-- 28. WAVE A ACTION FIXTURES (stable, realistic, and safe to re-run)
-- ============================================================================
-- These records give the Actionable AI demo a known project context for
-- Cycle, Module, Page, View, Intake and task update/assignment/comment flows.
DECLARE @WaveANow DATETIME2 = GETUTCDATE();
DECLARE @WaveAProjectId UNIQUEIDENTIFIER = 'C0000001-0001-0001-0001-000000000001';
DECLARE @WaveAOwnerId UNIQUEIDENTIFIER = 'D0000001-0001-0001-0001-000000000001';

IF NOT EXISTS (SELECT 1 FROM Sprints WHERE Id = '7A000001-0001-0001-0001-000000000001')
    INSERT INTO Sprints (Id, ProjectId, Name, StartDate, EndDate, Status, IsFavorite, CreatedAt)
    VALUES ('7A000001-0001-0001-0001-000000000001', @WaveAProjectId, N'Chu kỳ demo AI: Hoàn thiện trải nghiệm Copilot',
        DATEADD(DAY, -2, @WaveANow), DATEADD(DAY, 12, @WaveANow), 1, 1, @WaveANow);

IF NOT EXISTS (SELECT 1 FROM Modules WHERE Id = '7A000002-0001-0001-0001-000000000001')
    INSERT INTO Modules (Id, Name, Description, ProjectId, StartDate, TargetDate, Status, LeadId, CreatedAt, UpdatedAt)
    VALUES ('7A000002-0001-0001-0001-000000000001', N'Copilot có thể thao tác',
        N'Hoàn thiện luồng xem trước, xác nhận và thực thi action có kiểm soát cho người dùng demo.',
        @WaveAProjectId, DATEADD(DAY, -3, @WaveANow), DATEADD(DAY, 12, @WaveANow), 'InProgress', @WaveAOwnerId, @WaveANow, @WaveANow);

IF NOT EXISTS (SELECT 1 FROM Pages WHERE Id = '7A000003-0001-0001-0001-000000000001')
    INSERT INTO Pages (Id, Title, Content, ProjectId, CreatedById, SortOrder, IsLocked, IsArchived, IsPrivate, IsStarred, CreatedAt, UpdatedAt)
    VALUES ('7A000003-0001-0001-0001-000000000001', N'Kịch bản demo AI Copilot',
        N'{"type":"doc","content":[{"type":"heading","attrs":{"level":1},"content":[{"type":"text","text":"Kịch bản demo AI Copilot"}]},{"type":"paragraph","content":[{"type":"text","text":"Tạo chu kỳ, mô-đun, tài liệu, bộ lọc đã lưu và yêu cầu mới. Mọi thay đổi phải được xem trước và xác nhận trước khi thực thi."}]},{"type":"bulletList","content":[{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Kiểm tra action chỉ tạo một thực thể khi xác nhận hai lần."}]}]},{"type":"listItem","content":[{"type":"paragraph","content":[{"type":"text","text":"Làm mới trang để xác nhận dữ liệu vẫn được lưu."}]}]}]}]}',
        @WaveAProjectId, @WaveAOwnerId, 90, 0, 0, 0, 1, @WaveANow, @WaveANow);

IF NOT EXISTS (SELECT 1 FROM ProjectViews WHERE Id = '7A000004-0001-0001-0001-000000000001')
    INSERT INTO ProjectViews (Id, ProjectId, Name, Description, QueryMetadata, IsFavorite, CreatedById, CreatedAt, UpdatedAt)
    VALUES ('7A000004-0001-0001-0001-000000000001', @WaveAProjectId, N'Việc cần xử lý trong tuần',
        N'Hiển thị các công việc chưa hoàn thành, ưu tiên cao hoặc đã quá hạn để điều phối demo.',
        N'{"status":["BACKLOG","TO DO","IN PROGRESS","IN REVIEW"],"priority":[1,2],"due":"this_week"}', 1, @WaveAOwnerId, @WaveANow, @WaveANow);

IF NOT EXISTS (SELECT 1 FROM Intakes WHERE Id = '7A000005-0001-0001-0001-000000000001')
    INSERT INTO Intakes (Id, Title, Description, Source, Status, Priority, DesiredDueDate, ProjectId, SubmittedById, ReviewedById, CreatedIssueId, CreatedAt, ReviewedAt)
    VALUES ('7A000005-0001-0001-0001-000000000001', N'Khách hàng cần xuất báo cáo tiến độ theo tuần',
        N'Đề nghị thêm báo cáo tuần có trạng thái công việc, tải của thành viên và danh sách việc quá hạn.',
        'FORM', 'Pending', 2, DATEADD(DAY, 7, @WaveANow), @WaveAProjectId,
        'D0000001-0001-0001-0001-000000000013', NULL, NULL, @WaveANow, NULL);

IF NOT EXISTS (SELECT 1 FROM WorkTasks WHERE Id = '7A000006-0001-0001-0001-000000000001')
    INSERT INTO WorkTasks (Id, ProjectId, SprintId, TaskTypeId, TaskStatusId, Title, Description, Priority, StoryPoints, PlannedStartDate, PlannedEndDate, ReporterId, AssignedUserId, DueDate, CreatedAt, UpdatedAt, IsDeleted, IsArchived, TotalEstimatedHours, TotalActualHours, SortOrder, SequenceId, WorkspaceId)
    VALUES ('7A000006-0001-0001-0001-000000000001', @WaveAProjectId, '7A000001-0001-0001-0001-000000000001',
        'F0000001-0001-0001-0001-000000000001', 'E0000001-0001-0001-0001-000000000002', N'Xác nhận luồng action Wave A',
        N'Công việc mẫu để kiểm tra cập nhật trạng thái, ưu tiên, hạn hoàn thành, giao việc và bình luận từ AI Copilot.',
        2, 3, @WaveANow, DATEADD(DAY, 5, @WaveANow), @WaveAOwnerId, 'D0000001-0001-0001-0001-000000000006', DATEADD(DAY, 5, @WaveANow),
        @WaveANow, @WaveANow, 0, 0, 8, 0, 360000, 'SPRINT-WAVEA-01', 'A0000001-0001-0001-0001-000000000001');

IF NOT EXISTS (SELECT 1 FROM TaskAssignments WHERE WorkTaskId = '7A000006-0001-0001-0001-000000000001' AND UserId = 'D0000001-0001-0001-0001-000000000006')
    INSERT INTO TaskAssignments (WorkTaskId, UserId, Status, ProgressPercent, ContributionWeight, EstimatedHours, TotalActualHours, ActualStartDate, ActualEndDate, Description, Priority)
    VALUES ('7A000006-0001-0001-0001-000000000001', 'D0000001-0001-0001-0001-000000000006', 0, 0, 1.0, 8, 0, @WaveANow, @WaveANow,
        N'Người phụ trách kiểm tra luồng thao tác AI.', 2);

IF NOT EXISTS (SELECT 1 FROM Comments WHERE Id = '7A000007-0001-0001-0001-000000000001')
    INSERT INTO Comments (Id, EntityId, EntityType, UserId, Content, CreatedAt, UpdatedAt, IsDeleted)
    VALUES ('7A000007-0001-0001-0001-000000000001', '7A000006-0001-0001-0001-000000000001', 'WorkTask', @WaveAOwnerId,
        N'Bình luận mẫu: chỉ thực thi action sau khi người dùng xác nhận trên thẻ xem trước.', @WaveANow, @WaveANow, 0);
GO

PRINT N'============================================================================';
PRINT N'🎉 SPRINTA DEMO DATA SEEDED SUCCESSFULLY FOR NOVATECH SOLUTIONS 🎉';
PRINT N'============================================================================';
GO




