# SprintA — So sánh Local ZIP với GitHub

Ngày audit: 22/07/2026

## 1. Mốc Git trong bản ZIP

- Branch local: `KhoiSigma`
- Local HEAD: `62c6b9137975c26145a9d6fce8426eed25ca8226`
- `origin/main` được lưu trong ZIP: `64242adc2cd8fb627c8432ba7feaa99a18dbb7f5`
- Mốc `origin/main` cuối trong ZIP: 2026-07-16T14:21:03+07:00 | Merge pull request #106 from DinhTuanKhoiTB01696/profile/kiet
- So với `origin/main` trong ZIP: local ahead **8**, behind **0**
- GitHub giao diện hiện hiển thị nhánh `main` có 309 commits; bản `origin/main` trong ZIP có 299 commits. Vì môi trường audit không fetch trực tiếp được Git, cần chạy `git fetch` trên máy Windows để xác nhận 10 commit mới nhất trước merge.

### 8 commit local chưa có trong `origin/main` của ZIP

```text
62c6b913 fix(security): require external JWT secret and protect GitHub credentials
2b36eea3 fix: allow authenticated users to access stickies
1e74614e feat: complete AI integrations and floating stickies
2ea7f114 Merge remote-tracking branch 'origin/main' into integration/demo-release
f2042534 chore: remove generated artifacts
b18c2525 Merge remote-tracking branch 'origin/main' into integration/demo-release
578d8fa0 fix: connect cover upload to mounted project flow
2dfae36b fix: complete Khoi cover and AI review
```

## 2. Kết luận quan trọng

**Không chạy `git pull` và không restore toàn bộ từ `origin/main`.**

Local chứa các sửa mới hơn và an toàn hơn:

- JWT bắt buộc lấy secret ngoài tracked config.
- Data Protection cho GitHub credential.
- Private upload protection.
- Migration/deployment command an toàn.
- AI attachment/history/stickies.
- P0 permission, dependency, reward, RowVersion và AI safety.

GitHub `main` hiện vẫn có `Program.cs` kiểu cũ: Development có thể fallback InMemory, `/uploads` được serve tĩnh, migration/raw SQL/seed chạy khi startup và lỗi migration có thể bị bỏ qua. `appsettings.json` trên GitHub cũng dùng các chuỗi `CHANGE_ME...` thay vì placeholder bị runtime từ chối. Pull đè hai file này sẽ làm **regression bảo mật**.

## 3. Sự thật về worktree ZIP

Sau khi bỏ qua **579 file chỉ khác CRLF/LF**, còn:

- 57 file thực sự modified.
- 154 file deleted.
- 48 mục untracked.
- Trong 154 deletion: 153 là docs/ảnh; chỉ một source file là `IntegrationSchemaGuard.cs`, đã được thay bằng migration/deployment flow mới nên **không nên restore**.

### 57 file có thay đổi nội dung thật

