# SPRINTA MASTER EXECUTION PACK

> Bộ tài liệu thực thi được tạo ngày 17/07/2026 từ:
>
> - Repository công khai `DinhTuanKhoiTB01696/QuanLyCongViec`.
> - Bộ Project Bible đã tạo trước đó.
> - `test_cases_1000.xlsx`.
> - `Mệt Vãi Lon.xlsx`.
> - `SprintA_AI_Document_Test.txt`.

## Sự thật kiểm thử hiện tại

| Nguồn | Dữ liệu đọc được | Cách hiểu đúng |
|---|---:|---|
| Test 1.000 dòng | 1.000 reported pass, nhưng chỉ **47 kịch bản gốc** | Dùng làm catalog; chưa phải 1.000 bằng chứng độc lập |
| Test 600 dòng | 555 reported pass, 36 fail, 9 dòng lệch cột | 36 fail là backlog bắt buộc; pass cần evidence runtime/automation |
| AI Document Test | Acceptance cho Report, Export, Pages và AI TXT/DOCX/PDF | Chuyển thành E2E scenario chính thức |

## Thứ tự đọc và thi hành

1. `SPRINTA_MASTER_CONTEXT.md`
2. `SPRINTA_PROJECT_MEMORY.md`
3. `SPRINTA_TEST_TRUTH_MATRIX.md`
4. `SPRINTA_MASTER_BACKLOG.md`
5. `SPRINTA_P0_FIX_PLAN.md`
6. `SPRINTA_EXECUTION_PROMPTS.md`
7. File chuyên môn của phase đang làm.

## Các file mới

- `SPRINTA_CURRENT_REPO_AUDIT.md`

- `SPRINTA_TEST_TRUTH_MATRIX.md`
- `SPRINTA_MASTER_BACKLOG.md`
- `SPRINTA_P0_FIX_PLAN.md`
- `SPRINTA_CORE_STABILIZATION_PLAN.md`
- `SPRINTA_AI_CREDITS_BILLING_SPEC.md`
- `SPRINTA_ADVANCED_FEATURE_ROADMAP.md`
- `SPRINTA_OPEN_SOURCE_USAGE_GUIDE.md`
- `SPRINTA_EXECUTION_PROMPTS.md`
- `SPRINTA_RELEASE_ACCEPTANCE_CHECKLIST.md`

## Quy tắc điều hành

```text
P0 Security/Data Integrity
→ P1 Test/Architecture/Core UX
→ AI Credits & Billing
→ P2 Product Completion
→ P3 Enterprise/Advanced
```

Không phát triển thêm feature nâng cao nếu P0 release gate chưa pass.
