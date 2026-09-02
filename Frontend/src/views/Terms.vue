<script setup>
import PublicLegalLayout from '@/components/layout/PublicLegalLayout.vue'
</script>

<template>
  <PublicLegalLayout
    eyebrow="TERMS OF SERVICE"
    title="Điều khoản sử dụng SprintA"
    intro="Các điều khoản cơ bản cho việc truy cập và sử dụng SprintA. Đây là thông tin sử dụng dịch vụ, không phải tư vấn pháp lý."
  >
    <p class="terms-updated">Cập nhật lần cuối: 02/09/2026</p>

    <section class="terms-section">
      <h2>1. Chấp nhận điều khoản</h2>
      <p>Bằng việc truy cập hoặc sử dụng SprintA, bạn xác nhận đã đọc và đồng ý tuân thủ các điều khoản này cùng các chính sách được liên kết. Nếu không đồng ý, vui lòng không sử dụng dịch vụ.</p>
    </section>

    <section class="terms-section">
      <h2>2. Mô tả dịch vụ</h2>
      <p>SprintA là ứng dụng quản lý project, task và work management. Dịch vụ cung cấp các chức năng như workspace, board, cộng tác, báo cáo và các quy trình hỗ trợ năng suất trong phạm vi phiên bản đang được vận hành.</p>
    </section>

    <section class="terms-section">
      <h2>3. Tài khoản và trách nhiệm của người dùng</h2>
      <p>Bạn chịu trách nhiệm cung cấp thông tin phù hợp, bảo vệ thông tin đăng nhập và mọi hoạt động phát sinh từ tài khoản của mình. Hãy thông báo sớm cho SprintA nếu bạn nghi ngờ tài khoản bị truy cập trái phép.</p>
    </section>

    <section class="terms-section">
      <h2>4. Sử dụng được chấp nhận</h2>
      <p>Bạn đồng ý sử dụng SprintA hợp pháp, tôn trọng quyền của người khác và không cố gắng phá hoại, gây quá tải, vượt qua kiểm soát truy cập, phát tán mã độc hoặc sử dụng dịch vụ để xâm phạm dữ liệu hay quyền riêng tư của người khác.</p>
    </section>

    <section class="terms-section">
      <h2>5. Tích hợp bên thứ ba và dịch vụ Google</h2>
      <p>SprintA có thể hỗ trợ các tích hợp bên thứ ba tùy theo cấu hình và phiên bản dịch vụ. Google Sign-In được dùng cho việc xác thực; Google Calendar và Gmail là các tích hợp tùy chọn chỉ được kết nối khi bạn chủ động yêu cầu. Việc sử dụng các dịch vụ Google cũng chịu sự điều chỉnh của điều khoản và chính sách riêng của Google.</p>
    </section>

    <section class="terms-section">
      <h2>6. Nội dung và sở hữu trí tuệ</h2>
      <p>Bạn giữ trách nhiệm đối với nội dung mình đưa vào SprintA và cần có quyền cần thiết để sử dụng nội dung đó. SprintA và các thành phần liên quan của dịch vụ được bảo vệ bởi các quyền sở hữu trí tuệ áp dụng; không điều khoản nào chuyển giao các quyền đó ngoài phạm vi cần thiết để bạn sử dụng dịch vụ.</p>
    </section>

    <section class="terms-section">
      <h2>7. Tính sẵn sàng và thay đổi dịch vụ</h2>
      <p>SprintA có thể bảo trì, sửa lỗi, cập nhật hoặc thay đổi một phần dịch vụ để vận hành và cải thiện sản phẩm. Chúng tôi sẽ cố gắng duy trì dịch vụ phù hợp, nhưng không thể cam kết dịch vụ luôn hoạt động liên tục hoặc không có lỗi.</p>
    </section>

    <section class="terms-section">
      <h2>8. Giới hạn trách nhiệm</h2>
      <p>Trong phạm vi pháp luật cho phép, SprintA không chịu trách nhiệm cho các thiệt hại phát sinh từ việc sử dụng hoặc không thể sử dụng dịch vụ, gián đoạn ngoài khả năng kiểm soát hợp lý, hoặc hành động của dịch vụ bên thứ ba. Nội dung này không loại trừ các trách nhiệm không thể loại trừ theo pháp luật áp dụng.</p>
    </section>

    <section class="terms-section">
      <h2>9. Tạm ngưng hoặc chấm dứt tài khoản</h2>
      <p>SprintA có thể tạm ngưng hoặc chấm dứt quyền truy cập khi cần để bảo vệ dịch vụ, người dùng khác, hoặc khi có vi phạm điều khoản. Bạn cũng có thể ngừng sử dụng dịch vụ và liên hệ để được hướng dẫn về yêu cầu tài khoản hoặc dữ liệu.</p>
    </section>

    <section class="terms-section">
      <h2>10. Cập nhật điều khoản và liên hệ</h2>
      <p>Điều khoản có thể được cập nhật khi dịch vụ hoặc yêu cầu vận hành thay đổi. Phiên bản mới sẽ được đăng trên trang này cùng ngày cập nhật. Nếu có câu hỏi, vui lòng liên hệ <a href="mailto:support@sprinta.id.vn">support@sprinta.id.vn</a>.</p>
    </section>
  </PublicLegalLayout>
</template>

<style scoped>
.terms-updated {
  margin: 0 0 28px;
  color: var(--sp-text-muted);
  font-size: 12px;
}

.terms-section + .terms-section {
  margin-top: 32px;
  padding-top: 32px;
  border-top: 1px solid var(--sp-border);
}

.terms-section h2 {
  margin: 0;
  color: var(--sp-text);
  font-family: var(--sp-font-display);
  font-size: 21px;
  letter-spacing: -.02em;
}

.terms-section p {
  margin: 12px 0 0;
  color: var(--sp-text-muted);
  line-height: 1.75;
}

.terms-section a {
  color: var(--sp-primary);
  font-weight: 750;
}
</style>
