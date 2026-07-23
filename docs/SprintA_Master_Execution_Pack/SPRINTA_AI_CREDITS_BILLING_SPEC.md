# SPRINTA AI CREDITS & BILLING SPEC

> Tên hiển thị với người dùng: **SprintA AI Credits**.
>
> Không gọi là “Gemini token” hoặc bán token kỹ thuật trực tiếp.

---

## 1. Mục tiêu

- Người dùng biết số dư AI.
- Biết ước tính chi phí trước tác vụ lớn.
- Mua thêm Credits bằng VND.
- Không trừ sai khi provider lỗi/retry.
- Tách hoàn toàn Credits mua bằng tiền khỏi điểm Gamification.
- Admin thay đổi package, giá model và conversion mà không deploy frontend.

---

## 2. Ba loại tài sản không được trộn

| Loại | Mục đích | Có mua bằng tiền? | Có hết hạn? |
|---|---|---:|---:|
| Gamification Points | Badge/reward nội bộ | Không | Theo policy |
| Promotional AI Credits | Free/monthly/coupon | Không trực tiếp | Có thể |
| Purchased AI Credits | Sử dụng AI trả phí | Có | Nên không hết hạn hoặc policy minh bạch |

Ledger phải ghi nguồn credit được tiêu theo thứ tự cấu hình, ví dụ promotional hết hạn trước rồi purchased.

---

## 3. Pricing principle

Giá provider thay đổi theo:

- Model.
- Input/output.
- Audio/image/video.
- Context cache.
- Grounding/search.
- Batch/priority.
- Tỷ giá USD/VND.

Vì vậy không hard-code “1 credit = x token” trong frontend.

### Cost engine

```text
Provider Usage
× Provider Unit Price
× FX Rate
× Safety Margin
+ Storage/Payment/Retry Overhead
= Internal Cost
→ Convert to Credits
```

### Model routing đề xuất

| Tác vụ | Model tier |
|---|---|
| Intent, classification, short summary | Economy/Flash-Lite |
| Chat, Task planning, document extraction | Standard Flash |
| Complex analysis/high-value report | Pro/advanced theo quyền |
| Embedding/RAG | Embedding model riêng |
| Voice/image | Multimodal tier, estimate trước |

Model name và price phải nằm DB/config. Current app config dùng `gemini-2.5-flash`, nhưng billing không được phụ thuộc cứng vào model này.

---

## 4. Gói giá MVP đề xuất

> Đây là **giá thử nghiệm sản phẩm**, Admin được chỉnh. Trước production phải chạy cost simulation.

| Package | Giá VND | Credits | Bonus | Target |
|---|---:|---:|---:|---|
| Free Monthly | 0 | 100/tháng | 0 | Dùng thử |
| Mini Top-up | 20.000 | 250 | 0 | Học sinh/cá nhân ít dùng |
| Starter | 49.000 | 700 | 50 | Cá nhân |
| Plus | 99.000 | 1.600 | 200 | Dùng thường xuyên |
| Pro | 199.000 | 4.000 | 700 | File, report, AI tools |
| Team | 499.000 | 12.000 shared | 3.000 | Nhóm nhỏ |
| Business | 999.000 | 27.000 shared | 8.000 | Doanh nghiệp nhỏ |
| Enterprise | Liên hệ | Metered/contract | Theo SLA | Enterprise |

### Credit estimate khởi đầu

| Tác vụ | Estimated Credits |
|---|---:|
| Chat ngắn economy | 1 |
| Tóm tắt Task/Project | 1–2 |
| Lập kế hoạch | 2–5 |
| AI tool mutation | 2–6 |
| Phân tích ảnh | 3–12 |
| TXT/DOCX/PDF | Theo token/page, hiển thị estimate |
| Grounding/search | Base + external query surcharge |
| Voice | Theo phút/token |

Không dùng bảng này để trừ cố định. Actual debit dựa provider usage, nhưng UX có thể hiển thị range.

---

## 5. UX Information Architecture

### Sidebar/Header

- Credit balance badge.
- Low balance warning.
- Link “Mua Credits”.
- Không hiển thị quá gây phiền.

