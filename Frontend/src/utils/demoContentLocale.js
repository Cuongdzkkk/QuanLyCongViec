const normalizedLocale = (locale) => String(locale || 'vi').toLowerCase().startsWith('en') ? 'en' : 'vi'

const viToEn = {
  'SprintA Enterprise Platform': 'SprintA Enterprise Platform',
  'Integration Hub & Unified Inbox': 'Integration Hub & Unified Inbox',
  'Mobile App MVP': 'Mobile App MVP',
  'Customer Success Portal': 'Customer Success Portal',
  'Website Landing & Growth Campaign': 'Website Landing & Growth Campaign',
  'Internal Operations Automation': 'Internal Operations Automation',
  'Hệ thống đặt lịch book xe trung tâm dạy lái': 'Driving Center Vehicle Booking System',

  'Nâng cấp nền tảng SprintA thành hệ thống quản lý công việc cấp doanh nghiệp với đầy đủ tính năng Kanban, Sprint, Goal, OKR, Wiki, SprintA AI.':
    'Upgrade SprintA into an enterprise work management platform with Kanban, sprints, goals, OKRs, wiki, reports, and SprintA AI capabilities.',
  'Kết nối Google Calendar, GitHub, Slack, Microsoft Mail và gom thông báo về một inbox chung cho mỗi user.':
    'Connect Google Calendar, GitHub, Slack, and Microsoft Mail, then bring every notification into one inbox for each user.',
  'Phát triển ứng dụng mobile cho quản lý task, sprint và notification bằng Flutter.':
    'Build a Flutter mobile app for task management, sprint tracking, and push notifications.',
  'Xây dựng cổng hỗ trợ khách hàng, tài liệu hướng dẫn và quy trình phản hồi.':
    'Build a customer support portal with help documents and a clear response workflow.',
  'Tối ưu landing page, SEO, tracking conversion và chiến dịch marketing.':
    'Improve the landing page, SEO, conversion tracking, and marketing campaigns.',
  'Tự động hóa quy trình nội bộ: phê duyệt, báo cáo, audit và quản lý nhân sự.':
    'Automate internal workflows for approvals, reports, audit trails, and people operations.',
  'Dự án mẫu quản lý tài khoản, xe tập lái, dịch vụ, đặt lịch, thanh toán, hóa đơn và báo cáo vận hành trung tâm.':
    'A sample project for managing accounts, training vehicles, services, bookings, payments, invoices, and driving center operations.',

  'Hệ thống Gamification tích lũy điểm thưởng khi đóng góp code':
    'Gamification system tracks reward points for code contributions',
  'Báo cáo tự động OKR định kỳ gửi qua Slack':
    'Scheduled OKR summary report sent to Slack',
  'Đồng bộ lịch Google Calendar với Dashboard cá nhân':
    'Sync Google Calendar with the personal dashboard',
  'Tạo OAuth flow cho Google Calendar':
    'Build the OAuth flow for Google Calendar',
  'Unified Inbox đọc dữ liệu từ database thật':
    'Unified Inbox reads real data from the database',
  'Tạo task từ sự kiện Google Calendar':
    'Create a task from a Google Calendar event',
  'Thiết kế lại Integration Hub theo phong cách SaaS':
    'Redesign Integration Hub with a SaaS-style experience',
  'Chuẩn hóa responsive cho dashboard mobile':
    'Polish the dashboard responsive layout for mobile',
  'Checklist QA cho chức năng tạo công việc':
    'QA checklist for work item creation',
  'Theo dõi lỗi đăng nhập OAuth trên môi trường local':
    'Track local OAuth sign-in issues',
  'Họp chiến lược phát triển sản phẩm Q3':
    'Q3 product strategy planning meeting',
  'Tạo tài khoản admin, giáo viên và học viên':
    'Create admin, instructor, and student accounts',
  'API quản lý danh sách xe tập lái':
    'API for managing training vehicles',
  'Màn hình lịch trống theo xe và giáo viên':
    'Availability calendar by vehicle and instructor',
  'Luồng học viên đặt lịch thực hành trong 3 bước':
    'Three-step practical lesson booking flow for students',
  'Kiểm tra trùng lịch xe, giáo viên và học viên':
    'Detect booking conflicts for vehicles, instructors, and students',
  'Trang chi tiết phiếu đặt lịch':
    'Booking request detail page',
  'Tích hợp thanh toán học phí và đặt cọc':
    'Integrate tuition and deposit payments',
  'Xuất hóa đơn và biên lai cho học viên':
    'Generate invoices and receipts for students',
  'Nhắc lịch học qua email và thông báo trong app':
    'Send lesson reminders by email and in-app notification',
  'Báo cáo tỷ lệ sử dụng xe theo tuần':
    'Weekly vehicle utilization report',
  'Giao diện lịch dạy cá nhân cho giáo viên':
    'Personal teaching schedule for instructors',
  'API đánh giá buổi học sau khi hoàn thành':
    'API for post-lesson evaluations',

  'Tạo ví cá nhân cho từng user, ghi lại lịch sử cộng điểm kudo và đổi quà.':
    'Create a personal wallet for each user, with kudo point history and reward redemption.',
  'Schedule job chạy mỗi chiều thứ Sáu, tự quét tiến độ OKR/Goal và gửi tóm tắt vào channel Slack.':
    'Run a scheduled job every Friday afternoon to scan OKR and goal progress, then send a summary to Slack.',
  'API tự quét sự kiện trong ngày và ghim lên mục "My Calendar" ở trang For You.':
    'Scan daily events through the API and pin them to the My Calendar area on the For You page.',
  'Thảo luận về lộ trình nâng cấp SprintA và kế hoạch marketing.':
    'Discuss SprintA upgrade roadmap and the marketing plan.',
  'Hoàn thiện luồng tạo tài khoản, gán vai trò và kích hoạt hồ sơ người dùng cho trung tâm dạy lái.':
    'Complete account creation, role assignment, and profile activation for the driving center.',
  'Xây dựng endpoint thêm xe, cập nhật trạng thái bảo trì, lọc theo hạng bằng lái và tình trạng khả dụng.':
    'Build endpoints for adding vehicles, updating maintenance status, and filtering by license class and availability.',
  'Thiết kế lịch tuần giúp điều phối viên thấy khung giờ còn trống, xe đang bảo trì và giáo viên bận.':
    'Design a weekly calendar so coordinators can see free slots, vehicles under maintenance, and busy instructors.',
  'Học viên chọn gói học, ngày giờ, xe hoặc giáo viên mong muốn rồi gửi yêu cầu xác nhận.':
    'Students choose a lesson package, time, vehicle or preferred instructor, then submit the request for confirmation.',
  'Backend cần chặn booking nếu cùng xe, cùng giáo viên hoặc cùng học viên đã có lịch ở khung giờ giao nhau.':
    'The backend must block bookings when the same vehicle, instructor, or student already has an overlapping schedule.',
  'Hiển thị thông tin học viên, giáo viên, xe, địa điểm tập, trạng thái xác nhận và ghi chú của điều phối viên.':
    'Show student, instructor, vehicle, training location, confirmation status, and coordinator notes.',
  'Tạo API ghi nhận thanh toán tiền mặt/chuyển khoản, đối soát công nợ và khóa lịch khi chưa đặt cọc.':
    'Create APIs for cash/bank transfer payments, receivable reconciliation, and schedule locking before deposit.',
  'Tạo mẫu biên lai, mã giao dịch, trạng thái đã thu và chức năng tải PDF cho học viên.':
    'Create receipt templates, transaction codes, paid status, and PDF download for students.',
  'Tự động gửi nhắc lịch trước 24 giờ và trước 2 giờ cho học viên, giáo viên và điều phối viên.':
    'Automatically send reminders 24 hours and 2 hours before lessons to students, instructors, and coordinators.',
  'Tổng hợp số giờ xe được đặt, số giờ bảo trì, tỷ lệ hủy lịch và danh sách xe cần kiểm tra.':
    'Summarize booked vehicle hours, maintenance hours, cancellation rate, and vehicles that need inspection.',
  'Giáo viên xem lịch theo ngày, xác nhận đã dạy, ghi chú lỗi xe hoặc đánh giá kỹ năng học viên.':
    'Instructors view daily schedules, confirm completed lessons, note vehicle issues, and evaluate student skills.',
  'Lưu nhận xét của giáo viên, số km thực hành, kỹ năng cần cải thiện và đề xuất lịch học tiếp theo.':
    'Store instructor notes, practice kilometers, skills to improve, and suggested next lessons.'
}

