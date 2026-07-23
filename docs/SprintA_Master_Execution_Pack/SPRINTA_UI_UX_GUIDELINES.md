# SPRINTA UI/UX GUIDELINES

> Phong cách: Premium Calm Futuristic SaaS  
> Cập nhật: 17/07/2026  
> Mục tiêu: hiện đại, dễ dùng, mật độ phù hợp phần mềm quản lý công việc và không làm mất nghiệp vụ.

---

## 1. Nguyên tắc nền

1. Function before decoration.
2. Data before motion.
3. Existing product before reference.
4. Small scoped redesign before system-wide redesign.
5. Real state before optimistic success.
6. Accessibility và responsive là một phần của Done.

---

## 2. Thiết lập

```text
DESIGN_VARIANCE = 4/10
MOTION_INTENSITY = 4/10
VISUAL_DENSITY = 7/10
```

- Không phá bố cục app đang chạy.
- Motion vừa phải.
- Dashboard mật độ cao nhưng có hierarchy.
- Không biến ứng dụng thành landing template.

---

## 3. Visual Language

### Màu

- Navy/charcoal.
- Cyan accent.
- Semantic success/warning/error/info nhất quán.
- Ít gradient.
- Glow chỉ cho focus/CTA/AI state quan trọng.
- Dark mode không phải mọi thứ đen và phát sáng.

### Typography

- Heading phân cấp.
- Body dễ đọc.
- Label không quá mờ.
- KPI dùng tabular number nếu phù hợp.
- Không text animation trong bảng/form.
- Line-height phù hợp tiếng Việt.
- Tránh all-caps dài.

### Spacing

- Dùng token.
- Khoảng cách phản ánh nhóm.
- Không tăng padding vô lý làm mất mật độ.
- Form label/input/action nhất quán.
- Drawer/detail pane có header/footer cố định khi cần.

### Border/radius/shadow

- Radius nhất quán.
- Border nhẹ.
- Shadow vừa.
- Không glass blur trên bảng dài.
- Focus ring rõ.

---

## 4. Foundation Components

Trước component mới:

1. Tìm Foundation hiện có.
2. Kiểm tra props/variant.
3. Dark mode.
4. i18n.
5. Responsive.
6. Chỉ tạo khi không có primitive phù hợp.

Nhóm cần nhất quán:

- Button/IconButton.
- Input/Select/Date wrapper.
- AppAvatar.
- Badge/Status.
- Card.
- Modal/Drawer.
- Empty/Loading/Error.
- Confirm Dialog.
- File Card.
- Context Chip.
- Action Preview.
- Toast.
- Page Header.
- Filter Bar.
- Table/List/Split Pane.

Không tạo nhiều bản CSS khác nhau cho cùng một primitive.

---

## 5. Nguồn tham khảo

### Mobbin

Dùng cho Landing, Features, How It Works, Showcase, Integrations, Pricing, FAQ, Login/Register, Site Selection, Empty State, Settings, AI/Notes Drawer.

Quy tắc:

- Tối đa khoảng 3 reference đúng loại màn hình.
- Chỉ rút hierarchy, section order, spacing, typography, CTA, layout, responsive, micro-interaction.
- Không copy logo/content/image/brand.
- Không áp marketing layout vào dashboard.

### Taste

Dùng như workflow audit existing project, không phải giấy phép redesign tự do.

### React Bits/Vue Bits

Có thể dùng ý tưởng:

- Text reveal landing.
- KPI count-up.
- Spotlight nhẹ.
- Animated list.
- Shimmer.
- CTA hover có kiểm soát.

Không copy JSX/hooks, không particle nặng trong task table.

### GSAP

Phù hợp với landing, showcase, KPI một lần, FLIP filter/card, command palette, widget rearrangement.

Không dùng cho mọi button, row, input, animation vô hạn. Bắt buộc reduced-motion và cleanup.

### 21st/Hallmark

Dùng composition/polish component, không copy React/Tailwind nguyên khối, không redesign toàn app.

---

## 6. Marketing vs Application

### Landing

```text
Header
-> Hero
-> Social Proof
-> Features
-> How It Works
-> Product Showcase
-> Integrations
-> Pricing
-> FAQ
-> Final CTA
-> Footer
```

Được storytelling, motion, product visual và CTA.

### Dashboard/Application

Ưu tiên Task, KPI, Progress, Filter, Action, Table/List/Board, Context, Notification.

Không đưa Pricing, Social Proof, hero lớn, decorative section hoặc motion làm khó đọc.

---

## 7. Application Shell

- Header/sidebar/project nav có behavior rõ.
- Một scroll container chính khi có thể.
- Không body và pane cùng scroll ngoài chủ ý.
- Split pane có min/max width.
- Drawer không đẩy content sai.
- Sticky header không che content.

Dự án từng có NexusLayout và HomeSiteLayout. Trước khi chỉnh:

- Map route.
- Chọn shell chính.
- Không sửa cả hai ngẫu nhiên.
- Không tạo thêm profile/dashboard song song.
- Shared nav/avatar dùng cùng source.

Z-index tham chiếu:

```text
base              0-10
sticky header     20
popover           30
utility rail      40
drawer            50
modal             60
toast             70
critical overlay  80
```

Ưu tiên scale hiện có thay vì hard-code rải rác.

---

## 8. Global Utility Rail

Desktop:

```text
Main content                         [AI]
                                     [NOTE]
```

- Có ở mọi route phù hợp.
- Tooltip/accessibility label.
- Active state rõ.
- AI và Notes không mở cùng lúc.
- Drawer khoảng 360-440px.
- Focus vào drawer khi mở.
- Escape đóng nếu an toàn.
- Không che action quan trọng.

