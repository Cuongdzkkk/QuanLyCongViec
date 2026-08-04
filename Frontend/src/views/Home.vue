<script setup>
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import {
  ArrowRight,
  BarChart3,
  Bot,
  Check,
  ChevronDown,
  CircleHelp,
  Coins,
  FileText,
  KanbanSquare,
  Layers3,
  Languages,
  Menu,
  Moon,
  Play,
  Rocket,
  ShieldCheck,
  Sparkles,
  Sun,
  Target,
  Users,
  Workflow,
  X,
  Zap
} from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import axiosClient from '@/api/axiosClient'
import ProductVideoSection from '@/components/landing/ProductVideoSection.vue'
import { currentTheme, toggleTheme } from '@/utils/theme'
import { clearAuthSession, getStoredAccessToken, getStoredUserSession } from '@/utils/authSession'
import { language, setLanguage } from '@/i18n'

const router = useRouter()
const user = ref(getStoredUserSession() || {})
const authenticated = ref(Boolean(getStoredAccessToken()))
const mobileOpen = ref(false)
const activeFaq = ref(0)
const pricing = ref(null)
const usage = ref(null)
const pricingError = ref(false)
const usageError = ref(false)
const landingRoot = ref(null)
const scrollProgress = ref(0)
let revealObserver = null
let revealFallbackTimer = null

const isVi = computed(() => language.value === 'vi')
const displayName = computed(() => user.value?.fullName || user.value?.username || user.value?.email || (isVi.value ? 'Người dùng SprintA' : 'SprintA user'))
const initials = computed(() => displayName.value.split(/\s+/).filter(Boolean).map((part) => part[0]).slice(-2).join('').toUpperCase() || 'SA')
const workspaceName = computed(() => user.value?.currentWorkspace?.name || user.value?.workspaceName || 'Workspace')
const editorialHeadlines = computed(() => isVi.value ? {
  hero: [
    [{ text: 'Quản lý ' }, { text: 'công việc', tone: 'cyan', glint: true }],
    [{ text: 'rõ ràng, chạy sprint' }],
    [{ text: 'gọn hơn.', tone: 'mint' }]
  ],
  ai: [
    [{ text: 'AI', tone: 'amber' }, { text: ' hiểu ' }, { text: 'context.', tone: 'cyan', glint: true }],
    [{ text: 'Bạn', tone: 'editorial' }, { text: ' vẫn giữ quyền quyết định.', tone: 'mint' }]
  ],
  video: [
    [{ text: 'SprintA', tone: 'cyan', glint: true }, { text: ' trong một luồng' }],
    [{ text: 'làm việc thật', tone: 'amber', glint: true }]
  ],
  workflow: [
    [{ text: 'Từ ' }, { text: 'ý tưởng', tone: 'amber', glint: true }, { text: ' đến ' }, { text: 'hoàn thành', tone: 'mint', glint: true }]
  ]
} : {
  hero: [
    [{ text: 'Work with ' }, { text: 'clarity.', tone: 'cyan', glint: true }],
    [{ text: 'Run focused sprints' }],
    [{ text: 'without noise.', tone: 'mint' }]
  ],
  ai: [
    [{ text: 'AI', tone: 'amber' }, { text: ' understands ' }, { text: 'context.', tone: 'cyan', glint: true }],
    [{ text: 'You', tone: 'editorial' }, { text: ' stay in control.', tone: 'mint' }]
  ],
  video: [
    [{ text: 'SprintA', tone: 'cyan', glint: true }, { text: ' in a real' }],
    [{ text: 'workflow', tone: 'amber', glint: true }]
  ],
  workflow: [
    [{ text: 'From ' }, { text: 'idea', tone: 'amber', glint: true }, { text: ' to ' }, { text: 'done', tone: 'mint', glint: true }]
  ]
})
const landingHeadlines = computed(() => isVi.value ? {
  products: ['Một hệ thống', 'đủ sâu', 'cho đội thật'],
  ai: ['AI hiểu context.', 'Bạn vẫn giữ quyền quyết định.'],
  pricing: ['Giá và AI credits', 'lấy từ server'],
  workflow: ['Từ ý tưởng đến', 'hoàn thành'],
  cta: ['Tạo workspace', 'đầu tiên'],
  faq: ['Những điều', 'cần biết']
} : {
  products: ['A real system', 'built deep', 'for real teams'],
  ai: ['AI understands context.', 'You stay in control.'],
  pricing: ['Pricing and AI credits', 'from the server'],
  workflow: ['From idea', 'to done'],
  cta: ['Create your first', 'workspace'],
  faq: ['Good', 'to know']
})

const copy = computed(() => isVi.value ? {
  nav: ['Tính năng', 'AI', 'Quy trình', 'Bảng giá', 'Video'],
  badge: 'SPRINTA AGILE WORKSPACE',
  title: 'Quản lý công việc rõ ràng, chạy sprint gọn hơn.',
  intro: 'SprintA gom task, cycle, mục tiêu, tài liệu, báo cáo và AI Copilot vào một workspace thống nhất để đội nhóm luôn thấy rõ việc cần làm, người phụ trách và rủi ro đang nằm ở đâu.',
  start: 'Bắt đầu miễn phí',
  demo: 'Xem demo',
  proof: ['Không cần thẻ thanh toán', 'Demo data có sẵn', 'Cài như PWA'],
  productsTitle: 'Một hệ thống đủ sâu cho đội thật',
  productsIntro: 'Không phải landing giả. Các module bên dưới đang tồn tại trong SprintA và nối với dữ liệu runtime.',
  aiTitle: 'AI hiểu context. Bạn vẫn giữ quyền quyết định.',
  aiIntro: 'Copilot tạo bản xem trước action, giải thích tác động và chỉ gọi API khi bạn xác nhận.',
  pricingTitle: 'Giá và AI credits lấy từ server',
  pricingIntro: 'Landing đọc pricing/usage từ backend thật. Gói chưa được chốt giá sẽ hiển thị đúng trạng thái pending, không bịa số.',
  workflowTitle: 'Từ ý tưởng đến hoàn thành',
  cta: 'Tạo workspace đầu tiên',
  open: 'Mở demo',
  signIn: 'Đăng nhập',
  logout: 'Đăng xuất',
  launch: 'Vào SprintA',
  aiButton: 'Mở AI Assistant',
  faqTitle: 'Những điều cần biết',
  apiFail: 'Không tải được dữ liệu từ API.',
  usageFail: 'Không tải được usage hiện tại.',
  includedUsers: 'người dùng bao gồm',
  includedCredits: 'AI credits bao gồm',
  remainingCredits: 'AI credits còn lại',
  pending: 'Chưa công bố',
  monthly: 'Theo tháng',
  serverPricing: 'Dữ liệu từ server',
  popular: 'Được đề xuất',
  choosePlan: 'Bắt đầu với gói',
  perMonth: '/ tháng',
  perUser: '/ người dùng',
  extraCredits: 'Có thể mua thêm AI credits',
  transparentPricing: 'Bảng giá minh bạch, không có chi phí ẩn',
  plansPending: 'Bảng giá đang được cập nhật',
  plansPendingDetail: 'Chưa có gói nào được công bố từ server. SprintA sẽ hiển thị tại đây ngay khi dữ liệu được phê duyệt.'
} : {
  nav: ['Features', 'AI', 'Workflow', 'Pricing', 'Video'],
  badge: 'SPRINTA AGILE WORKSPACE',
  title: 'Work with clarity. Run sprints without noise.',
  intro: 'SprintA brings tasks, cycles, goals, documents, reports and AI Copilot into one focused workspace so every team sees ownership, risk and progress clearly.',
  start: 'Start for free',
  demo: 'Watch demo',
  proof: ['No credit card', 'Demo data included', 'Install as PWA'],
  productsTitle: 'A real system for real teams',
  productsIntro: 'These are not mock sections. They map to SprintA modules connected to runtime data.',
  aiTitle: 'AI understands context. You stay in control.',
  aiIntro: 'Copilot prepares action previews, explains impact and only calls the backend after confirmation.',
  pricingTitle: 'Pricing and AI credits from the server',
  pricingIntro: 'The landing reads pricing and usage from real backend endpoints. Unapproved prices stay pending instead of being invented.',
  workflowTitle: 'From idea to done',
  cta: 'Create your first workspace',
  open: 'Open demo',
  signIn: 'Sign in',
  logout: 'Log out',
  launch: 'Open SprintA',
  aiButton: 'Open AI Assistant',
  faqTitle: 'Good to know',
  apiFail: 'Could not load API data.',
  usageFail: 'Could not load current usage.',
  includedUsers: 'included users',
  includedCredits: 'included AI credits',
  remainingCredits: 'AI credits remaining',
  pending: 'Not published',
  monthly: 'Monthly',
  serverPricing: 'Server-backed pricing',
  popular: 'Recommended',
  choosePlan: 'Start with',
  perMonth: '/ month',
  perUser: '/ user',
  extraCredits: 'Extra AI credits available',
  transparentPricing: 'Transparent pricing with no hidden costs',
  plansPending: 'Pricing is being updated',
  plansPendingDetail: 'No plans are published by the server yet. SprintA will show them here as soon as the data is approved.'
})

