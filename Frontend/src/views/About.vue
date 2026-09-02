<script setup>
import PublicLegalLayout from '@/components/layout/PublicLegalLayout.vue'
</script>

<template>
  <PublicLegalLayout
    eyebrow="ABOUT SPRINTA"
    title="Một không gian làm việc rõ ràng cho đội nhóm"
    intro="SprintA là ứng dụng quản lý project, task và quy trình làm việc, giúp đội nhóm biết việc gì đang diễn ra, ai phụ trách và bước tiếp theo là gì."
  >
    <section class="about-section">
      <h2>SprintA là gì?</h2>
      <p>
        SprintA hỗ trợ đội nhóm tổ chức project, task, board và work item trong các workspace. Người dùng có thể theo dõi tiến độ, phối hợp, xem báo cáo và duy trì các quy trình làm việc, năng suất phù hợp với cách đội mình vận hành.
      </p>
    </section>

    <section class="about-grid" aria-label="Các nhóm chức năng của SprintA">
      <div>
        <h3>Quản lý công việc</h3>
        <p>Tạo, phân công, sắp xếp và theo dõi task, cycle, mục tiêu và các work item liên quan.</p>
      </div>
      <div>
        <h3>Cộng tác và báo cáo</h3>
        <p>Làm việc cùng thành viên trong workspace, xem hoạt động và sử dụng các báo cáo hỗ trợ quyết định.</p>
      </div>
      <div>
        <h3>Tích hợp tùy chọn</h3>
        <p>Google Sign-In dùng để xác thực. Google Calendar và Gmail là các tích hợp tùy chọn, chỉ kết nối khi người dùng chủ động yêu cầu.</p>
      </div>
    </section>

    <section class="about-section about-next">
      <h2>Thông tin dành cho người dùng mới</h2>
      <p>
        Bạn có thể xem <router-link to="/privacy">Chính sách riêng tư</router-link> và <router-link to="/terms">Điều khoản dịch vụ</router-link> trước khi bắt đầu. Nếu đã có tài khoản, hãy <router-link to="/login">đăng nhập SprintA</router-link>.
      </p>
    </section>
  </PublicLegalLayout>
</template>

<style scoped>
.about-section + .about-section {
  margin-top: 36px;
}

.about-section h2,
.about-grid h3 {
  margin: 0;
  color: var(--sp-text);
  font-family: var(--sp-font-display);
}

.about-section h2 {
  font-size: clamp(22px, 3vw, 30px);
  letter-spacing: -.025em;
}

.about-section p,
.about-grid p {
  margin: 12px 0 0;
  color: var(--sp-text-muted);
  line-height: 1.75;
}

.about-section a {
  color: var(--sp-primary);
  font-weight: 750;
}

.about-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
  margin-top: 32px;
}

.about-grid > div {
  min-width: 0;
  padding: 20px;
  border: 1px solid var(--sp-border);
  border-radius: var(--sp-radius-md);
  background: var(--sp-surface-raised);
}

.about-grid h3 {
  font-size: 17px;
}

.about-grid p {
  font-size: 13px;
}

.about-next {
  padding-top: 28px;
  border-top: 1px solid var(--sp-border);
}

@media (max-width: 760px) {
  .about-grid {
    grid-template-columns: 1fr;
  }
}
</style>