Mobile:

- Floating chips/bottom navigation action.
- Drawer thành bottom sheet/full screen.
- Respect safe area.
- Không che nội dung.

---

## 9. AI Drawer

Phải có:

- Conversation title/context.
- History/new chat.
- Message list/streaming.
- Composer.
- Attachment preview.
- Context chips.
- Action Preview.
- Confirm/Cancel.
- Result/link.
- Error/retry.

Không:

- Giấu Project đích.
- Biến Preview thành toast.
- Mất Action Timeline sau refresh.
- Cho tool chạy chỉ vì user nhập text.

---

## 10. Notes và Floating Stickies

### Notes Drawer

```text
Ghi chú nhanh
[+ Ghi chú mới] [Tìm kiếm]

Pinned
- Note A

Gần đây
- Note B

[Mở toàn bộ ghi chú]
```

- Autosave debounce.
- Save state rõ.
- Search/pin/color/label.
- Link Workspace/Project/Task/Goal/route.
- API persistence.
- Empty/error/loading.
- Không localStorage làm nguồn chính.

### Floating Card

```text
+-----------------------------+
| drag  Tên note       -   x  |
+-----------------------------+
| Nội dung...                 |
| Project - cập nhật 17:21    |
+-----------------------------+
```

- Drag từ handle.
- Không kéo khi select/edit.
- Clamp viewport.
- Focus tăng z-index.
- Close không delete.
- Delete có confirm.
- Scope route/project/global.
- F5 còn vị trí.
- Số note tối đa hợp lý.

---

## 11. Integration Hub UI

Có thể gồm:

- Provider connections.
- Sync status.
- Unified Inbox list.
- Detail pane.
- Filter/search.
- Task Candidate.
- Create Task.

Rules:

- List/detail không bị cắt.
- Pane scroll độc lập rõ.
- Mobile detail full-screen.
- Provider status nhất quán.
- Sync error có lý do.
- Skeleton/empty/error.
- Không sync toàn bộ khi mở trang.
- Create Task phải hiện Project đích.

Success:

```text
Đã tạo SPRINT-142
Review báo cáo tuần 28
Project: SprintA Platform
Status: To do

[Mở task] [Mở Work Items]
```

---

## 12. Form UX

- Label rõ.
- Required marker.
- Inline validation.
- Server error cụ thể.
- Submit loading/disabled.
- Double-click safe.
- Dirty close warning.
- Date/time/timezone rõ.
- Member/project từ dữ liệu thật.
- Không hard-code option.

---

## 13. Loading, Empty, Error

### Loading

- Skeleton theo cấu trúc.
- Không spinner toàn trang cho action nhỏ.
- Button loading.
- Không flash fake data.

### Empty

Nói rõ chưa có dữ liệu, nguyên nhân và action tiếp theo. Không dùng marketing copy dài.

### Error

- Lý do dễ hiểu.
- Retry khi an toàn.
- Permission denied khác server error.
- Không success sau error.
- Không nuốt network error.

---

## 14. Status Badge

- REAL: không cần badge.
- Beta: API thật nhưng chưa đủ.
- Coming soon: action chưa backend.
- Local only: chỉ thiết bị.
- Syncing/Failed/Disconnected.
- Draft/Preview/Executing/Succeeded/Failed cho AI.

Không gọi logic rule-based là AI nếu không dùng model.

---

## 15. Responsive

Test tối thiểu:

- 390px.
- Tablet.
- Laptop.
- Desktop rộng.

Kiểm tra sidebar, header, table scroll, drawer, split pane, modal, form, stickies, text tiếng Việt dài, mobile keyboard và safe area.

Không chỉ scale desktop; phải đổi interaction khi thiếu không gian.

---

## 16. Accessibility

- Semantic button.
- Accessible name cho icon.
- Keyboard.
- Focus visible/trap.
- Escape behavior.
- Contrast.
- Reduced motion.
- Không chỉ dùng màu.
- Screen reader text.
- Drag không cản keyboard flow.

---

## 17. Motion

Motion phải giúp giữ context, báo thay đổi, hướng focus hoặc phản hồi thao tác.

Không:

- Infinite shimmer.
- Parallax dashboard.
- Card floating liên tục.
- Text reveal data table.
- Layout shift.

---

## 18. Workflow bắt buộc

Trước code:

1. Trang hiện có gì.
2. Chức năng phải khóa.
3. Vấn đề UX cụ thể.
4. Tối đa 3 reference.
5. Nguyên tắc áp dụng.
6. File dự kiến sửa.
7. Acceptance criteria.

Sau code:

- Build.
- Browser.
- Light/dark.
- Responsive.
- Keyboard/focus.
- Network/error.
- Diff review.

---

## 19. Anti-patterns

- Redesign nhiều trang một PR.
- Copy marketing template.
- Fake data.
- Nút không flow.
- Fake success.
- Overlay che content.
- Z-index ngẫu nhiên.
- Scroll container hỗn loạn.
- Animation vô hạn.
- Drawer mất action state.
- Dark mode chỉ invert.
- Table thiếu loading/empty/error.
- Action không hiện destination.
- Xóa route vì chưa hoàn thiện.

# 20. Pricing and Billing UI

- Package lấy từ API, không hard-code.
- Một package Recommended.
- Balance, usage và estimated Credits là data-first.
- Checkout minimal motion.
- Success UI chỉ sau backend order `CREDITED`.
- Mobile card stack.
- Vue Bits/Motion chỉ dùng count-up/selection feedback nhẹ.
