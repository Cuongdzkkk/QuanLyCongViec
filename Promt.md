
# TASK: Đồng bộ toàn bộ UI/UX của 3 trang Goal Detail, User Profile Detail và Team Detail

Tôi muốn bạn **review và refactor giao diện frontend của 3 trang Detail hiện tại** để chúng trở thành một hệ thống giao diện thống nhất, chuyên nghiệp và có cùng visual language.

3 trang cần đồng bộ:

1. **Goal Detail**
2. **User Profile Detail**
3. **Team Detail**

Hãy xem 3 screenshot tôi cung cấp làm reference trực tiếp.

Mục tiêu không phải copy cứng toàn bộ nội dung giữa 3 trang, mà là:

> **Giữ nguyên chức năng và dữ liệu riêng của từng trang, nhưng chuẩn hóa layout, spacing, background header, avatar, action area, typography và interaction pattern để cả 3 trang nhìn như cùng thuộc một design system.**

---

## 1. NGUYÊN TẮC QUAN TRỌNG

Trước khi chỉnh code:

* Đọc kỹ code hiện tại của cả 3 trang.
* Tìm các component dùng chung liên quan đến:

  * Header
  * Avatar
  * Cover / Background
  * Tabs
  * Action buttons
  * Overflow menu
  * Content container
  * Right sidebar
  * Notes
  * Breadcrumb / Back button
* Không tạo 3 cách implement khác nhau cho cùng một UI pattern nếu có thể dùng component dùng chung.
* Ưu tiên **reuse shared component / shared CSS / design tokens**.
* Không phá vỡ logic backend/API hiện tại.
* Không làm mất các chức năng đang hoạt động.
* Không thay đổi business logic nếu không cần thiết.
* Chỉ refactor UI/UX, layout và component architecture khi cần.
* Không hard-code dữ liệu chỉ để làm giao diện đẹp hơn.
* Phải đảm bảo responsive tốt.

Mục tiêu cuối cùng:

> Goal Detail, User Profile Detail và Team Detail phải nhìn như 3 biến thể của cùng một hệ thống Detail Page.

---

# 2. CHUẨN HÓA HERO / COVER BACKGROUND

Hiện tại 3 trang đang có phần background phía trên khác nhau.

Hãy tạo một chuẩn chung cho cả 3.

## Yêu cầu

Phần cover/background trên cả 3 trang phải:

* Có **cùng chiều cao**.
* Có cùng khoảng cách tới content bên dưới.
* Có cùng cách xử lý background.
* Có cùng border-radius / clipping behavior nếu design hiện tại sử dụng.
* Có cùng vị trí bắt đầu và kết thúc.
* Không được để Goal cao hơn Team hoặc User thấp hơn Goal.

Tạo một shared component nếu phù hợp, ví dụ:

`DetailHero`
hoặc
`ProfileHero`

Component này có thể nhận:

* `type`
* `background`
* `avatar`
* `title`
* `actions`
* `editable`
* `fallback`

Nhưng không bắt buộc phải dùng đúng tên này. Hãy dùng architecture phù hợp với project hiện tại.

---

# 3. CHUẨN HÓA AVATAR

Avatar của cả 3 trang phải có:

* Cùng kích thước.
* Cùng tỷ lệ.
* Cùng vị trí.
* Cùng khoảng cách với background.
* Cùng cách overlap giữa cover và content.
* Cùng border treatment.
* Cùng visual weight.

## Goal

Avatar Goal hiện tại có style khá rõ ràng.

Hãy lấy style của Goal làm chuẩn:

* Size tương ứng.
* Border.
* Radius.
* Shadow nếu đang có.
* Vị trí overlap.

## Team

Team KHÔNG sử dụng avatar dạng chữ "BA" như hiện tại nữa.

Team cần dùng **icon folder/reference icon giống hình ảnh tôi cung cấp ở ảnh 4**.

Tức là:

* Team avatar = icon.
* Icon nằm chính giữa.
* Kích thước icon phải cân đối với avatar Goal.
* Background của avatar Team phải phù hợp với design system.
* Không làm icon quá lớn.
* Không làm icon quá nhỏ.

Mục tiêu là khi nhìn Goal và Team cạnh nhau, avatar phải có cùng visual weight.