const products = computed(() => (isVi.value ? [
  { icon: KanbanSquare, name: 'Kanban & công việc', detail: 'Backlog, trạng thái, ưu tiên, người phụ trách và deadline trong một board.', route: '/dashboard' },
  { icon: Zap, name: 'Chu kỳ', detail: 'Lập sprint, giữ trọng tâm và phát hiện việc chậm tiến độ.', route: '/cycles' },
  { icon: Target, name: 'Mục tiêu & OKR', detail: 'Nối kết quả đội nhóm với công việc tạo ra tác động.', route: '/home/goals' },
  { icon: BarChart3, name: 'Báo cáo', detail: 'Theo dõi tiến độ, tải công việc, overdue và rủi ro từ dữ liệu thật.', route: '/reports' },
  { icon: FileText, name: 'Pages', detail: 'Giữ context dự án, ghi chú và tài liệu gần với công việc.', route: '/pages' },
  { icon: Users, name: 'Thành viên & quyền', detail: 'Vai trò workspace, project và quyền truy cập được thể hiện rõ.', route: '/home/people' }
] : [
  { icon: KanbanSquare, name: 'Kanban & Work Items', detail: 'Backlog, status, priority, owner and due date in one board.', route: '/dashboard' },
  { icon: Zap, name: 'Cycles', detail: 'Plan a sprint, keep focus visible and spot delayed work.', route: '/cycles' },
  { icon: Target, name: 'Goals & OKR', detail: 'Connect team outcomes to work that moves them forward.', route: '/home/goals' },
  { icon: BarChart3, name: 'Reports', detail: 'See progress, workload, overdue work and risk from real data.', route: '/reports' },
  { icon: FileText, name: 'Pages', detail: 'Keep project context, notes and documents close to the work.', route: '/pages' },
  { icon: Users, name: 'Members & permissions', detail: 'Workspace, project roles and access stay explicit.', route: '/home/people' }
]).map((item) => ({ ...item, image: '/landing/sprinta-dashboard-real.png' })))

const faqs = computed(() => isVi.value ? [
  ['AI có tự ý sửa dữ liệu không?', 'Không. AI action cần bản xem trước và xác nhận trước khi gọi API thực thi.'],
  ['Có thể dùng SprintA không cần AI không?', 'Có. Work items, cycles, goals, pages và reports hoạt động độc lập với AI.'],
  ['AI credit được tính thế nào?', 'Landing hiển thị quy tắc và usage do server trả về, không tự suy đoán số liệu.'],
  ['Logo dùng asset nào?', 'Landing dùng logo thật trong Frontend/public, không thay bằng mascot hoặc ký tự giả.']
] : [
  ['Can AI change data by itself?', 'No. AI actions require a preview and confirmation before execution.'],
  ['Can I use SprintA without AI?', 'Yes. Work items, cycles, goals, pages and reports work independently.'],
  ['How are AI credits counted?', 'The landing displays server-provided rules and usage without inventing numbers.'],
  ['Which logo is used?', 'The landing uses the real asset from Frontend/public, not the mascot or a placeholder letter.']
])

const workflowSteps = computed(() => isVi.value
  ? [
      { title: 'Thu thập yêu cầu', detail: 'Gom bối cảnh và mục tiêu', icon: FileText },
      { title: 'Tạo project', detail: 'Thiết lập không gian chung', icon: KanbanSquare },
      { title: 'Tách work item', detail: 'Biến kế hoạch thành việc rõ ràng', icon: Check },
      { title: 'Phân vai trò', detail: 'Chốt người chịu trách nhiệm', icon: Users },
      { title: 'Theo dõi sprint', detail: 'Giữ nhịp độ và xử lý lệch hướng', icon: Target },
      { title: 'Báo cáo rủi ro', detail: 'Ra quyết định bằng tín hiệu thật', icon: BarChart3 }
    ]
  : [
      { title: 'Capture request', detail: 'Bring context and goals together', icon: FileText },
      { title: 'Create project', detail: 'Set up a shared workspace', icon: KanbanSquare },
      { title: 'Break into work', detail: 'Turn plans into clear work items', icon: Check },
      { title: 'Assign owners', detail: 'Make ownership explicit', icon: Users },
      { title: 'Track sprint', detail: 'Keep momentum and resolve drift', icon: Target },
      { title: 'Report risk', detail: 'Make decisions from live signals', icon: BarChart3 }
    ])

const go = (path) => {
  mobileOpen.value = false
  if (path.startsWith('#')) {
    // An anchor can jump before IntersectionObserver receives its first frame.
    // Reveal its children eagerly so a navigation click never leaves a blank section.
    landingRoot.value?.querySelectorAll(`${path} [data-reveal]`).forEach((item) => item.classList.add('is-visible'))
    document.querySelector(path)?.scrollIntoView({ behavior: 'smooth' })
  }
  else router.push(path)
}

const syncUser = () => {
  user.value = getStoredUserSession() || {}
  authenticated.value = Boolean(getStoredAccessToken())
}

const loadContext = async () => {
  if (!authenticated.value) return
  try {
    const response = await axiosClient.get('/auth/context')
    const data = response.data?.data || {}
    user.value = {
      ...user.value,
      ...(data.user || {}),
      systemRoles: data.roles || [],
      permissions: data.permissions || [],
      workspaces: data.workspaces || [],
      currentWorkspace: data.currentWorkspace || null
    }
  } catch {
    // Guest landing must not break when context is unavailable.
  }
}

const loadPricing = async () => {
  pricingError.value = false
  try {
    pricing.value = (await axiosClient.get('/public/pricing')).data?.data || null
  } catch {
    pricingError.value = true
  }
}

const loadUsage = async () => {
  if (!authenticated.value) return
  usageError.value = false
  try {
    usage.value = (await axiosClient.get('/ai/usage-summary')).data?.data || null
  } catch {
    usageError.value = true
  }
}

const priceLabel = (plan) => {
  if (plan.monthlyPriceVnd == null) return copy.value.pending
  return `${new Intl.NumberFormat(isVi.value ? 'vi-VN' : 'en-US').format(plan.monthlyPriceVnd)} VND`
}

const planCode = (plan) => String(plan.id || plan.code || 'plan').toLowerCase()
const isFeaturedPlan = (plan) => plan.isFeatured === true || planCode(plan) === 'team'
const planIcon = (plan) => planCode(plan) === 'business' ? ShieldCheck : planCode(plan) === 'team' ? Users : Sparkles

const planFeatures = (plan) => {
  if (plan.features?.length) return plan.features
  const features = []
  if (plan.includedUsers != null) features.push(`${plan.includedUsers} ${copy.value.includedUsers}`)
  if (plan.includedAiCredits > 0) features.push(`${plan.includedAiCredits} ${copy.value.includedCredits}`)
  if (plan.extraAiCreditsEnabled) features.push(copy.value.extraCredits)
  return features
}

const logout = () => {
  clearAuthSession()
  authenticated.value = false
  user.value = {}
  router.push('/')
}

const updateScrollProgress = () => {
  const available = document.documentElement.scrollHeight - window.innerHeight
  scrollProgress.value = available > 0 ? Math.min(100, Math.max(0, (window.scrollY / available) * 100)) : 0
}

const setSpotlight = (event) => {
  const target = event.currentTarget
  const rect = target.getBoundingClientRect()
  target.style.setProperty('--spot-x', `${event.clientX - rect.left}px`)
  target.style.setProperty('--spot-y', `${event.clientY - rect.top}px`)
}

onMounted(() => {
  document.documentElement.lang = language.value
  window.addEventListener('storage', syncUser)
  loadContext()
  loadPricing()
  loadUsage()
  updateScrollProgress()
  window.addEventListener('scroll', updateScrollProgress, { passive: true })

  const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
  const revealItems = landingRoot.value?.querySelectorAll('[data-reveal]') || []
  landingRoot.value?.classList.add('motion-ready')
  if (reduceMotion || !('IntersectionObserver' in window)) {
    revealItems.forEach((item) => item.classList.add('is-visible'))
  } else {
    revealObserver = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return
        entry.target.classList.add('is-visible')
        revealObserver?.unobserve(entry.target)
      })
    }, { threshold: 0.14, rootMargin: '0px 0px -7% 0px' })
    revealItems.forEach((item) => revealObserver.observe(item))
  }
  // A final paint-safe fallback covers reload + immediate anchor navigation.
  revealFallbackTimer = window.setTimeout(() => {
    revealItems.forEach((item) => item.classList.add('is-visible'))
    landingRoot.value?.classList.add('motion-complete')
    revealObserver?.disconnect()
  }, 900)
})

onBeforeUnmount(() => {
  window.removeEventListener('storage', syncUser)
  window.removeEventListener('scroll', updateScrollProgress)
  revealObserver?.disconnect()
  window.clearTimeout(revealFallbackTimer)
})
</script>

