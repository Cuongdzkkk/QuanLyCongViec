<script setup>
import PublicLegalLayout from '@/components/layout/PublicLegalLayout.vue'
</script>

<template>
  <PublicLegalLayout
    eyebrow="PRIVACY POLICY"
    title="Chính sách riêng tư SprintA"
    intro="Chính sách này mô tả những thông tin SprintA có thể xử lý, mục đích sử dụng và các lựa chọn của bạn khi dùng dịch vụ."
  >
    <p class="policy-updated">Cập nhật lần cuối: 02/09/2026</p>

    <section class="policy-section">
      <h2>1. Thông tin SprintA có thể xử lý</h2>
      <p>Tùy theo cách bạn sử dụng dịch vụ, SprintA có thể xử lý:</p>
      <ul>
        <li>Thông tin nhận diện tài khoản như tên, email và thông tin hồ sơ.</li>
        <li>Thông tin tên, email và hồ sơ được cung cấp thông qua Google Sign-In.</li>
        <li>Nội dung project, task, workspace, board, báo cáo và dữ liệu do bạn tạo trong SprintA.</li>
        <li>Thông tin kỹ thuật và bảo mật cần thiết để vận hành, bảo vệ và khắc phục sự cố của dịch vụ.</li>
      </ul>
    </section>

    <section class="policy-section">
      <h2>2. Google Sign-In</h2>
      <p>
        Đăng nhập Google thông thường của SprintA chỉ yêu cầu các scope cơ bản: <code>openid</code>, <code>email</code> và <code>profile</code>. Các scope này được dùng để xác thực và nhận diện tài khoản. Google Calendar, Gmail, Drive, Contacts và các scope API nhạy cảm khác không được yêu cầu cho đăng nhập thông thường.
      </p>
    </section>

    <section class="policy-section">
      <h2>3. Tích hợp Google tùy chọn</h2>
      <div class="policy-subsection">
        <h3>Google Calendar</h3>
        <p>Đây là tích hợp tùy chọn. SprintA hiện sử dụng <code>calendar.readonly</code> và chỉ truy cập khi bạn chủ động kết nối Google Calendar. Quyền này được dùng để đọc và hiển thị thông tin lịch phù hợp với chức năng tích hợp.</p>
      </div>
      <div class="policy-subsection">
        <h3>Gmail</h3>
        <p>Đây là tích hợp tùy chọn. SprintA hiện sử dụng <code>gmail.readonly</code> và chỉ truy cập khi bạn chủ động kết nối Gmail. Quyền này chỉ được dùng cho chức năng do tích hợp Gmail cung cấp.</p>
      </div>
      <p>Những quyền Google Calendar và Gmail nêu trên không bắt buộc cho việc đăng nhập SprintA.</p>
    </section>

    <section class="policy-section">
      <h2>4. Cách sử dụng dữ liệu</h2>
      <p>Dữ liệu Google và dữ liệu tài khoản được sử dụng để cung cấp chức năng SprintA mà bạn yêu cầu, duy trì tài khoản, vận hành dịch vụ, bảo vệ an toàn và hỗ trợ xử lý sự cố. SprintA không bán dữ liệu Google cho mục đích quảng cáo. Dữ liệu có thể được xử lý bởi các nhà cung cấp hạ tầng hoặc dịch vụ kỹ thuật cần thiết để vận hành SprintA, theo phạm vi phù hợp với mục đích này.</p>
    </section>

    <section class="policy-section">
      <h2>5. Bảo mật</h2>
      <p>SprintA áp dụng các biện pháp phù hợp như truyền tải qua kết nối bảo mật, kiểm soát quyền truy cập, bảo vệ thông tin xác thực bằng cơ chế phía máy chủ và giới hạn quyền Google theo chức năng. Không có biện pháp nào bảo đảm an toàn tuyệt đối, vì vậy bạn cũng nên bảo vệ thông tin đăng nhập và thiết bị của mình.</p>
    </section>

    <section class="policy-section">
      <h2>6. Thu hồi quyền truy cập</h2>
      <p>Bạn có thể ngắt kết nối các tích hợp trong SprintA ở nơi chức năng này được hỗ trợ, và/hoặc thu hồi quyền truy cập SprintA từ phần quyền của tài khoản Google. Việc thu hồi có thể khiến chức năng tích hợp tương ứng không còn hoạt động cho đến khi bạn kết nối lại.</p>
    </section>

    <section class="policy-section">
      <h2>7. Yêu cầu xóa tài khoản hoặc dữ liệu</h2>
      <p>Nếu muốn yêu cầu xóa tài khoản hoặc dữ liệu liên quan, vui lòng liên hệ <a href="mailto:support@sprinta.id.vn">support@sprinta.id.vn</a>. SprintA có thể cần xác minh yêu cầu và sẽ phản hồi về phạm vi, thời gian xử lý hoặc các dữ liệu cần được giữ lại theo nghĩa vụ vận hành phù hợp.</p>
    </section>

    <section class="policy-section">
      <h2>8. Liên hệ và cập nhật chính sách</h2>
      <p>Nếu có câu hỏi về quyền riêng tư, hãy liên hệ <a href="mailto:support@sprinta.id.vn">support@sprinta.id.vn</a>. SprintA có thể cập nhật chính sách này khi dịch vụ hoặc yêu cầu vận hành thay đổi; phiên bản mới sẽ được đăng trên trang này cùng ngày cập nhật.</p>
    </section>
  </PublicLegalLayout>
</template>

<style scoped>
.policy-updated {
  margin: 0 0 28px;
  color: var(--sp-text-muted);
  font-size: 12px;
}

.policy-section + .policy-section {
  margin-top: 32px;
  padding-top: 32px;
  border-top: 1px solid var(--sp-border);
}

.policy-section h2,
.policy-subsection h3 {
  margin: 0;
  color: var(--sp-text);
  font-family: var(--sp-font-display);
}

.policy-section h2 {
  font-size: 21px;
  letter-spacing: -.02em;
}

.policy-subsection {
  margin-top: 20px;
}

.policy-subsection h3 {
  font-size: 16px;
}

.policy-section p,
.policy-section li {
  color: var(--sp-text-muted);
  line-height: 1.75;
}

.policy-section p {
  margin: 12px 0 0;
}

.policy-section ul {
  display: grid;
  gap: 8px;
  margin: 12px 0 0;
  padding-left: 22px;
}

.policy-section a {
  color: var(--sp-primary);
  font-weight: 750;
}

code {
  padding: 2px 5px;
  border-radius: 4px;
  color: var(--sp-text);
  background: var(--sp-surface-raised);
  font-size: .9em;
}
</style>