---

# 4. DEFAULT BACKGROUND CHO GOAL VÀ TEAM

Tôi muốn nâng cấp background mặc định.

Tham khảo pattern background ở **ảnh 5**.

Ảnh 5 có phong cách:

* Pattern lặp.
* Các icon nhỏ phân bố đều.
* Có khoảng trắng.
* Có cảm giác nhẹ nhàng, playful.
* Không quá dày.
* Không gây khó đọc text phía trên.

Hãy tạo background mặc định theo nguyên tắc này.

## Goal

Goal có background mặc định dựa trên chính concept Goal.

Tức là:

* Dùng icon / motif liên quan đến Goal.
* Pattern lặp đều.
* Các icon có kích thước nhỏ.
* Không đặt quá sát nhau.
* Phân bố tự nhiên nhưng có kiểm soát.
* Không làm background quá rối.
* Không ảnh hưởng tới readability.

## Team

Team dùng background mặc định dựa trên icon Team.

Tức là:

* Lấy icon Team ở ảnh 4 làm source visual.
* Tạo pattern lặp đều.
* Các icon nhỏ phân bố trên toàn background.
* Giữ cùng density và style với Goal.
* Không làm pattern của Team khác phong cách Goal.

### Quan trọng

Goal và Team phải có:

* cùng chiều cao cover
* cùng density pattern
* cùng opacity
* cùng cách scale
* cùng spacing
* cùng visual treatment

Chỉ khác **loại icon/pattern**.

---

# 5. USER PROFILE BACKGROUND

User Profile phải có cơ chế background riêng vì user có thể thay đổi hình ảnh.

Ưu tiên logic:

### Nếu user có custom background

Hiển thị background của user.

### Nếu user chưa có background

Không để một vùng trống hoặc màu mặc định xấu.

Hãy tạo một fallback background:

* màu tối
* hiện đại
* subtle gradient hoặc subtle texture
* không quá nổi
* phù hợp với avatar và title
* nhìn giống một default professional profile cover.

Ví dụ visual direction:

`dark navy / deep blue / charcoal`

Không cần copy chính xác màu này nếu design system hiện tại đã có màu chuẩn.

Mục tiêu là User Profile chưa có background vẫn phải nhìn hoàn chỉnh.

---

# 6. TITLE / TÊN ĐẠI DIỆN

Tên đại diện của 3 trang phải có **cùng vertical alignment**.

Hãy lấy vị trí title của Goal làm baseline.

Sau đó áp dụng cho:

* Goal
* Team
* User Profile

Title phải có:

* cùng khoảng cách từ phía trên.
* cùng khoảng cách với avatar.
* cùng line-height logic.
* cùng visual hierarchy.

Không để:

Goal title ở một vị trí,

Team title lệch lên/xuống,

User title lại có một layout khác.

---

# 7. TITLE TRUNCATION

Goal và Team có thể có tên dài.

Tôi muốn giới hạn chiều dài title trong UI.

### Goal

Nếu title vượt quá chiều dài cho phép:

`Tên rất dài của Goal ...`

Không được để title phá vỡ layout.

### Team

Tương tự.

Nếu tên dài:

`Tên Team rất dài ...`

Hãy sử dụng:

* CSS line clamp hoặc
* text-overflow ellipsis

tùy layout hiện tại.

Quan trọng:

> Không được để title làm xô lệch Action Area hoặc layout bên phải.

---

# 8. ACTION AREA BÊN PHẢI

Hiện tại Goal/Team đang có nhiều button:

* Star
* Share
* More
* ...

Tôi muốn giảm visual noise.

## Goal Detail

Giữ action area gọn.

Không cần hiển thị quá nhiều button trực tiếp.

Các action phụ nên đưa vào `...`.

## Team Detail

Làm giống Goal.

Có thể giữ những action thực sự cần thiết, còn action phụ đưa vào overflow menu.

---

# 9. OVERFLOW MENU CHO GOAL VÀ TEAM

Nút `...` phải trở thành nơi chứa các action phụ.

Khi click:

Hiển thị menu gồm đúng các action:

* ⭐ Đánh dấu sao
* Archive
* Delete