```text
Backend/TaskManagement.Tests/Logic/AiActionPermissionTests.cs
Backend/TaskManagement.Tests/Logic/AiVoiceControllerTests.cs
Backend/TaskManagement.Tests/Logic/AuthLogicTests.cs
Backend/TaskManagement.Tests/Logic/P006HostingConfigurationTests.cs
Backend/TaskManagement.Tests/Logic/SprintLogicTests.cs
Backend/src/TaskManagement.API/Controllers/AiController.cs
Backend/src/TaskManagement.API/Controllers/AuthController.cs
Backend/src/TaskManagement.API/Controllers/CommentsController.cs
Backend/src/TaskManagement.API/Controllers/GoalsController.cs
Backend/src/TaskManagement.API/Controllers/InboxController.cs
Backend/src/TaskManagement.API/Controllers/IntegrationsController.cs
Backend/src/TaskManagement.API/Controllers/ProjectMembersController.cs
Backend/src/TaskManagement.API/Controllers/ProjectsController.cs
Backend/src/TaskManagement.API/Controllers/SprintsController.cs
Backend/src/TaskManagement.API/Controllers/TaskDependenciesController.cs
Backend/src/TaskManagement.API/Controllers/UploadsController.cs
Backend/src/TaskManagement.API/Controllers/UsersController.cs
Backend/src/TaskManagement.API/Controllers/WorkTasksController.cs
Backend/src/TaskManagement.API/Controllers/WorkspacesController.cs
Backend/src/TaskManagement.API/Extensions/HostingConfigurationExtensions.cs
Backend/src/TaskManagement.API/Extensions/ServiceCollectionExtensions.cs
Backend/src/TaskManagement.API/Filters/ProjectAuthorizeAttribute.cs
Backend/src/TaskManagement.API/Filters/RequirePermissionAttribute.cs
Backend/src/TaskManagement.API/Program.cs
Backend/src/TaskManagement.API/appsettings.json
Backend/src/TaskManagement.Application/Interfaces/IAiService.cs
Backend/src/TaskManagement.Application/Interfaces/IOtpService.cs
Backend/src/TaskManagement.Application/Interfaces/IProjectMemberService.cs
Backend/src/TaskManagement.Domain/Entities/PointTransaction.cs
Backend/src/TaskManagement.Domain/Entities/TaskAssignment.cs
Backend/src/TaskManagement.Infrastructure/Data/ApplicationDbContext.cs
Backend/src/TaskManagement.Infrastructure/Data/DataSeeder.cs
Backend/src/TaskManagement.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs
Backend/src/TaskManagement.Infrastructure/Services/AiAttachmentService.cs
Backend/src/TaskManagement.Infrastructure/Services/AiIntegrationService.cs
Backend/src/TaskManagement.Infrastructure/Services/AuthService.cs
Backend/src/TaskManagement.Infrastructure/Services/EmailService.cs
Backend/src/TaskManagement.Infrastructure/Services/GamificationService.cs
Backend/src/TaskManagement.Infrastructure/Services/GeminiAiService.cs
Backend/src/TaskManagement.Infrastructure/Services/GoalService.cs
Backend/src/TaskManagement.Infrastructure/Services/OtpService.cs
Backend/src/TaskManagement.Infrastructure/Services/ProjectMemberService.cs
Backend/src/TaskManagement.Infrastructure/Services/SprintService.cs
Backend/src/TaskManagement.Infrastructure/Services/WorkTaskService.cs
Frontend/src/components/CyclesTab.vue
Frontend/src/components/layout/NexusLayout.vue
Frontend/src/store/useAdminUserStore.js
Frontend/src/utils/permissions.js
Frontend/src/views/AIPage.vue
Frontend/src/views/ProjectSettings.vue
run.bat
scripts/run-sql.ps1
scripts/seed-demo-data.sql
```

### Untracked quan trọng — không được xóa

```text
Backend/TaskManagement.Tests/Logic/AuthSecurityIntegrationTests.cs
Backend/TaskManagement.Tests/Logic/P003DataIntegrityTests.cs
Backend/TaskManagement.Tests/Logic/P004MemberIntegrityTests.cs
Backend/TaskManagement.Tests/Logic/P005RewardConcurrencyTests.cs
Backend/TaskManagement.Tests/Logic/P006UploadSecurityTests.cs
Backend/TaskManagement.Tests/Logic/P007AiSafetyTests.cs
Backend/TaskManagement.Tests/Logic/P0ReportedDefectReproductionTests.cs
Backend/TaskManagement.Tests/Logic/ResourceAuthorizationTests.cs
Backend/src/TaskManagement.API/Controllers/PrivateAttachmentsController.cs
Backend/src/TaskManagement.API/Extensions/DatabaseDeploymentExtensions.cs
Backend/src/TaskManagement.API/Security/
Backend/src/TaskManagement.API/Services/PrivateUploadCleanupService.cs
Backend/src/TaskManagement.Application/Common/EmailCanonicalizer.cs
Backend/src/TaskManagement.Application/Common/ResourcePermissionPolicy.cs
Backend/src/TaskManagement.Application/Interfaces/IResourceAuthorizationService.cs
Backend/src/TaskManagement.Application/Interfaces/ITaskDependencyService.cs
Backend/src/TaskManagement.Domain/Entities/AiActionExecution.cs
Backend/src/TaskManagement.Domain/Rules/
Backend/src/TaskManagement.Infrastructure/Migrations/20260718001618_PreserveTaskAssignmentHistory*
Backend/src/TaskManagement.Infrastructure/Migrations/20260718003622_AddImmutableRewardLedger*
Backend/src/TaskManagement.Infrastructure/Migrations/20260718010229_ReplaceRuntimeSchemaGuards*
Backend/src/TaskManagement.Infrastructure/Migrations/20260718020108_AddAiActionSafetyState*
Backend/src/TaskManagement.Infrastructure/Services/AiSafetyGuard.cs
Backend/src/TaskManagement.Infrastructure/Services/ResourceAuthorizationService.cs
Backend/src/TaskManagement.Infrastructure/Services/SprintScopeValidator.cs
Backend/src/TaskManagement.Infrastructure/Services/TaskDependencyService.cs
```

Ngoài ra:

- `Backend/src/TaskManagement.API/data-protection-keys/` là runtime secret material: không commit, không gửi, không xóa trước khi biết môi trường nào đang dùng.
- `appsettings.Testing.json` cần kiểm tra secret; chỉ commit nếu hoàn toàn là test placeholder.
- Các docs bị delete không làm hỏng runtime, nhưng phải quyết định archive/restore riêng.

