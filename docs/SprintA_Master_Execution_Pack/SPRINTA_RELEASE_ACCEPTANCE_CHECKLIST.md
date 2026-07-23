# SPRINTA RELEASE ACCEPTANCE CHECKLIST

> Dùng trước merge vào release branch và trước demo/deploy.

---

## A. Scope và Git

- [ ] Task ID và phase rõ.
- [ ] Đọc đúng tài liệu phase.
- [ ] `git status --short` đã kiểm tra.
- [ ] Không có file ngoài scope.
- [ ] Không `git add .`.
- [ ] Không `git reset --hard`.
- [ ] Diff nhỏ và review được.
- [ ] Không secret/log/build output.

## B. Build và Test

- [ ] Frontend install/build.
- [ ] Frontend lint.
- [ ] Frontend typecheck.
- [ ] Frontend unit/component.
- [ ] Playwright critical smoke.
- [ ] Backend build.
- [ ] Backend unit.
- [ ] SQL integration.
- [ ] Migration smoke.
- [ ] Secret/dependency scan.

## C. Security

- [ ] Authentication active/deleted user.
- [ ] WorkspaceMember.IsActive.
- [ ] Backend permission.
- [ ] No system-role bypass ngoài policy.
- [ ] Input validation.
- [ ] Rate limit.
- [ ] Upload authorization.
- [ ] No secret sent to AI/log.
- [ ] Prompt injection test.
- [ ] Payment signature/idempotency nếu liên quan.

## D. Data Integrity

- [ ] Transaction.
- [ ] Unique constraint.
- [ ] Idempotency.
- [ ] RowVersion/concurrency.
- [ ] Soft-delete/history.
- [ ] Cross-project/workspace validation.
- [ ] Reload persistence.
- [ ] Double-click/retry.
- [ ] Rollback path.

## E. API

- [ ] 400 validation.
- [ ] 401 auth.
- [ ] 403 permission.
- [ ] 404 scoped not found.
- [ ] 409 duplicate/concurrency.
- [ ] 429 rate limit.
- [ ] ProblemDetails.
- [ ] Correlation ID.
- [ ] Pagination nếu list lớn.

## F. UI/UX

- [ ] Chức năng cũ giữ nguyên.
- [ ] Không fake data/success.
- [ ] Loading.
- [ ] Empty.
- [ ] Error/retry.
- [ ] Disabled/Coming soon rõ.
- [ ] Light mode.
- [ ] Dark mode.
- [ ] 390px.
- [ ] Tablet/laptop.
- [ ] Keyboard/focus.
- [ ] Reduced motion.
- [ ] Text tiếng Việt dài.
- [ ] No raw JSON/[object Object].

## G. AI

- [ ] Context hiện rõ.
- [ ] Project destination rõ.
- [ ] Permission backend.
- [ ] Preview.
- [ ] Cancel 0 mutation.
- [ ] Confirm đúng mutation.
- [ ] Retry no duplicate.
- [ ] Action state sau F5.
- [ ] Citation/source.
- [ ] Provider timeout/429.
- [ ] Credit estimate.
- [ ] Không trừ credit khi thất bại.

## H. Billing

- [ ] Package snapshot server-side.
- [ ] Không tin price từ frontend.
- [ ] Signature verify.
- [ ] Duplicate webhook no-op.
- [ ] Return/webhook order independent.
- [ ] Ledger equals wallet.
- [ ] No negative.
- [ ] Refund/reconciliation.
- [ ] Invoice/history.
- [ ] Gamification points tách AI Credits.

## I. Evidence

- [ ] Commit SHA.
- [ ] Build log.
- [ ] Test report.
- [ ] Screenshot/video khi UI.
- [ ] Request/response.
- [ ] Database evidence.
- [ ] Defect ID closed.
- [ ] A–E report.

---

## Release Stop Conditions

Dừng release nếu có:

- Auth/permission bypass.
- Cross-workspace data leak.
- Migration fail.
- Duplicate mutation/payment.
- Data loss/history hard-delete.
- Private upload public.
- AI auto mutation không confirm.
- Wallet/ledger lệch.
- Build/critical smoke fail.