Không cần thêm hàng chục action khác.

Menu phải:

* nhỏ gọn
* sạch
* dễ đọc
* có icon phù hợp
* spacing thống nhất
* có destructive styling cho Delete nếu design system hiện tại hỗ trợ.

Nếu Star đang active thì menu phải phản ánh đúng trạng thái hiện tại.

Ví dụ:

`☆ Đánh dấu sao`

hoặc

`★ Bỏ đánh dấu sao`

Không tạo action duplicate giữa button bên ngoài và menu.

---

# 10. USER PROFILE ACTION

User Profile không nên dùng action menu giống Goal/Team.

User Profile cần interaction riêng:

## Action chính

Hiển thị:

`Gửi lời mời kết bạn`

hoặc wording phù hợp với hệ thống hiện tại.

Mục tiêu:

* CTA rõ ràng
* dễ nhìn
* nằm trong Action Area bên phải.
* không cần Star/Archive/Delete như Goal/Team.

Nếu user đã gửi lời mời:

thể hiện trạng thái phù hợp.

Ví dụ:

`Đã gửi lời mời`

Nếu đã là bạn:

`Bạn bè`

Nếu logic backend hiện tại đã có trạng thái relationship thì hãy sử dụng trạng thái đó.

Không mock trạng thái.

---

# 11. BUTTON STYLE

Tất cả button trong 3 trang phải được normalize.

Đặc biệt:

* Back button
* Primary CTA
* Secondary button
* Icon button
* Overflow button
* Star button

Phải thống nhất:

* height
* border radius
* icon size
* font weight
* padding
* hover
* active
* focus
* disabled state

Không để mỗi page có một style button khác nhau.

---

# 12. TABS

3 trang hiện có tabs.

Hãy normalize:

* height
* padding
* font
* active state
* hover state
* border
* radius

Active tab phải có cùng visual treatment.

Không để:

Goal active tab màu xanh nhạt,

User active tab một kiểu khác,

Team active tab lại một kiểu khác.

Có thể khác label hoặc số lượng tab, nhưng component và visual language phải giống nhau.

---

# 13. CONTENT CONTAINER + PADDING

Đây là phần rất quan trọng.

Phần content bên dưới Hero của cả 3 trang phải sử dụng **cùng hệ thống spacing**.

Chuẩn hóa:

* left padding
* right padding
* top padding
* bottom padding
* khoảng cách giữa section
* khoảng cách giữa heading và content
* khoảng cách giữa main content và sidebar.

Goal hiện tại có một số khoảng cách khác User/Team.

Hãy đưa về một spacing system chung.

Ví dụ:

```text
Hero
↓
Title row
↓
Tabs
↓
Content
```

Các khoảng cách giữa những layer này phải đồng nhất.

---

# 14. MAIN CONTENT + RIGHT SIDEBAR

Nếu page có sidebar bên phải, hãy chuẩn hóa grid.

Ví dụ:

```text
┌───────────────────────────────┬──────────────┐
│                               │              │
│        Main Content           │   Sidebar    │
│                               │              │
└───────────────────────────────┴──────────────┘
```

Cả 3 trang phải:

* cùng container width
* cùng sidebar width hoặc cùng rule
* cùng gap
* cùng alignment.

Nếu một trang không có sidebar thì content chính vẫn phải tuân theo cùng container/padding system.

---

# 15. NOTES BUTTON

Hiện tại cả 3 trang có Notes floating button.

Hãy kiểm tra:

* vị trí
* kích thước
* offset từ edge
* shadow
* border radius
* icon
* typography

và làm cho cả 3 trang sử dụng cùng một implementation.

Không tạo 3 Notes button khác nhau.

---

# 16. RESPONSIVE

Sau khi chuẩn hóa desktop, kiểm tra responsive.

Đặc biệt:

### Desktop

Hero, avatar, title và action phải nằm đúng hàng.

### Tablet

Action area không được tràn.

### Mobile

Có thể stack:

```text
Avatar
Title
Meta
Action
Tabs
Content
```

Nhưng phải giữ cùng visual hierarchy.

---

# 17. COMPONENT ARCHITECTURE

Tôi muốn bạn ưu tiên tạo shared components thay vì copy CSS giữa 3 page.

