# SPRINTA CURRENT REPOSITORY AUDIT SNAPSHOT

> Snapshot công khai kiểm tra ngày 17/07/2026.
>
> Đây là static audit; runtime/build/database vẫn phải xác minh bằng Phase P0-00.

---

## 1. Repository

- Repository: `https://github.com/DinhTuanKhoiTB01696/QuanLyCongViec`
- Branch được xem: `main`
- GitHub hiển thị khoảng 309 commits tại thời điểm audit.
- Ngôn ngữ chính: Vue và C#, kèm JavaScript/TSQL/CSS/Python.
- Có thư mục:
  - `.github/workflows`
  - `Backend`
  - `Frontend`
  - `docs`
  - `scripts`
  - `AGENTS.md`

Nguồn:
- `https://github.com/DinhTuanKhoiTB01696/QuanLyCongViec`

---

## 2. AGENTS.md đang có guardrail tốt

Các rule hiện có:

- UI task chỉ sửa frontend.
- Không tự đổi backend/API contract.
- Không mock/hard-code.
- Không thay component API thật bằng visual mock.
- Giữ Vue 3 + Element Plus + Pinia + Tailwind.
- Không thêm UI framework lớn.
- Modern SaaS productivity style.
- Dùng design tokens.
- Trước/sau sửa kiểm tra Git.
- Báo file, UI change, API preserved, backend change, build và test.

Execution prompts trong pack này giữ nguyên và mở rộng các rule đó.

---

## 3. Frontend package snapshot

`Frontend/package.json` hiện chỉ có scripts:

```json
{
  "dev": "...",
  "build": "vite build",
  "preview": "vite preview"
}
```

Thiếu script chính thức:

- lint.
- typecheck.
- unit test.
- E2E.
- quality check.

Dependency hiện có gồm:

- Vue 3/Pinia/Vue Router.
- Element Plus.
- TipTap.
- DOMPurify.
- SignalR.
- motion.
- vuedraggable.
- ApexCharts.
- Chart.js.
- ECharts.

Kết luận:

- Thêm frontend quality gate là P1.
- Chọn ECharts làm chart stack mục tiêu.
- Không gỡ chart package cũ trước khi migrate/grep.

Nguồn:
- `https://raw.githubusercontent.com/DinhTuanKhoiTB01696/QuanLyCongViec/main/Frontend/package.json`

---

## 4. GitHub Actions snapshot

`.github/workflows` hiện hiển thị một workflow:

- `dotnet-ci.yml`

Chưa thấy workflow frontend riêng trong snapshot.

Kết luận:

- Thêm frontend CI.
- Thêm security/dependency scan.
- Không tuyên bố frontend tests chạy CI trước khi workflow tồn tại.

Nguồn:
- `https://github.com/DinhTuanKhoiTB01696/QuanLyCongViec/tree/main/.github/workflows`

---

## 5. Program.cs risk snapshot

Source công khai hiện cho thấy:

- Add rate limiter policy.
- Development có thể fallback InMemory nếu SQL không kết nối.
- HTTPS redirect đang comment/tắt.
- Serve static `/uploads`.
- Startup tự tạo migration history guard.
- Catch migration exception rồi tiếp tục.
- Chạy raw SQL tạo/sửa table/column/index.
- Seed mock data khi startup.
- Catch migrate/seed exception và tiếp tục chạy.

Kết luận:

- Đây là P0 Hosting/Data Safety.
- Không sửa bằng cách thêm raw SQL mới.
- Migration cần trở thành source of truth/deployment step.
- Upload private cần authorization.

Nguồn:
- `https://raw.githubusercontent.com/DinhTuanKhoiTB01696/QuanLyCongViec/main/Backend/src/TaskManagement.API/Program.cs`

---

## 6. Configuration snapshot

Current `appsettings.json` trên `main` dùng placeholder cho JWT/Gemini/GitHub/Resend và empty OAuth secrets. Đây là tốt hơn việc commit secret thật.

Vẫn cần:

- Secret scan toàn Git history.
- Environment/User Secrets.
- Không dùng local SQL machine name ở deployment.
- Config validation/fail fast.
- Rotate key nếu bất kỳ secret thật từng được commit.

Nguồn:
- `https://raw.githubusercontent.com/DinhTuanKhoiTB01696/QuanLyCongViec/main/Backend/src/TaskManagement.API/appsettings.json`

---

## 7. Current priority conclusion

Dự án không thiếu breadth. Rủi ro là:

```text
Feature breadth
> automated evidence
> deployment safety
> consistent authorization
> data integrity guarantees
```

Thứ tự đúng:

1. Reproduce test truth.
2. Security/permission.
3. Data integrity/concurrency.
4. Hosting/migration/upload.
5. CI/test.
6. Core UX/architecture.
7. AI Credits/Billing.
8. Advanced feature.

---

## 8. Assumptions cần runtime verify

- Backend build hiện tại.
- Migration apply trên DB sạch và DB hiện hữu.
- Route nào đang gọi component nào.
- Controller/action permission coverage.
- AI provider behavior.
- Integration secret/OAuth.
- Upload ownership.
- Payment code có tồn tại hay chỉ Pricing controller.
- Feature Pass trong Excel có chạy trên commit nào.

Không được biến assumption thành claim “đã hoàn thiện”.