### Billing area

```text
AI Credits Overview
Current Balance
Monthly Free Credits
Purchased Credits
Usage This Month
Estimated Days Remaining

[Buy Credits]
[Usage History]
[Invoices]
[Spending Limits]
```

### Pricing page

- Monthly plans và one-time top-up tách rõ.
- Nêu Credits, nhóm dùng chung, feature limits.
- Calculator “Tôi thường dùng…”.
- FAQ Credits có hết hạn không.
- Không quảng cáo “unlimited” nếu có fair-use limit.

### Composer

Trước tác vụ lớn:

```text
Ước tính 18–25 Credits
Số dư: 320
Model: Standard
[Tiếp tục] [Chọn chế độ tiết kiệm]
```

---

## 6. Domain Model

### `AiCreditWallet`

```text
Id
OwnerType: User | Workspace
OwnerId
AvailablePurchased
AvailablePromotional
Reserved
CurrencyVersion
RowVersion
CreatedAt
UpdatedAt
```

### `AiCreditLedger`

```text
Id
WalletId
Type: Grant | Purchase | Reserve | Debit | Release | Refund | Expire | Adjustment
Amount
BalanceAfter
ReferenceType
ReferenceId
UsageId?
PaymentTransactionId?
IdempotencyKey
Description
CreatedBy?
CreatedAt
```

### `AiUsageRecord`

```text
Id
UserId
WorkspaceId?
ConversationId?
ActionId?
Provider
Model
InputTokens
OutputTokens
CachedTokens
AudioSeconds?
ImageCount?
GroundingQueries?
ProviderCostUsd
FxRate
InternalCostVnd
CreditsEstimated
CreditsDebited
Status
StartedAt
CompletedAt?
ErrorCode?
```

### `AiCreditPackage`

```text
Id
Code
Name
PriceVnd
BaseCredits
BonusCredits
OwnerScope
IsRecurring
DurationDays?
IsActive
SortOrder
EffectiveFrom
EffectiveTo?
```

### Payment

```text
PaymentOrder
PaymentTransaction
PaymentWebhookEvent
RefundRequest
Invoice
Coupon
Subscription
```

---

## 7. Credit Reservation State Machine

```text
ESTIMATE
→ RESERVE
→ PROVIDER_CALL
→ FINALIZE_ACTUAL
→ RELEASE_UNUSED
```

Error:

```text
VALIDATION_FAIL → no reserve
PROVIDER_FAIL_BEFORE_USAGE → release all
PARTIAL_PROVIDER_USAGE → finalize measured amount
TIMEOUT_UNKNOWN → reconcile job
DUPLICATE_CALLBACK → idempotent no-op
```

### Rules

- Không cho wallet âm.
- Reserve trong transaction có RowVersion.
- Retry action dùng cùng usage/idempotency reference.
- Provider response usage là source actual.
- Nếu provider không trả usage, dùng tokenizer estimator có flag `Estimated`.
- Refund/adjustment chỉ qua ledger, không sửa balance trực tiếp.

---

## 8. Payment Gateway Architecture

```text
IPaymentGateway
  CreateOrder
  VerifyReturn
  VerifyWebhook
  QueryTransaction
  Refund
```

### Thứ tự triển khai

1. VNPAY sandbox.
2. MoMo.
3. ZaloPay.

Lý do chọn VNPAY đầu tiên:

- Có tài liệu sandbox và code mẫu C#.
- Có API query và refund.
- Phù hợp one-time top-up.

Gateway phải là adapter; domain billing không biết chi tiết `vnp_`, MoMo hoặc ZaloPay.

---

## 9. Payment State Machine

```text
CREATED
→ PENDING
→ PAID
→ CREDITED
```

Các nhánh:

```text
FAILED
CANCELLED
EXPIRED
PENDING_RECONCILIATION
REFUND_PENDING
REFUNDED
CHARGEBACK
```

### Luồng

1. User chọn package.
2. Backend snapshot package price/credits.
3. Tạo `PaymentOrder`.
4. Gateway trả payment URL.
5. Redirect user.
6. Gateway webhook/return.
7. Verify signature.
8. Idempotency.
9. Query gateway nếu cần.
10. Transaction:
    - mark Paid;
    - create PaymentTransaction;
    - credit ledger;
    - mark Credited.