## 4. Phần local có nhưng GitHub `origin/main` cũ chưa có

Các nhóm chính:

- AI attachments, conversation history và multimodal.
- Floating/Global Stickies.
- Security configuration validation.
- GitHub credential cleanup.
- Nhiều migrations và test P0.
- Private upload and AI safety implementation.

Số lượng committed difference giữa local HEAD và `origin/main` trong ZIP:

- Added: 33
- Modified: 34
- Deleted: 165

Các deletion committed phần lớn là build output, runtime uploads, logs và script sửa một lần; không nên kéo lại.

## 5. Phần GitHub có nhưng local không có

Trong snapshot `origin/main` nằm trong ZIP, **không có source production quan trọng nào vừa thiếu vừa an toàn để restore nguyên file**.

Các mục local thiếu chủ yếu là:

- `publish_output/`
- runtime `uploads/`
- build logs/output
- one-off patch scripts
- docs/ảnh

Những mục này không nên pull để “sửa chức năng”.

## 6. Lỗi và rủi ro cần giao agent

### CODEX — Backend / Git / Security

1. **P0 Git hygiene:** 57 modified thật + 48 untracked + 153 docs deleted; phải tạo recovery branch và commit theo phase.
2. **P0 remote reconciliation:** fetch remote mới nhất, so sánh 309-commit `main` với local 307-commit history; không pull đè.
3. **P0 secrets:** kiểm tra Git history vì nhiều secret từng xuất hiện trong appsettings.
4. **P0 Data Protection:** key ring đang nằm trong repo worktree; đưa path ra ngoài Git, cấu hình production persistence/permission.
5. **P1 migrations:** bốn migration P0 đang untracked trong ZIP; phải review, test clean DB + existing DB, rồi commit cùng snapshot.
6. **P1 appsettings.Testing:** loại secret thật và quyết định ignore/commit.
7. **P1 line endings:** 579 file bị đánh dấu modified chỉ do CRLF; thêm `.gitattributes`, normalize trong commit riêng.
8. **P1 current remote main regression:** không lấy `Program.cs`/`appsettings.json` từ main; thay vào đó đưa security commit local lên branch/PR.
9. **P1 verify tests:** chạy full .NET suite trên clean Windows worktree; audit trước đây báo 133/133 pass nhưng ZIP hiện còn mixed uncommitted changes.

### KIMI — Frontend

1. **P1 cross-platform install:** `@rolldown/binding-win32-x64-msvc` đang là direct devDependency. `npm ci` fail trên Linux CI. Xóa direct platform package, để Rolldown/Vite tự chọn optional binding.
2. **P1 dependency security:** npm audit trước đó có 5 high + 4 moderate; nâng Axios, DOMPurify, ECharts, Vite và transitive packages từng nhóm; `xlsx` cần thay thế hoặc cô lập.
3. **P1 missing QA scripts:** thêm lint/typecheck/unit/E2E scripts và CI.
4. **P1 login responsive:** mobile form nằm quá xa, overflow và header bị cắt.
5. **P1 chart bundle:** đang dùng ApexCharts + Chart.js + ECharts; bundle chart khoảng 1.8 MB. Chuẩn hóa dần về ECharts.
6. **P2 runtime warnings:** PWA/Rolldown warning, chunk >1.6 MB, duplicate Google initialization/dynamic import warning.
7. **Không pull `package.json` nguyên file từ GitHub:** GitHub thiếu các dependency local cần cho TipTap/PWA/XLSX và vẫn chứa binding Windows-only.

## 7. Quyết định restore

| Nhóm | Hành động |
|---|---|
| `Program.cs`, `appsettings.json`, auth/security | `KEEP_LOCAL`, không pull main |
| P0 migrations/tests/services untracked | `REVIEW_AND_COMMIT_LOCAL` |
| `IntegrationSchemaGuard.cs` | `KEEP_DELETED` nếu migration P0-06 pass |
| publish output, uploads, logs | `KEEP_DELETED` |
| docs/ảnh bị delete | `REVIEW_SEPARATELY`, không ảnh hưởng runtime |
| file thật sự remote-only sau `git fetch` | chỉ restore khi build/import chứng minh cần |
| file BOTH_MODIFIED | manual merge, không restore whole file |

## 8. Thứ tự làm

1. Chạy script `compare-current-github.ps1`.
2. Codex tạo branch reconciliation và xử lý Git/migrations/security.
3. Kimi làm frontend trên worktree riêng.
4. Merge vào integration worktree.
5. Chạy backend tests + frontend build/test.
6. Chỉ sau đó mới push/PR.