<template>
  <div ref="landingRoot" class="landing-page">
    <header class="landing-nav">
      <span class="scroll-progress" :style="{ transform: `scaleX(${scrollProgress / 100})` }"></span>
      <div class="nav-inner">
        <router-link to="/" class="brand" aria-label="SprintA home">
          <span class="brand-mark" role="img" aria-label="SprintA logo"></span>
          <span>SprintA</span>
        </router-link>

        <nav class="desktop-nav" aria-label="Primary navigation">
          <a v-for="(item, index) in copy.nav" :key="item" :href="['#features','#ai','#workflow','#pricing','#video'][index]">{{ item }}</a>
        </nav>

        <div class="nav-actions">
          <button class="icon-btn" type="button" :aria-label="currentTheme === 'dark' ? 'Dark theme active' : 'Light theme active'" @click="toggleTheme()">
            <Moon v-if="currentTheme === 'dark'" :size="16" />
            <Sun v-else :size="16" />
          </button>
          <button class="lang-btn" type="button" :aria-label="isVi ? 'Ngôn ngữ hiện tại: Tiếng Việt. Chuyển sang English' : 'Current language: English. Switch to Vietnamese'" :title="isVi ? 'Tiếng Việt — bấm để chuyển sang English' : 'English — click to switch to Tiếng Việt'" @click="setLanguage(isVi ? 'en' : 'vi')">
            <Languages :size="15" /> {{ isVi ? 'VI' : 'EN' }}
          </button>
          <button v-if="authenticated" type="button" class="user-chip desktop-only" @click="go('/dashboard')">
            <span class="avatar">{{ initials }}</span>
            <span class="user-meta">
              <b>{{ displayName }}</b>
              <small>{{ workspaceName }}</small>
            </span>
          </button>
          <button v-if="authenticated" type="button" class="text-btn desktop-only" @click="logout">{{ copy.logout }}</button>
          <router-link v-else to="/login" class="text-btn desktop-only">{{ copy.signIn }}</router-link>
          <button class="btn btn-primary nav-cta" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
            {{ authenticated ? copy.launch : copy.start }}
          </button>
          <button class="icon-btn mobile-menu" type="button" aria-label="Open menu" @click="mobileOpen = !mobileOpen">
            <X v-if="mobileOpen" :size="18" />
            <Menu v-else :size="18" />
          </button>
        </div>
      </div>

      <nav v-if="mobileOpen" class="mobile-nav" aria-label="Mobile navigation">
        <a v-for="(item, index) in copy.nav" :key="item" :href="['#features','#ai','#workflow','#pricing','#video'][index]" @click="mobileOpen = false">{{ item }}</a>
        <button type="button" class="btn btn-primary" @click="go(authenticated ? '/dashboard' : '/register')">
          {{ authenticated ? copy.launch : copy.start }}
        </button>
      </nav>
    </header>

    <main>
      <section class="hero">
        <div class="hero-aurora" aria-hidden="true"><i></i><i></i><i></i></div>
        <div class="shell hero-grid">
          <div class="hero-copy reveal" data-reveal>
            <div class="eyebrow"><Sparkles :size="15" /> {{ copy.badge }}</div>
            <h1 :aria-label="copy.title" class="editorial-headline">
              <span v-for="(line, lineIndex) in editorialHeadlines.hero" :key="lineIndex" class="headline-line title-word" :style="{ '--word-delay': `${lineIndex * 84}ms` }">
                <span v-for="(part, partIndex) in line" :key="partIndex" :class="[part.tone ? `tone-${part.tone}` : null, { 'has-glint': part.glint }]">{{ part.text }}</span>
              </span>
            </h1>
            <p class="lead">{{ copy.intro }}</p>
            <div class="hero-actions">
              <button class="btn btn-primary" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
                {{ authenticated ? copy.launch : copy.start }} <ArrowRight :size="17" />
              </button>
              <button class="btn btn-secondary" type="button" @click="go('#video')">
                <Play :size="16" /> {{ copy.demo }}
              </button>
            </div>
            <div class="proof-row">
              <span v-for="item in copy.proof" :key="item"><Check :size="15" /> {{ item }}</span>
            </div>
          </div>

          <div class="hero-art reveal spotlight-card" data-reveal @pointermove="setSpotlight">
            <span class="orbit orbit-one" aria-hidden="true"></span>
            <span class="orbit orbit-two" aria-hidden="true"></span>
            <div class="dashboard-frame">
              <div class="frame-bar"><i></i><i></i><i></i><span>Dashboard / Sprint workspace</span></div>
              <img src="/landing/sprinta-dashboard-real.png" alt="SprintA dashboard preview" />
            </div>
            <div class="context-card">
              <span><Bot :size="18" /></span>
              <div><b>{{ isVi ? 'Gợi ý thông minh' : 'Smart suggestions' }}</b><small>{{ isVi ? 'Có xác nhận trước khi chạy' : 'Confirmed before execution' }}</small></div>
            </div>
            <div class="live-strip"><span></span>{{ isVi ? 'Dữ liệu dự án đang đồng bộ' : 'Project data is live' }}</div>
          </div>
        </div>
      </section>

      <div class="signal-rail" aria-hidden="true">
        <div class="signal-track">
          <span>WORK ITEMS</span><i></i><span>SPRINT PLANNING</span><i></i><span>GOALS & OKR</span><i></i><span>LIVE REPORTS</span><i></i><span>AI ACTION PREVIEW</span><i></i>
          <span>WORK ITEMS</span><i></i><span>SPRINT PLANNING</span><i></i><span>GOALS & OKR</span><i></i><span>LIVE REPORTS</span><i></i><span>AI ACTION PREVIEW</span><i></i>
        </div>
      </div>

      <section id="features" class="section section-raised">
        <div class="shell">
          <div class="section-intro" data-reveal>
            <div class="eyebrow"><Layers3 :size="15" /> PRODUCT SUITE</div>
            <h2 class="product-headline"><span>{{ landingHeadlines.products[0] }}</span> <em class="has-glint">{{ landingHeadlines.products[1] }}</em> <span>{{ landingHeadlines.products[2] }}</span></h2>
            <p>{{ copy.productsIntro }}</p>
          </div>
          <div class="product-grid">
            <article v-for="(product, index) in products" :key="product.route" class="product-card spotlight-card" :class="{ wide: index < 2 }" data-reveal @pointermove="setSpotlight">
              <div class="product-top">
                <span class="product-icon"><component :is="product.icon" :size="20" /></span>
                <span class="product-index">0{{ index + 1 }}</span>
                <button type="button" class="link-btn" @click="go(product.route)">{{ copy.open }} <ArrowRight :size="15" /></button>
              </div>
              <h3>{{ product.name }}</h3>
              <p>{{ product.detail }}</p>
              <img :src="product.image" :alt="`${product.name} in SprintA`" />
            </article>
          </div>
        </div>
      </section>

      <section id="ai" class="section ai-section">
        <div class="shell ai-grid">
          <div data-reveal>
            <div class="eyebrow inverted"><Bot :size="15" /> SPRINTA AI</div>
            <h2 class="editorial-headline ai-headline">
              <span v-for="(line, lineIndex) in editorialHeadlines.ai" :key="lineIndex" class="headline-line">
                <span v-for="(part, partIndex) in line" :key="partIndex" :class="[part.tone ? `tone-${part.tone}` : null, { 'has-glint': part.glint }]">{{ part.text }}</span>
              </span>
            </h2>
            <p class="section-copy">{{ copy.aiIntro }}</p>
            <div class="ai-flow">
              <div><span>01</span><b>{{ isVi ? 'Đọc ngữ cảnh' : 'Read context' }}</b><small>{{ isVi ? 'Tóm tắt project đang mở' : 'Summarizes the current project' }}</small></div>
              <ArrowRight :size="18" />
              <div><span>02</span><b>{{ isVi ? 'Xem tác động' : 'Preview impact' }}</b><small>{{ isVi ? 'Hiển thị action trước khi chạy' : 'Shows actions before execution' }}</small></div>
              <ArrowRight :size="18" />
              <div><span>03</span><b>{{ isVi ? 'Bạn quyết định' : 'You approve' }}</b><small>{{ isVi ? 'Không tự sửa dữ liệu' : 'No silent data mutation' }}</small></div>
            </div>
            <button class="btn btn-ghost" type="button" @click="go('/dashboard')">{{ copy.aiButton }} <ArrowRight :size="16" /></button>
          </div>
          <div class="ai-panel spotlight-card" data-reveal @pointermove="setSpotlight">
            <div class="panel-head">
              <img src="/ai-sprinta/idle.png" alt="SprintA AI mascot" />
              <div>
                <b>SprintA AI</b>
                <small>{{ isVi ? 'Trợ lý công việc có kiểm soát' : 'Controlled work assistant' }}</small>
              </div>
            </div>
            <div class="prompt-bubble">{{ isVi ? 'Tạo task kiểm tra báo cáo tuần này và gán người phụ trách.' : 'Create a task to review this week’s report and assign an owner.' }}</div>
            <div class="action-card">
              <div class="action-title">
                <ShieldCheck :size="18" />
                <b>{{ isVi ? 'Action Preview' : 'Action Preview' }}</b>
                <span>{{ isVi ? 'Chờ xác nhận' : 'Needs approval' }}</span>
              </div>
              <p>{{ isVi ? 'Tạo 1 work item trong project hiện tại. Chưa có dữ liệu nào được ghi cho tới khi bạn bấm xác nhận.' : 'Creates one work item in the current project. Nothing is written until you confirm.' }}</p>
            </div>
          </div>
        </div>
      </section>

      <section id="pricing" class="section pricing-section">
        <div class="pricing-orb pricing-orb-one" aria-hidden="true"></div>
        <div class="pricing-orb pricing-orb-two" aria-hidden="true"></div>
        <div class="shell pricing-header" data-reveal>
          <div class="pricing-heading">
            <div class="eyebrow"><Coins :size="15" /> USAGE TRANSPARENCY</div>
            <h2><span>{{ landingHeadlines.pricing[0] }}</span> <em>{{ landingHeadlines.pricing[1] }}</em></h2>
            <p class="section-copy">{{ copy.pricingIntro }}</p>
            <div class="pricing-mode" aria-label="Billing information">
              <span class="pricing-mode-active"><Coins :size="14" /> {{ copy.monthly }}</span>
              <span><ShieldCheck :size="14" /> {{ copy.serverPricing }}</span>
            </div>
          </div>
          <div v-if="usage && authenticated" class="usage-panel">
            <div><span>{{ isVi ? 'Workspace' : 'Workspace' }}</span><b>{{ usage.workspaceName || workspaceName }}</b></div>
            <div class="usage-values">
              <b>{{ usage.usedCredits ?? 0 }}</b><span>/</span><b>{{ usage.includedCredits ?? 0 }}</b>
            </div>
            <div><span>{{ copy.remainingCredits }}</span><b>{{ usage.remainingCredits ?? 0 }}</b></div>
          </div>
          <div v-else-if="usageError" class="api-state">{{ copy.usageFail }}</div>
        </div>
        <div class="shell">
          <div v-if="pricing?.plans?.length" class="pricing-grid">
            <article v-for="plan in pricing.plans" :key="plan.id || plan.code || plan.name" class="price-card spotlight-card" :class="{ featured: isFeaturedPlan(plan) }" data-reveal @pointermove="setSpotlight">
              <div v-if="isFeaturedPlan(plan)" class="popular-badge"><Sparkles :size="13" /> {{ copy.popular }}</div>
              <div class="price-card-head">
                <span class="plan-icon"><component :is="planIcon(plan)" :size="20" /></span>
                <div class="price-label">{{ plan.id || plan.code || 'PLAN' }}</div>
              </div>
              <h3>{{ plan.name }}</h3>
              <div class="price-value" :class="{ pending: plan.monthlyPriceVnd == null }">
                <strong>{{ priceLabel(plan) }}</strong>
                <span v-if="plan.monthlyPriceVnd != null">{{ copy.perMonth }}<template v-if="plan.perUser"> {{ copy.perUser }}</template></span>
              </div>
              <p class="price-status"><span></span>{{ plan.monthlyPriceVnd == null ? copy.pending : copy.transparentPricing }}</p>
              <button class="plan-cta" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
                {{ copy.choosePlan }} {{ plan.name }} <ArrowRight :size="16" />
              </button>
              <div class="price-divider"></div>
              <div class="feature-list">
                <div v-for="feature in planFeatures(plan)" :key="feature" class="price-line"><span><Check :size="14" /></span>{{ feature }}</div>
                <div v-if="!planFeatures(plan).length" class="price-line muted"><span><ShieldCheck :size="14" /></span>{{ copy.serverPricing }}</div>
              </div>
            </article>
          </div>
          <div v-else-if="pricing" class="pricing-empty" data-reveal>
            <span><Coins :size="24" /></span>
            <div><b>{{ copy.plansPending }}</b><p>{{ copy.plansPendingDetail }}</p></div>
          </div>
          <div v-else-if="pricingError" class="api-state">{{ copy.apiFail }}</div>
        </div>
      </section>

      <section id="video" class="section video-section" data-reveal>
        <ProductVideoSection
          :title="isVi ? 'SprintA trong một luồng làm việc thật' : 'SprintA in a real workflow'"
          :intro="isVi ? 'Video dùng asset local, poster, phụ đề và transcript truy cập được.' : 'The video uses local assets, captions and an accessible transcript.'"
        >
          <template #title>
            <span v-for="(line, lineIndex) in editorialHeadlines.video" :key="lineIndex" class="headline-line">
              <span v-for="(part, partIndex) in line" :key="partIndex" :class="[part.tone ? `tone-${part.tone}` : null, { 'has-glint': part.glint }]">{{ part.text }}</span>
            </span>
          </template>
        </ProductVideoSection>
      </section>

      <section id="workflow" class="section">
        <div class="shell">
          <div class="section-intro" data-reveal>
            <div class="eyebrow"><Workflow :size="15" /> OPERATING FLOW</div>
            <h2 class="workflow-title editorial-headline">
              <span v-for="(line, lineIndex) in editorialHeadlines.workflow" :key="lineIndex" class="headline-line">
                <span v-for="(part, partIndex) in line" :key="partIndex" :class="[part.tone ? `tone-${part.tone}` : null, { 'has-glint': part.glint }]">{{ part.text }}</span>
              </span>
            </h2>
          </div>
          <div class="workflow-line" data-reveal>
            <div v-for="(step, index) in workflowSteps" :key="index" class="workflow-node" :class="{ 'is-lower': index % 2 === 1 }" :style="{ '--flow-delay': `${index * .12}s` }">
              <div class="workflow-copy">
                <span class="workflow-number">{{ String(index + 1).padStart(2, '0') }}</span>
                <b>{{ step.title }}</b>
                <small>{{ step.detail }}</small>
              </div>
              <div class="workflow-anchor" :aria-label="step.title">
                <component :is="step.icon" :size="22" stroke-width="2" />
              </div>
            </div>
          </div>
          <div class="final-cta spotlight-card" data-reveal @pointermove="setSpotlight">
            <div>
              <div class="eyebrow inverted"><Rocket :size="15" /> SPRINTA WORKSPACE</div>
              <h2><span>{{ landingHeadlines.cta[0] }}</span> <em>{{ landingHeadlines.cta[1] }}</em></h2>
            </div>
            <button class="btn btn-primary" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
              {{ authenticated ? copy.launch : copy.start }} <ArrowRight :size="17" />
            </button>
          </div>
        </div>
      </section>

      <section class="section faq-section">
        <div class="shell faq-grid" data-reveal>
          <div>
            <div class="eyebrow"><CircleHelp :size="15" /> FAQ</div>
            <h2><span>{{ landingHeadlines.faq[0] }}</span> <em>{{ landingHeadlines.faq[1] }}</em></h2>
            <p class="section-copy">{{ isVi ? 'Thông tin phản ánh đúng những gì hệ thống hiện hỗ trợ thật.' : 'Answers reflect what the current system really supports.' }}</p>
          </div>
          <div class="faq-list">
            <article v-for="(faq, index) in faqs" :key="index" class="faq-item">
              <button type="button" @click="activeFaq = activeFaq === index ? -1 : index">
                <span>{{ faq[0] }}</span><ChevronDown :size="17" :class="{ rotate: activeFaq === index }" />
              </button>
              <p v-if="activeFaq === index">{{ faq[1] }}</p>
            </article>
          </div>
        </div>
      </section>
    </main>

    <footer class="footer">
      <div class="shell footer-inner">
        <router-link to="/" class="brand"><span class="brand-mark" role="img" aria-label="SprintA logo"></span><span>SprintA</span></router-link>
        <span>© 2026 SprintA · {{ isVi ? 'Quản lý công việc rõ ràng hơn.' : 'Make work visible.' }}</span>
        <div><a href="#features">{{ copy.nav[0] }}</a><a href="#pricing">{{ copy.nav[3] }}</a><a href="#video">{{ copy.nav[4] }}</a></div>
      </div>
    </footer>
  </div>