11. Tạo receipt/invoice.
12. SignalR/poll update UI.

Không cộng credit chỉ từ frontend return URL.

---

## 10. API đề xuất

### Public/User

```http
GET  /api/ai-billing/packages
GET  /api/ai-billing/wallet
GET  /api/ai-billing/usage
GET  /api/ai-billing/ledger
POST /api/ai-billing/orders
GET  /api/ai-billing/orders/{id}
POST /api/ai-billing/orders/{id}/cancel
GET  /api/ai-billing/invoices
POST /api/ai-billing/estimate
```

### Gateway

```http
GET/POST /api/payments/vnpay/return
POST     /api/payments/vnpay/webhook
POST     /api/payments/vnpay/query
POST     /api/payments/vnpay/refund
```

### Admin

```http
CRUD /api/admin/ai-credit-packages
CRUD /api/admin/model-prices
GET  /api/admin/payments
GET  /api/admin/reconciliation
POST /api/admin/wallet-adjustments
POST /api/admin/refunds
```

Admin adjustment bắt buộc reason + permission + audit.

---

## 11. Pricing Admin

Admin quản lý:

- Provider/model.
- Effective date.
- USD price per unit.
- FX rate.
- Margin.
- Minimum charge.
- Package price/credit.
- Monthly free credit.
- Workspace spending limit.
- Low-balance threshold.

Price snapshot phải được lưu vào Usage/Order để lịch sử không thay đổi khi Admin cập nhật giá.

---

## 12. Security

- Gateway secret trong environment/secret manager.
- Verify HMAC/signature.
- Idempotency request/order/webhook.
- Amount/package snapshot server-side.
- Không tin price/credits từ frontend.
- Webhook log sanitized.
- Rate limit checkout.
- CSRF/state protection phù hợp redirect.
- Reconciliation job.
- Refund permission.
- PII retention tối thiểu.
- Không lưu card data.

---

## 13. Test Plan

### Wallet

- Concurrent reserve.
- Retry.
- Release.
- Promotional expiry.
- Purchased balance.
- No negative.
- Ledger sum equals wallet.

### Payment

- Valid paid webhook.
- Invalid signature.
- Duplicate webhook.
- Return trước webhook.
- Webhook trước return.
- Timeout.
- Query reconciliation.
- Refund.
- Package changed after order.
- Amount tampering.

### AI usage

- Provider success.
- 429.
- Timeout.
- Partial usage.
- Cancel before call.
- Duplicate action.
- Team wallet permission.
- Spending limit.

---

## 14. UI Design

Phong cách:

- Premium Calm Futuristic SaaS.
- Pricing card sạch, không glow quá mạnh.
- “Recommended” chỉ một gói.
- So sánh dễ đọc.
- Balance/usage là data-first.
- Checkout modal không có animation gây phân tâm.
- Mobile pricing ngang thành stack.
- Dark/light đầy đủ.

Dùng Element Plus/Foundation; Vue Bits/Motion chỉ cho count-up nhẹ, success transition và package selection feedback.

---

## 15. Rollout

### Phase B0

- Wallet/ledger/usage schema.
- Không payment.
- Free credits + admin grant.

### Phase B1

- Reservation/finalization.
- Usage page.
- Cost estimate.

### Phase B2

- Package/Order.
- VNPAY sandbox.
- Webhook/query/reconciliation.

### Phase B3

- Pricing/checkout/invoice/admin.
- Production merchant onboarding.

### Phase B4

- Team wallet/subscription.
- MoMo/ZaloPay.
- Coupon/refund automation.

---

## 16. Sources to re-check before production

- Gemini pricing: `https://ai.google.dev/gemini-api/docs/pricing`
- VNPAY sandbox docs: `https://sandbox.vnpayment.vn/apis/`
- MoMo developer docs: `https://developers.momo.vn/`
- ZaloPay docs: `https://docs.zalopay.vn/`

Pricing and gateway contracts must be revalidated at implementation time.