const enToVi = Object.fromEntries(Object.entries(viToEn).map(([vi, en]) => [en, vi]))

export const translateDemoText = (value, locale = 'vi') => {
  if (value === null || value === undefined) return value

  const text = String(value)
  const lang = normalizedLocale(locale)
  if (lang === 'en') return viToEn[text] || text
  return enToVi[text] || text
}

export const translateDemoFields = (record, locale = 'vi', fields = ['name', 'title', 'description', 'projectName']) => {
  if (!record || typeof record !== 'object') return record
  return fields.reduce((nextRecord, field) => {
    if (Object.prototype.hasOwnProperty.call(nextRecord, field)) {
      nextRecord[field] = translateDemoText(nextRecord[field], locale)
    }
    return nextRecord
  }, { ...record })
}

const translatablePayloadKeys = new Set([
  'name',
  'title',
  'description',
  'projectName',
  'summary',
  'content',
  'message',
  'body',
  'label'
])

export const translateDemoPayload = (payload, locale = 'vi') => {
  if (Array.isArray(payload)) {
    return payload.map(item => translateDemoPayload(item, locale))
  }

  if (!payload || typeof payload !== 'object') {
    return payload
  }

  return Object.fromEntries(
    Object.entries(payload).map(([key, value]) => {
      if (typeof value === 'string' && translatablePayloadKeys.has(key)) {
        return [key, translateDemoText(value, locale)]
      }
      return [key, translateDemoPayload(value, locale)]
    })
  )
}