</template>

<style scoped>
:global(*) { box-sizing: border-box; }
:global(html) { scroll-behavior: smooth; }
:global(body) { margin: 0; }
.landing-page {
  --bg: #f3f7f8;
  --surface: #ffffff;
  --surface-2: #eaf1f3;
  --ink: #061c2d;
  --muted: #536a78;
  --line: #cfdde2;
  --navy: #041c2e;
  --navy-2: #08324a;
  --accent: #00a9cf;
  --accent-2: #20c7a8;
  --accent-warm: #dc6848;
  --accent-gold: #c88716;
  --accent-soft: #d8f6fa;
  --brand-deep: #0d519c;
  --brand-royal: #0b4fd9;
  --brand-slate: #5c6795;
  --brand-sky: #41c0f2;
  --shadow: 0 24px 70px rgba(4, 28, 46, .11);
  width: 100%;
  min-width: 0;
  min-height: 100vh;
  overflow-x: clip;
  color: var(--ink);
  background: var(--bg);
  font-family: Inter, "Avenir Next", ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
}
.landing-page img { max-width: 100%; }
:global([data-theme="dark"] .landing-page) {
  --bg: #050d16;
  --surface: #0b1724;
  --surface-2: #101f2e;
  --ink: #f2f7f8;
  --muted: #9ab0bc;
  --line: #263b49;
  --navy: #030a12;
  --navy-2: #08293d;
  --accent: #35c8e6;
  --accent-2: #49d6ad;
  --accent-warm: #ff8061;
  --accent-gold: #f3b84b;
  --accent-soft: #123b48;
  --brand-deep: #0d519c;
  --brand-royal: #0b4fd9;
  --brand-slate: #7382bb;
  --brand-sky: #41c0f2;
  --shadow: 0 26px 86px rgba(0, 0, 0, .35);
  background:
    radial-gradient(circle at 18% 4%, rgba(53, 200, 230, .11), transparent 34rem),
    var(--bg);
}
.shell { width: min(1320px, calc(100% - 80px)); margin-inline: auto; }
.landing-nav {
  position: sticky;
  top: 14px;
  z-index: 30;
  width: min(1320px, calc(100% - 80px));
  margin: 14px auto -100px;
  border: 1px solid rgba(126, 158, 185, .28);
  border-radius: 22px;
  background: color-mix(in srgb, var(--surface) 86%, transparent);
  backdrop-filter: blur(18px);
  box-shadow: 0 18px 50px rgba(4, 28, 46, .09);
  overflow: hidden;
}
.scroll-progress { position: absolute; inset: 0 0 auto; height: 2px; transform-origin: left; background: linear-gradient(90deg, var(--accent), var(--accent-2)); will-change: transform; }
.nav-inner { min-height: 74px; display: flex; align-items: center; gap: 28px; padding: 0 18px; }
.brand { display: inline-flex; align-items: center; gap: 11px; color: var(--ink); text-decoration: none; font-weight: 900; letter-spacing: -.035em; }
.brand-mark {
  display: block;
  width: 12px;
  height: 11px;
  flex: 0 0 12px;
  background-image: url('/sprinta-mark-light.png');
  background-repeat: no-repeat;
  background-position: center;
  background-size: contain;
}
.brand-mark { filter: none; }
:global([data-theme="dark"] .brand-mark) { background-image: url('/sprinta-mark-dark.png'); filter: none; }
.brand > span:last-child { font-size: 17px; }
.desktop-nav { display: flex; gap: 22px; flex: 1; }
.desktop-nav a, .mobile-nav a { color: var(--muted); text-decoration: none; font-size: 13px; font-weight: 700; }
.desktop-nav a:hover, .mobile-nav a:hover { color: var(--accent); }
.nav-actions { display: flex; align-items: center; gap: 10px; }
.icon-btn, .lang-btn, .text-btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 6px;
  min-height: 38px;
  color: var(--muted);
  border: 1px solid transparent;
  border-radius: 12px;
  background: transparent;
  font-weight: 800;
  cursor: pointer;
}
.icon-btn { width: 40px; }
.icon-btn:hover, .text-btn:hover { color: var(--ink); border-color: var(--line); background: var(--surface-2); }
.lang-btn, .text-btn { padding-inline: 10px; }
.lang-btn { min-width: 58px; color: var(--accent); border-color: color-mix(in srgb, var(--accent) 45%, var(--line)); background: color-mix(in srgb, var(--accent) 10%, transparent); box-shadow: inset 0 1px rgba(255,255,255,.12); transition: transform .2s cubic-bezier(.2,.8,.2,1), background .2s ease, box-shadow .2s ease; }
.lang-btn:hover { color: var(--accent); background: color-mix(in srgb, var(--accent) 18%, transparent); box-shadow: 0 8px 20px color-mix(in srgb, var(--accent) 18%, transparent); transform: translateY(-1px); }
.lang-btn:active { transform: translateY(1px) scale(.97); }
.btn {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  min-height: 44px;
  padding: 0 18px;
  border-radius: 12px;
  border: 1px solid transparent;
  text-decoration: none;
  font-size: 14px;
  font-weight: 900;
  position: relative;
  overflow: hidden;
  transition: transform .2s cubic-bezier(.2,.8,.2,1), box-shadow .2s ease, background .2s ease, border-color .2s ease;
  cursor: pointer;
}
.btn::after { content: ''; position: absolute; inset: 0; background: linear-gradient(105deg, transparent 30%, rgba(255,255,255,.26) 48%, transparent 66%); transform: translateX(-140%); transition: transform .55s ease; }
.btn:hover::after { transform: translateX(140%); }
.btn:active { transform: translateY(2px) scale(.985); }
.btn-primary { color: #fff; background: #008fb8; box-shadow: 0 12px 28px rgba(0, 143, 184, .22); }
.btn-primary:hover { transform: translateY(-2px); background: #007fa8; box-shadow: 0 18px 40px rgba(0, 143, 184, .28); }
.btn-secondary { color: var(--ink); border-color: var(--line); background: var(--surface); }
.btn-ghost { color: #ecf8ff; border-color: rgba(255,255,255,.2); background: rgba(255,255,255,.08); }
.user-chip { display: flex; align-items: center; gap: 8px; padding: 4px 8px 4px 4px; border: 1px solid var(--line); border-radius: 999px; background: var(--surface-2); color: var(--ink); cursor: pointer; text-align: left; }
.avatar { display: grid; place-items: center; width: 32px; height: 32px; color: #fff; border-radius: 50%; background: #e35d43; font-size: 11px; font-weight: 900; }
.user-meta { display: grid; gap: 1px; }
.user-meta b { font-size: 12px; }
.user-meta small { color: var(--muted); font-size: 10px; }
.mobile-menu, .mobile-nav { display: none; }
.hero { position: relative; isolation: isolate; min-height: 100vh; display: grid; align-items: center; padding: 136px 0 96px; overflow: hidden; }
.hero::after { content: ''; position: absolute; inset: auto 0 0; height: 1px; background: linear-gradient(90deg, transparent, var(--line) 18% 82%, transparent); }
.hero-aurora { position: absolute; inset: 0; z-index: -1; pointer-events: none; overflow: hidden; }
.hero-aurora i { position: absolute; border-radius: 999px; filter: blur(4px); opacity: .7; }
.hero-aurora i:nth-child(1) { width: 42vw; height: 42vw; left: -14vw; top: -16vw; background: radial-gradient(circle, rgba(32,199,168,.2), transparent 68%); animation: auroraDrift 14s ease-in-out infinite alternate; }
.hero-aurora i:nth-child(2) { width: 34vw; height: 34vw; right: -9vw; top: 9vh; background: radial-gradient(circle, rgba(0,169,207,.18), transparent 68%); animation: auroraDrift 18s ease-in-out -4s infinite alternate-reverse; }
.hero-aurora i:nth-child(3) { width: 26vw; height: 26vw; left: 45%; bottom: -17vw; background: radial-gradient(circle, rgba(68,115,255,.10), transparent 68%); }
.hero-grid { display: grid; grid-template-columns: minmax(500px, .92fr) minmax(560px, 1.08fr); gap: clamp(58px, 7vw, 112px); align-items: center; }
.eyebrow { display: inline-flex; align-items: center; gap: 8px; color: var(--accent); font-size: 12px; font-weight: 950; letter-spacing: .16em; text-transform: uppercase; }
.eyebrow svg { box-sizing: content-box; padding: 5px; border: 1px solid color-mix(in srgb, currentColor 24%, transparent); border-radius: 9px; background: color-mix(in srgb, currentColor 10%, transparent); }
.hero h1 { max-width: 720px; margin: 20px 0; font-size: clamp(54px, 4.65vw, 78px); line-height: .96; letter-spacing: -.058em; text-wrap: balance; }
.title-word { display: inline-block; transform-origin: left bottom; animation: wordReveal .7s cubic-bezier(.16,1,.3,1) both; animation-delay: var(--word-delay); }
.section-intro h2 em, .faq-section h2 em, .final-cta h2 em {
  font-style: normal;
  color: var(--accent);
  text-shadow: 0 0 28px color-mix(in srgb, var(--accent) 22%, transparent);
}
.headline-line { display: block; }
.tone-cyan { color: var(--accent); text-shadow: 0 0 28px color-mix(in srgb, var(--accent) 24%, transparent); }
.tone-mint { color: var(--accent-2); text-shadow: 0 0 28px color-mix(in srgb, var(--accent-2) 22%, transparent); }
.tone-amber { color: var(--accent-gold); text-shadow: 0 0 26px color-mix(in srgb, var(--accent-gold) 22%, transparent); }
.tone-editorial { color: var(--accent-warm); font-family: Georgia, 'Times New Roman', serif; font-style: italic; font-weight: 600; letter-spacing: -.06em; }
.has-glint { position: relative; display: inline-block; }
.has-glint::after { content: '✦'; position: absolute; top: -.14em; right: -.22em; font-size: .17em; line-height: 1; color: currentColor; opacity: .7; text-shadow: 0 0 12px currentColor; animation: glintTwinkle 2.8s ease-in-out infinite; }
.lead { max-width: 700px; color: var(--muted); font-size: clamp(18px, 1.2vw, 21px); line-height: 1.72; }
.hero-actions { display: flex; flex-wrap: wrap; gap: 12px; margin-top: 30px; }
.proof-row { display: flex; flex-wrap: wrap; gap: 16px; margin-top: 24px; color: var(--muted); font-size: 13px; font-weight: 700; }
.proof-row span { display: inline-flex; align-items: center; gap: 6px; }
.proof-row svg { color: #17a878; }
.hero-art { position: relative; --spot-x: 50%; --spot-y: 50%; }
.hero-art::before { content: ''; position: absolute; inset: -45px; z-index: -1; border-radius: 50%; background: radial-gradient(circle at var(--spot-x) var(--spot-y), rgba(53,200,230,.25), transparent 46%); opacity: .75; transition: opacity .2s ease; }
.dashboard-frame {
  overflow: hidden;
  border: 1px solid rgba(100, 139, 170, .32);
  border-radius: 26px;
  background: var(--navy);
  box-shadow: 0 38px 110px rgba(4, 28, 46, .28);
  animation: dashboardEnter .9s cubic-bezier(.16,1,.3,1) both, dashboardFloat 7s ease-in-out 1s infinite;
  transform-origin: 50% 70%;
}
.frame-bar { display: flex; align-items: center; gap: 7px; padding: 14px 16px; color: #a9c4d7; font-size: 12px; }
.frame-bar i { width: 8px; height: 8px; border-radius: 50%; background: #ef5b63; }
.frame-bar i:nth-child(2) { background: #f4b63f; }
.frame-bar i:nth-child(3) { background: #25b66d; }
.frame-bar span { margin-left: 8px; }
.dashboard-frame img { display: block; width: 100%; aspect-ratio: 16 / 9; object-fit: cover; object-position: top; }
.context-card { position: absolute; right: -16px; top: 18%; display: flex; align-items: center; gap: 10px; max-width: 250px; padding: 14px; border: 1px solid rgba(255,255,255,.22); border-radius: 16px; color: #edf9ff; background: rgba(8, 37, 66, .88); box-shadow: 0 20px 48px rgba(7, 26, 47, .22); }
.context-card span { display: grid; place-items: center; width: 34px; height: 34px; border-radius: 11px; background: rgba(0, 167, 216, .24); color: #62daf1; }
.context-card div { display: grid; gap: 2px; }
.context-card small { color: #a8c4d8; }
.live-strip { position: absolute; left: 24px; bottom: -17px; display: flex; align-items: center; gap: 9px; padding: 10px 14px; color: var(--ink); border: 1px solid var(--line); border-radius: 999px; background: color-mix(in srgb, var(--surface) 90%, transparent); backdrop-filter: blur(14px); box-shadow: var(--shadow); font-size: 12px; font-weight: 850; }
.live-strip span { width: 8px; height: 8px; border-radius: 50%; background: var(--accent-2); box-shadow: 0 0 0 5px color-mix(in srgb, var(--accent-2) 18%, transparent); animation: livePulse 2s ease-out infinite; }
.orbit { position: absolute; z-index: -1; border: 1px solid color-mix(in srgb, var(--accent) 35%, transparent); border-radius: 50%; pointer-events: none; }
.orbit::after { content: ''; position: absolute; width: 8px; height: 8px; border-radius: 50%; background: var(--accent); box-shadow: 0 0 18px var(--accent); }
.orbit-one { width: 112px; height: 112px; right: -46px; bottom: -42px; animation: orbitSpin 12s linear infinite; }
.orbit-one::after { top: 15px; left: 10px; }
.orbit-two { width: 68px; height: 68px; left: -28px; top: 22px; animation: orbitSpin 9s linear infinite reverse; }
.orbit-two::after { right: 3px; bottom: 12px; }
.signal-rail { overflow: hidden; padding: 14px 0; color: var(--muted); border-bottom: 1px solid var(--line); background: var(--surface); font-size: 11px; font-weight: 900; letter-spacing: .16em; }
.signal-track { display: flex; align-items: center; gap: 28px; width: max-content; animation: railMove 28s linear infinite; }
.signal-track span { white-space: nowrap; }
.signal-track i { width: 5px; height: 5px; border-radius: 50%; background: var(--accent); }
.section { padding: 118px 0; scroll-margin-top: 112px; }
.section-raised { background: color-mix(in srgb, var(--surface-2) 86%, transparent); }
.section-intro { max-width: 760px; }
.section-intro h2, .ai-section h2, .faq-section h2, .final-cta h2 { margin: 14px 0 12px; font-size: clamp(38px, 4.4vw, 68px); line-height: 1; letter-spacing: -.055em; }
.product-headline { display: flex; flex-wrap: wrap; align-items: baseline; gap: 0 .22em; }
#features .product-headline em { color: var(--accent-gold); text-shadow: 0 0 28px color-mix(in srgb, var(--accent-gold) 22%, transparent); }
.workflow-title { max-width: none; white-space: nowrap; font-size: clamp(42px, 4vw, 62px) !important; }
.section-intro p, .section-copy { color: var(--muted); line-height: 1.75; font-size: 16px; }
.product-grid { display: grid; grid-template-columns: repeat(6, 1fr); gap: 20px; margin-top: 46px; }
.product-card { grid-column: span 2; min-height: 360px; display: flex; flex-direction: column; padding: 24px; border: 1px solid var(--line); border-radius: 24px; background: var(--surface); box-shadow: 0 16px 42px rgba(7, 26, 47, .055); transition: transform .24s cubic-bezier(.2,.8,.2,1), border-color .18s ease, box-shadow .18s ease; }
.product-card.wide { grid-column: span 3; }
.product-card:hover { transform: translateY(-7px); border-color: color-mix(in srgb, var(--accent) 50%, var(--line)); box-shadow: var(--shadow); }
.product-top { display: flex; justify-content: space-between; align-items: center; }
.product-icon { display: grid; place-items: center; width: 42px; height: 42px; border-radius: 14px; color: var(--accent); background: color-mix(in srgb, var(--accent) 12%, transparent); }
.product-index { margin-left: auto; color: color-mix(in srgb, var(--muted) 55%, transparent); font-size: 11px; font-weight: 900; letter-spacing: .12em; }
.product-index + .link-btn { margin-left: 18px; }
.link-btn { display: inline-flex; align-items: center; gap: 5px; color: var(--accent); border: 0; background: transparent; cursor: pointer; font-weight: 900; }
.product-card h3 { margin: 24px 0 10px; font-size: 27px; letter-spacing: -.04em; }
.product-card p { min-height: 54px; margin: 0 0 18px; color: var(--muted); line-height: 1.6; }
.product-card img { margin-top: auto; width: 100%; aspect-ratio: 16 / 8.5; object-fit: cover; object-position: top; border: 1px solid var(--line); border-radius: 16px; background: var(--surface-2); filter: saturate(.95) contrast(1.02); }
.ai-section { position: relative; overflow: hidden; color: #edf9ff; background: var(--navy); }
.ai-section::before { content: ''; position: absolute; width: 560px; height: 560px; right: -180px; top: -240px; border-radius: 50%; background: radial-gradient(circle, rgba(53,200,230,.16), transparent 68%); pointer-events: none; }
.inverted { color: #66d9f1; }
.ai-grid { display: grid; grid-template-columns: minmax(460px, .95fr) minmax(540px, 1.05fr); gap: clamp(52px, 7vw, 98px); align-items: center; }
.ai-section h2 { color: #edf9ff; }
.final-cta h2 em { color: var(--accent-2); text-shadow: 0 0 30px color-mix(in srgb, var(--accent-2) 24%, transparent); }
#pricing h2 em { color: var(--accent-gold); text-shadow: 0 0 28px color-mix(in srgb, var(--accent-gold) 20%, transparent); }
.faq-section h2 > span, .faq-section h2 > em { display: block; }
.faq-section h2 em { color: var(--accent-warm); text-shadow: 0 0 28px color-mix(in srgb, var(--accent-warm) 18%, transparent); }
.ai-section .section-copy { color: #a9c4d8; }
.ai-flow { display: grid; grid-template-columns: 1fr auto 1fr auto 1fr; align-items: stretch; gap: 12px; margin: 34px 0; }
.ai-flow > div { display: grid; gap: 7px; padding: 16px; border: 1px solid rgba(255,255,255,.15); border-radius: 18px; background: rgba(255,255,255,.06); }
.ai-flow span { color: #66d9f1; font-size: 12px; font-weight: 950; }
.ai-flow small { color: #a9c4d8; line-height: 1.45; }
.ai-flow > svg { align-self: center; color: #67cfe7; }
.spotlight-card { position: relative; isolation: isolate; --spot-x: 50%; --spot-y: 50%; }
.spotlight-card::after { content: ''; position: absolute; inset: 0; z-index: -1; border-radius: inherit; background: radial-gradient(360px circle at var(--spot-x) var(--spot-y), rgba(53,200,230,.11), transparent 64%); opacity: 0; transition: opacity .25s ease; pointer-events: none; }
.spotlight-card:hover::after { opacity: 1; }
.ai-panel { padding: 28px; border: 1px solid rgba(255,255,255,.17); border-radius: 28px; background: #0b2940; box-shadow: inset 0 1px rgba(255,255,255,.08), 0 28px 70px rgba(0,0,0,.18); }
.panel-head { display: flex; align-items: center; gap: 13px; }
.panel-head img { width: 58px !important; height: 58px !important; object-fit: contain !important; border-radius: 18px; background: rgba(255,255,255,.06); }
.panel-head div { display: grid; gap: 2px; }
.panel-head small { color: #a9c4d8; }
.prompt-bubble { width: min(82%, 520px); margin: 24px 0 16px auto; padding: 15px 16px; border-radius: 18px 18px 4px 18px; color: #dff7ff; background: rgba(0, 167, 216, .18); line-height: 1.6; }
.action-card { padding: 18px; border: 1px solid rgba(255,255,255,.16); border-radius: 18px; background: rgba(2, 17, 31, .56); }
.action-title { display: flex; align-items: center; gap: 8px; font-weight: 900; }
.action-title svg { color: #28d0a3; }
.action-title span { margin-left: auto; padding: 5px 8px; border-radius: 999px; color: #ffd789; background: rgba(255, 216, 137, .12); font-size: 12px; }
.action-card p { margin-bottom: 0; color: #a9c4d8; }
.split { max-width: none; display: grid; grid-template-columns: 1fr .85fr; gap: 70px; align-items: end; }
.pricing-section { position: relative; isolation: isolate; overflow: hidden; background: linear-gradient(180deg, color-mix(in srgb, var(--brand-sky) 5%, var(--bg)), var(--bg) 72%); }
.pricing-orb { position: absolute; z-index: -1; border-radius: 999px; pointer-events: none; filter: blur(2px); }
.pricing-orb-one { width: 420px; height: 420px; top: -180px; right: -120px; background: radial-gradient(circle, color-mix(in srgb, var(--brand-sky) 16%, transparent), transparent 68%); }
.pricing-orb-two { width: 360px; height: 360px; bottom: -210px; left: -120px; background: radial-gradient(circle, color-mix(in srgb, var(--brand-royal) 10%, transparent), transparent 70%); }
.pricing-header { display: grid; grid-template-columns: minmax(0, 1fr) minmax(320px, .72fr); gap: 70px; align-items: end; }
.pricing-heading { max-width: 760px; }
.pricing-mode { display: inline-flex; align-items: center; gap: 5px; margin-top: 24px; padding: 5px; border: 1px solid var(--line); border-radius: 999px; background: color-mix(in srgb, var(--surface) 88%, transparent); box-shadow: 0 12px 28px color-mix(in srgb, var(--ink) 6%, transparent); }
.pricing-mode span { display: inline-flex; align-items: center; gap: 7px; min-height: 36px; padding: 0 13px; color: var(--muted); border-radius: 999px; font-size: 12px; font-weight: 850; }
.pricing-mode .pricing-mode-active { color: #fff; background: var(--brand-deep); box-shadow: 0 8px 18px color-mix(in srgb, var(--brand-deep) 24%, transparent); }
.pricing-grid { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: 20px; margin-top: 52px; align-items: stretch; }
.price-card { position: relative; min-height: 500px; display: flex; flex-direction: column; padding: 30px; border: 1px solid color-mix(in srgb, var(--brand-slate) 24%, var(--line)); border-radius: 26px; background: color-mix(in srgb, var(--surface) 96%, transparent); box-shadow: 0 20px 54px color-mix(in srgb, var(--brand-deep) 8%, transparent); transition: transform .24s cubic-bezier(.2,.8,.2,1), border-color .22s ease, box-shadow .22s ease; }
.price-card:hover { transform: translateY(-7px); border-color: color-mix(in srgb, var(--brand-sky) 58%, var(--line)); box-shadow: 0 28px 64px color-mix(in srgb, var(--brand-deep) 14%, transparent); }
.price-card.featured { border-color: color-mix(in srgb, var(--brand-royal) 70%, var(--line)); box-shadow: 0 26px 70px color-mix(in srgb, var(--brand-royal) 17%, transparent); }
.price-card.featured::before { content: ''; position: absolute; inset: 0; z-index: -1; border-radius: inherit; background: linear-gradient(145deg, color-mix(in srgb, var(--brand-sky) 9%, transparent), transparent 38%); pointer-events: none; }
.popular-badge { position: absolute; top: -15px; left: 50%; display: inline-flex; align-items: center; gap: 6px; min-height: 30px; padding: 0 13px; color: #fff; border-radius: 999px; background: var(--brand-royal); box-shadow: 0 10px 24px color-mix(in srgb, var(--brand-royal) 30%, transparent); transform: translateX(-50%); font-size: 11px; font-weight: 900; white-space: nowrap; }
.price-card-head { display: flex; align-items: center; justify-content: space-between; gap: 14px; }
.plan-icon { display: grid; place-items: center; width: 44px; height: 44px; color: var(--brand-deep); border-radius: 14px; background: color-mix(in srgb, var(--brand-sky) 16%, var(--surface)); box-shadow: inset 0 0 0 1px color-mix(in srgb, var(--brand-sky) 24%, transparent); }
:global([data-theme="dark"] .landing-page) .plan-icon { color: var(--brand-sky); }
.price-label { color: var(--brand-slate); font-size: 11px; font-weight: 950; letter-spacing: .16em; text-transform: uppercase; }
.price-card h3 { margin: 22px 0 8px; font-size: 31px; letter-spacing: -.045em; }
.price-value { min-height: 58px; display: flex; align-items: baseline; gap: 8px; }
.price-value strong { color: var(--ink); font-size: clamp(25px, 2vw, 34px); letter-spacing: -.04em; }
.price-value span { color: var(--muted); font-size: 12px; font-weight: 750; }
.price-value.pending strong { color: var(--brand-deep); font-size: 24px; }
:global([data-theme="dark"] .landing-page) .price-value.pending strong { color: var(--brand-sky); }
.price-status { display: flex; align-items: center; gap: 8px; min-height: 40px; margin: 5px 0 20px; color: var(--muted); font-size: 12px; line-height: 1.5; }
.price-status > span { flex: 0 0 auto; width: 7px; height: 7px; border-radius: 50%; background: var(--brand-sky); box-shadow: 0 0 0 4px color-mix(in srgb, var(--brand-sky) 13%, transparent); }
.plan-cta { display: flex; align-items: center; justify-content: center; gap: 8px; width: 100%; min-height: 48px; padding: 0 18px; color: var(--brand-deep); border: 1px solid color-mix(in srgb, var(--brand-deep) 62%, var(--line)); border-radius: 14px; background: transparent; font-weight: 900; cursor: pointer; transition: transform .2s ease, color .2s ease, background .2s ease, box-shadow .2s ease; }
.plan-cta:hover { color: #fff; background: var(--brand-deep); box-shadow: 0 14px 28px color-mix(in srgb, var(--brand-deep) 22%, transparent); transform: translateY(-2px); }
.featured .plan-cta { color: #fff; border-color: var(--brand-royal); background: var(--brand-royal); box-shadow: 0 14px 30px color-mix(in srgb, var(--brand-royal) 25%, transparent); }
.featured .plan-cta:hover { background: var(--brand-deep); }
.price-divider { height: 1px; margin: 24px 0 15px; background: var(--line); }
.feature-list { display: grid; gap: 12px; }
.price-line { display: flex; align-items: flex-start; gap: 10px; color: var(--muted); line-height: 1.5; font-size: 13px; font-weight: 700; }
.price-line > span { flex: 0 0 auto; display: grid; place-items: center; width: 22px; height: 22px; color: #fff; border-radius: 7px; background: var(--brand-deep); }
.price-line.muted > span { background: var(--brand-slate); }
.pricing-empty { display: flex; align-items: center; gap: 16px; max-width: 760px; margin: 46px auto 0; padding: 22px 24px; border: 1px dashed color-mix(in srgb, var(--brand-sky) 52%, var(--line)); border-radius: 20px; background: color-mix(in srgb, var(--brand-sky) 7%, var(--surface)); }
.pricing-empty > span { flex: 0 0 auto; display: grid; place-items: center; width: 52px; height: 52px; color: var(--brand-deep); border-radius: 16px; background: color-mix(in srgb, var(--brand-sky) 18%, var(--surface)); }
.pricing-empty div { display: grid; gap: 5px; }
.pricing-empty b { font-size: 16px; }
.pricing-empty p { margin: 0; color: var(--muted); line-height: 1.55; }
.api-state { margin-top: 28px; padding: 18px; border: 1px solid #efc6c6; border-radius: 16px; color: #b33131; background: #fff2f2; }
.usage-panel { display: flex; justify-content: space-between; gap: 16px; margin-top: 20px; padding: 18px 20px; border: 1px solid var(--line); border-radius: 18px; background: var(--surface); color: var(--muted); }
.usage-panel div { display: grid; gap: 4px; }
.usage-panel b { color: var(--ink); }
.usage-values { display: flex !important; flex-direction: row; gap: 20px; }
.video-section { background: var(--surface); }
.workflow-line { position: relative; display: grid; grid-template-columns: repeat(6, minmax(0, 1fr)); gap: 8px; min-height: 266px; margin-top: 38px; padding: 0 6px; }
.workflow-line::before { content: ''; position: absolute; z-index: 0; top: 50%; right: 5.5%; left: 5.5%; height: 2px; background: repeating-linear-gradient(90deg, color-mix(in srgb, var(--accent) 68%, transparent) 0 11px, transparent 11px 20px); opacity: .72; }
.workflow-line::after { content: ''; position: absolute; z-index: 1; top: calc(50% - 5px); left: 5.5%; width: 10px; height: 10px; border-radius: 999px; background: var(--accent-2); box-shadow: 0 0 0 5px color-mix(in srgb, var(--accent-2) 13%, transparent), 0 0 22px color-mix(in srgb, var(--accent-2) 78%, transparent); animation: journeySignal 9s cubic-bezier(.4,0,.2,1) infinite; }
.workflow-node { position: relative; z-index: 2; display: grid; grid-template-rows: 1fr 76px 1fr; min-width: 0; }
.workflow-copy { position: relative; display: grid; align-content: end; gap: 6px; min-height: 104px; padding: 15px 14px; border: 1px solid color-mix(in srgb, var(--line) 78%, transparent); border-radius: 16px; background: color-mix(in srgb, var(--surface) 92%, transparent); box-shadow: 0 12px 26px color-mix(in srgb, var(--ink) 7%, transparent); transition: transform .26s cubic-bezier(.2,.8,.2,1), border-color .26s ease, box-shadow .26s ease; }
.workflow-copy::after { content: ''; position: absolute; right: 18px; bottom: -16px; width: 1px; height: 16px; border-right: 1px dashed color-mix(in srgb, var(--accent) 55%, transparent); }
.workflow-node.is-lower .workflow-copy { grid-row: 3; align-content: start; }
.workflow-node.is-lower .workflow-copy::after { top: -16px; bottom: auto; }
.workflow-anchor { display: grid; grid-row: 2; place-self: center; place-items: center; width: 58px; height: 58px; color: var(--accent); border: 1px solid color-mix(in srgb, var(--accent) 48%, var(--line)); border-radius: 999px; background: var(--surface); box-shadow: 0 0 0 7px color-mix(in srgb, var(--canvas) 82%, transparent), 0 12px 28px color-mix(in srgb, var(--ink) 10%, transparent); transition: transform .26s cubic-bezier(.2,.8,.2,1), color .26s ease, background-color .26s ease, box-shadow .26s ease; }
.workflow-node:hover .workflow-copy { border-color: color-mix(in srgb, var(--accent) 58%, var(--line)); box-shadow: 0 18px 34px color-mix(in srgb, var(--accent) 13%, transparent); transform: translateY(-4px); }
.workflow-node:hover .workflow-anchor { color: #fff; background: var(--accent); box-shadow: 0 0 0 7px color-mix(in srgb, var(--accent) 12%, transparent), 0 15px 28px color-mix(in srgb, var(--accent) 29%, transparent); transform: scale(1.08); }
.workflow-number { color: var(--accent); font-size: 11px; font-weight: 950; letter-spacing: .12em; }
.workflow-copy b { color: var(--ink); font-size: 14px; line-height: 1.28; }
.workflow-copy small { color: var(--muted); font-size: 11px; line-height: 1.4; }
.final-cta { display: flex; align-items: center; justify-content: space-between; gap: 32px; margin-top: 74px; padding: 42px; border-radius: 28px; color: #edf9ff; background: var(--navy); box-shadow: 0 30px 80px rgba(8, 37, 66, .2); overflow: hidden; }
.final-cta h2 { color: #edf9ff !important; }
.faq-grid { display: grid; grid-template-columns: .78fr 1.22fr; gap: 96px; }
.faq-list { border-top: 1px solid var(--line); }
.faq-item { border-bottom: 1px solid var(--line); }
.faq-item button { display: flex; justify-content: space-between; width: 100%; padding: 21px 0; color: var(--ink); border: 0; background: transparent; font: inherit; font-weight: 900; text-align: left; cursor: pointer; }
.faq-item svg { transition: transform .18s ease; }
.faq-item svg.rotate { transform: rotate(180deg); }
.faq-item p { max-width: 720px; margin: 0 0 20px; color: var(--muted); line-height: 1.65; }
.footer { padding: 30px 0; border-top: 1px solid var(--line); background: var(--surface); }
.footer-inner { display: flex; align-items: center; justify-content: space-between; gap: 20px; color: var(--muted); font-size: 13px; }
.footer-inner > div { display: flex; gap: 16px; }
.footer-inner a:not(.brand) { color: var(--muted); text-decoration: none; }
.motion-ready [data-reveal] { opacity: 0; transform: translateY(28px); transition: opacity .7s cubic-bezier(.16,1,.3,1), transform .7s cubic-bezier(.16,1,.3,1); }
.motion-ready [data-reveal].is-visible { opacity: 1; transform: translateY(0); }
.motion-ready.motion-complete [data-reveal] { opacity: 1; transform: translateY(0); }
.motion-ready .product-card:nth-child(2), .motion-ready .price-card:nth-child(2) { transition-delay: .08s; }
.motion-ready .product-card:nth-child(3), .motion-ready .price-card:nth-child(3) { transition-delay: .16s; }
.motion-ready .product-card:nth-child(4) { transition-delay: .04s; }
.motion-ready .product-card:nth-child(5) { transition-delay: .12s; }
.motion-ready .product-card:nth-child(6) { transition-delay: .20s; }
@keyframes wordReveal { from { opacity: 0; transform: translateY(24px) rotate(1.5deg); filter: blur(7px); } to { opacity: 1; transform: translateY(0) rotate(0); filter: blur(0); } }
@keyframes glintTwinkle { 0%, 100% { opacity: .28; transform: scale(.72) rotate(0); } 48% { opacity: 1; transform: scale(1.12) rotate(18deg); } }
@keyframes dashboardEnter { from { opacity: 0; transform: translateY(28px) rotateX(7deg) scale(.97); } to { opacity: 1; transform: translateY(0) rotateX(0) scale(1); } }
@keyframes dashboardFloat { 0%,100% { transform: translateY(0); } 50% { transform: translateY(-8px); } }
@keyframes auroraDrift { from { transform: translate3d(-2%, -2%, 0) scale(.95); } to { transform: translate3d(8%, 7%, 0) scale(1.08); } }
@keyframes orbitSpin { to { transform: rotate(360deg); } }
@keyframes livePulse { 0% { box-shadow: 0 0 0 0 rgba(32,199,168,.35); } 70%,100% { box-shadow: 0 0 0 9px rgba(32,199,168,0); } }
@keyframes railMove { to { transform: translateX(-50%); } }
@keyframes journeySignal { 0%, 8% { left: 5.5%; opacity: 0; } 12% { opacity: 1; } 84% { left: calc(94.5% - 10px); opacity: 1; } 94%, 100% { left: calc(94.5% - 10px); opacity: 0; } }
@media (prefers-reduced-motion: reduce) {
  *, *::before, *::after { animation-duration: .01ms !important; transition-duration: .01ms !important; scroll-behavior: auto !important; }
  .motion-ready [data-reveal] { opacity: 1 !important; transform: none !important; }
}
@media (max-width: 980px) {
  .desktop-nav, .desktop-only, .user-meta { display: none; }
  .mobile-menu { display: inline-flex; }
  .mobile-nav { display: grid; gap: 12px; padding: 14px 16px 18px; border-top: 1px solid var(--line); }
  .hero { min-height: auto; padding-top: 128px; }
  .hero-grid, .ai-grid, .faq-grid, .split, .pricing-header { grid-template-columns: 1fr; gap: 44px; }
  .product-grid { grid-template-columns: 1fr 1fr; }
  .pricing-grid { grid-template-columns: 1fr; max-width: 640px; margin-inline: auto; }
  .price-card { min-height: 0; }
  .product-card, .product-card.wide { grid-column: span 1; }
  .workflow-line { grid-template-columns: repeat(3, 1fr); gap: 22px 14px; min-height: auto; padding: 0; }
  .workflow-line::before, .workflow-line::after { display: none; }
  .workflow-node { grid-template-rows: auto; }
  .workflow-copy, .workflow-node.is-lower .workflow-copy { grid-row: 1; min-height: 118px; align-content: start; padding-top: 66px; }
  .workflow-copy::after { display: none; }
  .workflow-anchor { position: absolute; top: 14px; left: 14px; width: 42px; height: 42px; box-shadow: none; }
  .ai-flow { grid-template-columns: 1fr; }
  .ai-flow > svg { display: none; }
  .workflow-title { white-space: normal; }
}
@media (max-width: 640px) {
  .shell, .landing-nav { width: calc(100% - 28px); }
  .landing-nav { top: 10px; margin-top: 10px; }
  .nav-cta { display: none; }
  .hero { padding: 112px 0 70px; }
  .hero h1 { font-size: clamp(42px, 15vw, 58px); }
  .lead { font-size: 16px; }
  .section { padding: 74px 0; }
  .signal-track { animation-duration: 36s; }
  .product-grid, .pricing-grid { grid-template-columns: 1fr; }
  .pricing-mode { width: 100%; justify-content: center; border-radius: 18px; }
  .pricing-mode span { justify-content: center; flex: 1 1 0; padding-inline: 8px; }
  .price-card { padding: 25px 22px; }
  .workflow-line { grid-template-columns: 1fr; gap: 12px; }
  .workflow-copy, .workflow-node.is-lower .workflow-copy { min-height: 104px; padding: 16px 16px 16px 70px; align-content: center; }
  .workflow-anchor { top: 50%; left: 16px; transform: translateY(-50%); }
  .workflow-node:hover .workflow-anchor { transform: translateY(-50%) scale(1.05); }
  .final-cta, .usage-panel, .footer-inner { align-items: flex-start; flex-direction: column; }
  .context-card { position: static; margin-top: 14px; max-width: none; }
}
</style>