Kiểm tra project hiện tại và nếu phù hợp hãy tạo các component như:

```text
DetailPageLayout
DetailHero
DetailAvatar
DetailTitle
DetailActions
DetailTabs
DetailContent
DetailSidebar
OverflowMenu
```

Không nhất thiết phải dùng đúng tên trên.

Mục tiêu là:

```text
GoalDetail
   ↓
Shared Detail Components

TeamDetail
   ↓
Shared Detail Components

UserProfile
   ↓
Shared Detail Components
```

Thay vì:

```text
Goal CSS riêng
Team CSS riêng
User CSS riêng
```

---

# 18. DESIGN DIRECTION

Tổng thể UI cần hướng tới:

* Enterprise
* Modern
* Clean
* Professional
* Atlassian/Jira-inspired
* Ít visual noise
* Consistent spacing
* Consistent typography
* Consistent interaction.

Không thêm quá nhiều decoration chỉ để "cho đẹp".

Ưu tiên:

**hierarchy + spacing + consistency + usability.**

---

# 19. KHÔNG ĐƯỢC LÀM

Không:

* thay đổi business logic không cần thiết
* thay đổi API contract
* thay đổi database schema
* xóa chức năng đang hoạt động
* hard-code user data
* hard-code Goal data
* hard-code Team data
* tạo UI component trùng lặp nếu đã có component tương tự.
* phá routing.
* phá permission/role logic.
* thay đổi wording hàng loạt nếu không cần.
* thay đổi sidebar/global navigation ngoài phạm vi task.

---

# 20. REFERENCE VISUAL

Hãy sử dụng các ảnh tôi cung cấp như visual reference:

* **Ảnh Goal:** làm baseline cho layout, avatar, title alignment và spacing.
* **Ảnh User Profile:** làm reference cho profile-specific behavior.
* **Ảnh Team:** làm reference cho team-specific behavior.
* **Ảnh 4:** làm source/reference cho Team avatar icon.
* **Ảnh 5:** làm reference cho default patterned background.

Không cần copy pixel-perfect.

Hãy **phân tích visual language** của chúng rồi xây thành một design system dùng chung.

---

# 21. ACCEPTANCE CRITERIA

Sau khi hoàn thành, tôi muốn khi mở 3 trang cạnh nhau:

### Goal

```text
[Cover]
       [Goal Avatar]

       Goal Name                [ ... ]

       Tabs

       Content
```

### Team

```text
[Cover]
       [Team Icon Avatar]

       Team Name                [ ... ]

       Tabs

       Content
```

### User

```text
[Cover]
       [User Avatar]

       User Name               [Kết bạn]

       Tabs

       Content
```

Ba trang phải có:

* Cover cùng height
* Avatar cùng size
* Avatar cùng position
* Title cùng vertical position
* Action area cùng alignment
* Tabs cùng style
* Content cùng padding
* Main container cùng alignment
* Sidebar spacing cùng rule
* Notes button cùng position
* Typography cùng hierarchy.

Chỉ khác:

* dữ liệu
* loại entity
* icon/avatar
* background source
* action logic riêng của từng entity.

---

# 22. FINAL CHECK

Sau khi code xong:

1. Chạy project.
2. Mở cả 3 page.
3. So sánh trực tiếp.
4. Kiểm tra pixel-level consistency ở:

   * cover height
   * avatar size
   * avatar position
   * title position
   * action position
   * tabs
   * content padding
   * sidebar
5. Kiểm tra overflow title.
6. Kiểm tra overflow menu.
7. Kiểm tra Team default background.
8. Kiểm tra Goal default background.
9. Kiểm tra User custom background.
10. Kiểm tra User fallback background.
11. Kiểm tra responsive.
12. Kiểm tra console không có warning/error mới.

Nếu trong project đã có component/design token phù hợp, **ưu tiên reuse và refactor component hiện tại thay vì tạo component mới trùng chức năng.**

Cuối cùng hãy báo cáo:

* Những file đã thay đổi.
* Component dùng chung nào được tạo/refactor.
* Những gì đã được normalize.
* Những behavior nào khác nhau giữa Goal / Team / User và lý do.
* Có issue nào cần tôi review thủ công hay không.
