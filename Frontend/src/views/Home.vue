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
const showPlanBenefits = ref(true)
let revealObserver = null
let revealFallbackTimer = null

const isVi = computed(() => language.value === 'vi')
const displayName = computed(() => user.value?.fullName || user.value?.username || user.value?.email || (isVi.value ? 'Người dùng SprintA' : 'SprintA user'))
const initials = computed(() => displayName.value.split(/\s+/).filter(Boolean).map((part) => part[0]).slice(-2).join('').toUpperCase() || 'SA')
const workspaceName = computed(() => user.value?.currentWorkspace?.name || user.value?.workspaceName || 'Workspace')
const editorialHeadlines = computed(() => isVi.value ? {
  hero: [
    [{ text: 'Quản lý ' }, { text: 'công việc', tone: 'cyan' }],
    [{ text: 'rõ ràng, ' }, { text: 'chạy sprint', tone: 'blue' }],
    [{ text: 'gọn hơn.', tone: 'mint' }]
  ],
  ai: [
    [{ text: 'AI hiểu ' }, { text: 'context.', tone: 'cyan' }],
    [{ text: 'Bạn vẫn giữ', tone: 'mint' }],
    [{ text: 'quyền quyết định.', tone: 'blue' }]
  ],
  video: [
    [{ text: 'SprintA', tone: 'cyan' }, { text: ' trong một luồng' }],
    [{ text: 'làm việc thật', tone: 'mint' }]
  ],
  workflow: [
    [{ text: 'Từ ' }, { text: 'ý tưởng', tone: 'warm' }, { text: ' đến ' }, { text: 'hoàn thành', tone: 'mint' }]
  ]
} : {
  hero: [
    [{ text: 'Work with ' }, { text: 'clarity.', tone: 'cyan' }],
    [{ text: 'Run focused ' }, { text: 'sprints', tone: 'blue' }],
    [{ text: 'without noise.', tone: 'mint' }]
  ],
  ai: [
    [{ text: 'AI understands ' }, { text: 'context.', tone: 'cyan' }],
    [{ text: 'You stay', tone: 'mint' }],
    [{ text: 'in control.', tone: 'blue' }]
  ],
  video: [
    [{ text: 'SprintA', tone: 'cyan' }, { text: ' in a real' }],
    [{ text: 'workflow', tone: 'mint' }]
  ],
  workflow: [
    [{ text: 'From ' }, { text: 'idea', tone: 'warm' }, { text: ' to ' }, { text: 'done', tone: 'mint' }]
  ]
})
const landingHeadlines = computed(() => isVi.value ? {
  products: ['Một hệ thống', 'đủ sâu', 'cho đội thật'],
  ai: ['AI hiểu context.', 'Bạn vẫn giữ quyền quyết định.'],
  pricing: ['Giá linh hoạt,', 'dễ bắt đầu'],
  workflow: ['Từ ý tưởng đến', 'hoàn thành'],
  cta: ['Bắt đầu cùng', 'SprintA?'],
  faq: ['Những điều', 'cần biết']
} : {
  products: ['One workspace', 'built deep', 'for real teams'],
  ai: ['AI understands context.', 'You stay in control.'],
  pricing: ['Flexible pricing,', 'easy to start'],
  workflow: ['From idea', 'to done'],
  cta: ['Ready to start with', 'SprintA?'],
  faq: ['Everything', 'worth knowing']
})

const copy = computed(() => isVi.value ? {
  nav: ['Tính năng', 'AI', 'Quy trình', 'Bảng giá', 'Video'],
  badge: 'SPRINTA',
  title: 'Quản lý công việc rõ ràng, chạy sprint gọn hơn.',
  intro: 'SprintA gom task, cycle, mục tiêu, tài liệu, báo cáo và AI Copilot vào một workspace thống nhất để đội nhóm luôn thấy rõ việc cần làm, người phụ trách và rủi ro.',
  start: 'Bắt đầu miễn phí',
  demo: 'Xem demo',
  proof: ['Không cần thẻ thanh toán', 'Demo data có sẵn', 'Cài như PWA'],
  productsTitle: 'Một hệ thống đủ sâu cho đội thật',
  productsIntro: 'Những module cốt lõi giúp đội nhóm lập kế hoạch, phối hợp và theo dõi công việc trong một workspace thống nhất.',
  aiTitle: 'AI hiểu context. Bạn vẫn giữ quyền quyết định.',
  aiIntro: 'SprintA AI đọc bối cảnh project, phân tích tác động và tạo bản xem trước hành động để bạn luôn là người quyết định cuối cùng.',
  pricingTitle: 'Giá linh hoạt, dễ bắt đầu',
  pricingIntro: 'Chọn gói phù hợp cho cá nhân, đội nhóm và doanh nghiệp. Nâng cấp khi nhu cầu của bạn tăng lên.',
  workflowTitle: 'Từ ý tưởng đến hoàn thành',
  cta: 'Bắt đầu cùng SprintA?',
  open: 'Mở module',
  signIn: 'Đăng nhập',
  logout: 'Đăng xuất',
  launch: 'Vào SprintA',
  aiButton: 'Mở AI Assistant',
  faqTitle: 'Những điều cần biết',
  apiFail: 'Không tải được bảng giá.',
  usageFail: 'Không tải được usage hiện tại.',
  includedUsers: 'người dùng bao gồm',
  includedCredits: 'AI credits bao gồm',
  pending: 'Liên hệ',
  monthly: 'Theo tháng',
  serverPricing: 'Quyền lợi theo gói',
  popular: 'Được đề xuất',
  choosePlan: 'Bắt đầu với gói',
  perMonth: '/ tháng',
  perUser: '/ người dùng',
  extraCredits: 'Có thể mua thêm AI credits',
  transparentPricing: 'Bảng giá minh bạch, không có chi phí ẩn',
  plansPending: 'Bảng giá đang được cập nhật',
  plansPendingDetail: 'Thông tin gói đang được hoàn thiện. Vui lòng quay lại sau.'
} : {
  nav: ['Features', 'AI', 'Workflow', 'Pricing', 'Video'],
  badge: 'SPRINTA',
  title: 'Work with clarity. Run focused sprints without noise.',
  intro: 'SprintA brings tasks, cycles, goals, documents, reports and AI Copilot into one focused workspace so ownership, risk and progress stay visible.',
  start: 'Start for free',
  demo: 'Watch demo',
  proof: ['No credit card', 'Demo data included', 'Install as PWA'],
  productsTitle: 'One workspace built deep for real teams',
  productsIntro: 'Core modules help teams plan, collaborate and follow work in one connected workspace.',
  aiTitle: 'AI understands context. You stay in control.',
  aiIntro: 'SprintA AI reads project context, analyzes impact and prepares an action preview so you remain the final decision maker.',
  pricingTitle: 'Flexible pricing, easy to start',
  pricingIntro: 'Choose the right plan for individuals, teams and businesses. Upgrade as your needs grow.',
  workflowTitle: 'From idea to done',
  cta: 'Ready to start with SprintA?',
  open: 'Open module',
  signIn: 'Sign in',
  logout: 'Log out',
  launch: 'Open SprintA',
  aiButton: 'Open AI Assistant',
  faqTitle: 'Everything worth knowing',
  apiFail: 'Could not load pricing.',
  usageFail: 'Could not load current usage.',
  includedUsers: 'included users',
  includedCredits: 'included AI credits',
  pending: 'Contact us',
  monthly: 'Monthly',
  serverPricing: 'Plan benefits',
  popular: 'Recommended',
  choosePlan: 'Start with',
  perMonth: '/ month',
  perUser: '/ user',
  extraCredits: 'Extra AI credits available',
  transparentPricing: 'Transparent pricing with no hidden costs',
  plansPending: 'Pricing is being updated',
  plansPendingDetail: 'Plan information is being finalized. Please check back soon.'
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
  ['AI có tự ý sửa dữ liệu không?', 'Không. AI luôn tạo bản xem trước và cần bạn xác nhận trước khi thực hiện hành động.'],
  ['Có thể dùng SprintA không cần AI không?', 'Có. Work items, cycles, goals, pages và reports hoạt động độc lập với AI.'],
  ['AI credit được tính thế nào?', 'Usage và quy tắc AI credit được hiển thị theo gói và mức sử dụng hiện tại của tài khoản.'],
  ['SprintA phù hợp với nhóm nào?', 'SprintA phù hợp từ cá nhân, nhóm nhỏ đến đội dự án cần một nơi thống nhất để lập kế hoạch và theo dõi công việc.']
] : [
  ['Can AI change data by itself?', 'No. AI always prepares a preview and requires your confirmation before execution.'],
  ['Can I use SprintA without AI?', 'Yes. Work items, cycles, goals, pages and reports work independently from AI.'],
  ['How are AI credits counted?', 'Usage and AI credit rules are shown based on your plan and current account usage.'],
  ['Who is SprintA for?', 'SprintA works for individuals, small teams and project teams that need one place to plan and track work.']
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
const isFeaturedPlan = (plan) => plan.isFeatured === true || planCode(plan) === 'plus'
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
  <div ref="landingRoot" class="landing-page" :class="{ 'is-light': currentTheme === 'light' }">
    <header class="landing-nav">
      <span class="scroll-progress" :style="{ transform: `scaleX(${scrollProgress / 100})` }"></span>
      <div class="nav-inner">
        <router-link to="/" class="brand" aria-label="SprintA home">
          <img class="brand-logo" src="/sprinta-mark-light.png" alt="" />
          <span class="brand-word">SprintA</span>
        </router-link>

        <nav class="desktop-nav" aria-label="Primary navigation">
          <a v-for="(item, index) in copy.nav" :key="item" :href="['#features','#ai','#workflow','#pricing','#video'][index]">{{ item }}</a>
        </nav>

        <div class="nav-actions">
          <button class="icon-btn" type="button" :aria-label="currentTheme === 'dark' ? 'Dark theme active' : 'Light theme active'" @click="toggleTheme()">
            <Moon v-if="currentTheme === 'dark'" :size="16" />
            <Sun v-else :size="16" />
          </button>
          <button class="lang-btn" type="button" @click="setLanguage(isVi ? 'en' : 'vi')">
            <Languages :size="15" /> {{ isVi ? 'VI' : 'EN' }}
          </button>

          <button v-if="authenticated" type="button" class="user-chip desktop-only" @click="go('/dashboard')">
            <span class="avatar">{{ initials }}</span>
            <span class="user-meta"><b>{{ displayName }}</b><small>{{ workspaceName }}</small></span>
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
        <button class="btn btn-primary" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
          {{ authenticated ? copy.launch : copy.start }}
        </button>
      </nav>
    </header>

    <main>
      <section class="hero section-dark">
        <div class="hero-ambient ambient-left" aria-hidden="true"></div>
        <div class="hero-ambient ambient-right" aria-hidden="true"></div>

        <div class="shell hero-grid">
          <div class="hero-copy" data-reveal>
            <div class="eyebrow"><Sparkles :size="14" /> {{ copy.badge }}</div>
            <div class="headline-wrap hero-headline-wrap">
              <h1 class="editorial-headline" :aria-label="copy.title">
                <span v-for="(line, lineIndex) in editorialHeadlines.hero" :key="lineIndex" class="headline-line">
                <span
                  v-for="(part, partIndex) in line"
                  :key="partIndex"
                  :class="part.tone ? `tone-${part.tone}` : null"
                >{{ part.text }}</span>
                </span>
              </h1>
              <span class="headline-glyph hero-glyph" aria-hidden="true"><Sparkles :size="21" /></span>
            </div>
            <p class="lead">{{ copy.intro }}</p>
            <div class="hero-actions">
              <button class="btn btn-primary glow-btn" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
                {{ authenticated ? copy.launch : copy.start }} <ArrowRight :size="17" />
              </button>
              <button class="btn btn-secondary" type="button" @click="go('#video')">
                {{ copy.demo }} <ArrowRight :size="15" />
              </button>
            </div>
            <div class="proof-row">
              <span v-for="item in copy.proof" :key="item"><Check :size="14" /> {{ item }}</span>
            </div>
          </div>

          <div class="hero-stage" data-reveal>
            <div class="wire-sphere" aria-hidden="true"></div>
            <div class="hero-platform" aria-hidden="true"></div>
            <div class="dashboard-tilt spotlight-card" @pointermove="setSpotlight">
              <div class="dashboard-window">
                <div class="window-top">
                  <span><i></i><i></i><i></i></span>
                  <small>Project workspace</small>
                </div>
                <img src="/landing/sprinta-dashboard-real.png" alt="SprintA workspace dashboard" />
              </div>

              <div class="float-card float-card-a">
                <span class="mini-icon"><Layers3 :size="16" /></span>
                <div><b>{{ isVi ? 'Project workspace' : 'Project workspace' }}</b><small>{{ isVi ? 'Công việc rõ ràng' : 'Clear ownership' }}</small></div>
              </div>
              <div class="float-card float-card-b">
                <span class="mini-icon"><Bot :size="16" /></span>
                <div><b>{{ isVi ? 'AI Copilot' : 'AI Copilot' }}</b><small>{{ isVi ? 'Gợi ý có xác nhận' : 'Confirmed suggestions' }}</small></div>
              </div>
              <div class="float-card float-card-c">
                <BarChart3 :size="17" />
                <strong>+18%</strong>
                <small>{{ isVi ? 'tiến độ sprint' : 'sprint momentum' }}</small>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section id="features" class="section product-section">
        <div class="shell">
          <div class="center-heading" data-reveal>
            <div class="eyebrow centered"><Layers3 :size="14" /> PRODUCT SUITE</div>
            <h2 class="product-title"><span>{{ landingHeadlines.products[0] }}</span> <em class="tone-warm">{{ landingHeadlines.products[1] }}</em> <span>{{ landingHeadlines.products[2] }}</span><span class="heading-glyph" aria-hidden="true"><Layers3 :size="18" /></span></h2>
            <p>{{ copy.productsIntro }}</p>
          </div>

          <div class="product-grid">
            <article
              v-for="(product, index) in products"
              :key="product.route"
              class="product-card spotlight-card"
              data-reveal
              @pointermove="setSpotlight"
            >
              <button class="card-link" type="button" :aria-label="`${copy.open}: ${product.name}`" @click="go(product.route)">
                <span class="product-visual" :class="`visual-${index + 1}`" aria-hidden="true">
                  <span class="iso-shadow"></span>
                  <span class="iso-platform back"></span>
                  <span class="iso-platform front"></span>
                  <span class="iso-object">
                    <component :is="product.icon" :size="58" stroke-width="1.6" />
                  </span>
                  <span class="data-chip chip-one"></span>
                  <span class="data-chip chip-two"></span>
                </span>
                <span class="product-copy">
                  <strong>{{ product.name }}</strong>
                  <small>{{ product.detail }}</small>
                  <span class="product-arrow"><ArrowRight :size="15" /></span>
                </span>
              </button>
            </article>
          </div>
        </div>
      </section>

      <section id="ai" class="section ai-section">
        <div class="ai-nebula" aria-hidden="true"></div>
        <div class="shell ai-grid">
          <div class="ai-copy" data-reveal>
            <div class="eyebrow"><Bot :size="14" /> {{ isVi ? 'AI ASSISTANT' : 'AI ASSISTANT' }}</div>
            <div class="headline-wrap ai-headline-wrap">
              <h2 class="editorial-headline ai-title">
                <span v-for="(line, lineIndex) in editorialHeadlines.ai" :key="lineIndex" class="headline-line">
                  <span v-for="(part, partIndex) in line" :key="partIndex" :class="part.tone ? `tone-${part.tone}` : null">{{ part.text }}</span>
                </span>
              </h2>
              <span class="headline-glyph ai-glyph" aria-hidden="true"><Sparkles :size="18" /></span>
            </div>
            <p class="section-copy">{{ copy.aiIntro }}</p>

            <div class="ai-steps">
              <article>
                <span class="step-icon"><FileText :size="24" /></span>
                <b>01</b>
                <strong>{{ isVi ? 'Đọc ngữ cảnh' : 'Read context' }}</strong>
                <small>{{ isVi ? 'Tóm tắt project đang mở' : 'Summarize current project' }}</small>
              </article>
              <article>
                <span class="step-icon"><Target :size="24" /></span>
                <b>02</b>
                <strong>{{ isVi ? 'Xem tác động' : 'Preview impact' }}</strong>
                <small>{{ isVi ? 'Hiện action trước khi chạy' : 'Show actions before run' }}</small>
              </article>
              <article>
                <span class="step-icon"><ShieldCheck :size="24" /></span>
                <b>03</b>
                <strong>{{ isVi ? 'Bạn quyết định' : 'You decide' }}</strong>
                <small>{{ isVi ? 'Không tự sửa dữ liệu' : 'Never changes data alone' }}</small>
              </article>
            </div>

            <div class="ai-copy-actions">
              <button class="btn btn-primary glow-btn" type="button" @click="go(authenticated ? '/ai' : '/login')">
                <Sparkles :size="16" /> {{ copy.aiButton }} <ArrowRight :size="16" />
              </button>
              <span class="trust-note"><ShieldCheck :size="15" /> {{ isVi ? 'An toàn · Minh bạch · Bạn kiểm soát' : 'Safe · Transparent · You control' }}</span>
            </div>
          </div>

          <div class="ai-showcase" data-reveal>
            <div class="ai-rings" aria-hidden="true"><i></i><i></i><i></i></div>
            <div class="assistant-panel">
              <div class="assistant-head">
                <div class="assistant-brand">
                  <img src="/sprinta-mark-light.png" alt="" />
                  <div><b>AI Assistant</b><small><i></i>{{ isVi ? 'Sẵn sàng' : 'Ready' }}</small></div>
                </div>
                <span class="control-pill"><ShieldCheck :size="14" /> {{ isVi ? 'Chỉ bạn quyết định' : 'You decide' }}</span>
              </div>

              <div class="chat-line user-line">
                <span class="chat-avatar"><Users :size="15" /></span>
                <div>
                  <small>{{ isVi ? 'Bạn' : 'You' }}</small>
                  <p>{{ isVi ? 'Tạo thêm field “Độ ưu tiên” cho bảng Tasks và hiển thị ở Board.' : 'Add a Priority field to Tasks and show it on the Board.' }}</p>
                </div>
              </div>

              <div class="chat-line ai-line">
                <span class="chat-avatar brand-avatar"><Bot :size="15" /></span>
                <div class="analysis-card">
                  <small>SprintA AI</small>
                  <p>{{ isVi ? 'Đã hiểu yêu cầu. Dưới đây là các thay đổi dự kiến:' : 'Request understood. Here is the planned impact:' }}</p>

                  <div class="impact-list">
                    <div><span><FileText :size="15" /></span><b>{{ isVi ? 'Thêm column mới' : 'Add new column' }}</b><small>tasks.priority (enum)</small><em>NEW</em></div>
                    <div><span><KanbanSquare :size="15" /></span><b>{{ isVi ? 'Cập nhật Board' : 'Update Board' }}</b><small>{{ isVi ? 'Hiển thị “Độ ưu tiên” ở cột' : 'Show Priority in columns' }}</small><em>UPDATE</em></div>
                    <div><span><BarChart3 :size="15" /></span><b>{{ isVi ? 'Ảnh hưởng' : 'Impact' }}</b><small>3 views · 2 filters · 1 report</small><em>IMPACT</em></div>
                  </div>
                </div>
              </div>

              <div class="confirm-card">
                <div><span class="confirm-icon"><ShieldCheck :size="19" /></span><b>{{ isVi ? 'Chờ xác nhận' : 'Waiting for confirmation' }}</b></div>
                <p>{{ isVi ? 'AI sẽ không thực hiện khi chưa được bạn xác nhận.' : 'AI will not execute until you confirm.' }}</p>
                <div class="confirm-actions">
                  <button type="button" class="btn btn-secondary"><X :size="15" /> {{ isVi ? 'Hủy bỏ' : 'Cancel' }}</button>
                  <button type="button" class="btn btn-primary"><Check :size="15" /> {{ isVi ? 'Xác nhận & Áp dụng' : 'Confirm & Apply' }}</button>
                </div>
              </div>
            </div>

            <div class="mascot-stage">
              <div class="mascot-platform" aria-hidden="true"></div>
              <img src="/ai-sprinta/guide.png" :alt="isVi ? 'Mascot SprintA đang chỉ vào bảng AI' : 'SprintA mascot pointing to the AI panel'" />
            </div>
            <div class="ai-float project-float"><Layers3 :size="15" /><div><b>Project: SprintA</b><small>28 files · 6 modules · 12 tests</small></div></div>
            <div class="ai-float engine-float"><Sparkles :size="15" /><div><b>Context Engine</b><small>{{ isVi ? 'Đang phân tích...' : 'Analyzing...' }}</small></div></div>
          </div>
        </div>
      </section>

      <section id="pricing" class="section pricing-section">
        <div class="shell">
          <div class="center-heading pricing-heading" data-reveal>
            <div class="eyebrow centered"><Coins :size="14" /> {{ isVi ? 'BẢNG GIÁ SPRINTA' : 'SPRINTA PRICING' }}</div>
            <h2><span>{{ landingHeadlines.pricing[0] }}</span> <em class="tone-mint">{{ landingHeadlines.pricing[1] }}</em></h2>
            <p>{{ copy.pricingIntro }}</p>
            <div class="pricing-controls" role="group" :aria-label="isVi ? 'Tùy chọn bảng giá' : 'Pricing options'">
              <span class="billing-pill"><Coins :size="14" /> {{ copy.monthly }}</span>
              <button
                type="button"
                class="billing-pill benefits-toggle"
                :class="{ active: showPlanBenefits }"
                :aria-pressed="showPlanBenefits"
                @click="showPlanBenefits = !showPlanBenefits"
              >
                <ShieldCheck :size="14" /> {{ copy.serverPricing }}
              </button>
            </div>
          </div>

          <div v-if="pricing?.plans?.length" class="pricing-grid">
            <article
              v-for="plan in pricing.plans"
              :key="plan.id || plan.code || plan.name"
              class="price-card spotlight-card"
              :class="{ featured: isFeaturedPlan(plan) }"
              data-reveal
              @pointermove="setSpotlight"
            >
              <span v-if="isFeaturedPlan(plan)" class="recommended-badge"><Sparkles :size="13" /> {{ copy.popular }}</span>
              <div class="price-card-head">
                <span class="price-icon"><component :is="planIcon(plan)" :size="18" /></span>
                <small>{{ planCode(plan).toUpperCase() }}</small>
              </div>
              <h3>{{ plan.name }}</h3>
              <div class="price-value" :class="{ pending: plan.monthlyPriceVnd == null }">
                <strong>{{ priceLabel(plan) }}</strong>
                <span v-if="plan.monthlyPriceVnd != null">{{ copy.perMonth }}<template v-if="plan.perUser"> {{ copy.perUser }}</template></span>
              </div>
              <p class="price-status"><i></i>{{ plan.monthlyPriceVnd == null ? copy.pending : copy.transparentPricing }}</p>
              <button class="plan-cta" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
                {{ copy.choosePlan }} {{ plan.name }} <ArrowRight :size="15" />
              </button>
              <div v-show="showPlanBenefits" class="feature-list">
                <div v-for="feature in planFeatures(plan)" :key="feature" class="price-line"><span><Check :size="13" /></span>{{ feature }}</div>
                <div v-if="!planFeatures(plan).length" class="price-line muted"><span><ShieldCheck :size="13" /></span>{{ copy.serverPricing }}</div>
              </div>
            </article>
          </div>

          <div v-else-if="pricing" class="pricing-empty" data-reveal>
            <Coins :size="22" />
            <div><b>{{ copy.plansPending }}</b><p>{{ copy.plansPendingDetail }}</p></div>
          </div>
          <div v-else-if="pricingError" class="api-state">{{ copy.apiFail }}</div>
        </div>
      </section>

      <section id="video" class="section video-section" data-reveal>
        <ProductVideoSection
          :title="isVi ? 'SprintA trong một luồng làm việc thật' : 'SprintA in a real workflow'"
          :intro="isVi ? 'Xem cách SprintA gom công việc, AI và báo cáo vào một luồng làm việc thống nhất.' : 'See how SprintA brings work, AI and reporting into one connected flow.'"
        >
          <template #title>
            <span v-for="(line, lineIndex) in editorialHeadlines.video" :key="lineIndex" class="headline-line">
              <span v-for="(part, partIndex) in line" :key="partIndex" :class="part.tone ? `tone-${part.tone}` : null">{{ part.text }}</span>
            </span>
          </template>
        </ProductVideoSection>
      </section>

      <section id="workflow" class="section workflow-section">
        <div class="shell">
          <div class="center-heading workflow-heading" data-reveal>
            <div class="eyebrow centered"><Workflow :size="14" /> OPERATING FLOW</div>
            <h2 class="editorial-headline">
              <span v-for="(line, lineIndex) in editorialHeadlines.workflow" :key="lineIndex" class="headline-line">
                <span v-for="(part, partIndex) in line" :key="partIndex" :class="part.tone ? `tone-${part.tone}` : null">{{ part.text }}</span>
              </span>
            </h2>
          </div>

          <div class="workflow-track" data-reveal>
            <span class="flow-line" aria-hidden="true"></span>
            <span class="flow-arrow" aria-hidden="true"><ArrowRight :size="22" /></span>
            <span class="flow-signal" aria-hidden="true"></span>
            <article
              v-for="(step, index) in workflowSteps"
              :key="step.title"
              class="flow-step"
              :class="index % 2 === 0 ? 'is-above' : 'is-below'"
            >
              <div class="flow-copy-card">
                <small>0{{ index + 1 }}</small>
                <b>{{ step.title }}</b>
                <p>{{ step.detail }}</p>
              </div>
              <span class="flow-stem" aria-hidden="true"></span>
              <div class="flow-node"><component :is="step.icon" :size="20" /></div>
            </article>
          </div>

          <div class="final-cta" data-reveal>
            <div class="cta-mascot"><span></span><img src="/ai-sprinta/idle.png" alt="" /></div>
            <h2><span>{{ landingHeadlines.cta[0] }}</span> <em class="tone-cyan">{{ landingHeadlines.cta[1] }}</em></h2>
            <button class="btn btn-primary glow-btn" type="button" @click="go(authenticated ? '/dashboard' : '/register')">
              {{ authenticated ? copy.launch : (isVi ? 'Bắt đầu miễn phí ngay' : 'Start free now') }} <ArrowRight :size="17" />
            </button>
          </div>
        </div>
      </section>

      <section class="section faq-section">
        <div class="shell faq-grid" data-reveal>
          <div class="faq-heading">
            <div class="eyebrow"><CircleHelp :size="14" /> FAQ</div>
            <h2><span>{{ landingHeadlines.faq[0] }}</span><em>{{ landingHeadlines.faq[1] }}</em></h2>
            <p>{{ isVi ? 'Những câu hỏi quan trọng trước khi bắt đầu với SprintA.' : 'The key things to know before getting started with SprintA.' }}</p>
          </div>
          <div class="faq-list">
            <article v-for="(faq, index) in faqs" :key="faq[0]" class="faq-item">
              <button type="button" :aria-expanded="activeFaq === index" @click="activeFaq = activeFaq === index ? -1 : index">
                <span>{{ faq[0] }}</span><ChevronDown :size="17" :class="{ rotate: activeFaq === index }" />
              </button>
              <p v-if="activeFaq === index">{{ faq[1] }}</p>
            </article>
          </div>
        </div>
      </section>
    </main>

    <footer class="footer">
      <div class="shell footer-panel">
        <div class="footer-brand">
          <router-link to="/" class="brand"><img class="brand-logo" src="/sprinta-mark-light.png" alt="" /><span class="brand-word">SprintA</span></router-link>
          <p>{{ isVi ? 'Agile workspace giúp đội nhóm làm việc tập trung, minh bạch và bứt phá.' : 'An agile workspace for focused, transparent, high-performing teams.' }}</p>
        </div>
        <div class="footer-col"><b>{{ isVi ? 'Sản phẩm' : 'Product' }}</b><a href="#features">{{ copy.nav[0] }}</a><a href="#ai">AI Assistant</a><a href="#pricing">{{ copy.nav[3] }}</a></div>
        <div class="footer-col"><b>{{ isVi ? 'Tài nguyên' : 'Resources' }}</b><a href="#video">{{ copy.nav[4] }}</a><a href="#workflow">{{ copy.nav[2] }}</a><a href="#features">{{ isVi ? 'Tính năng' : 'Features' }}</a></div>
        <div class="footer-col"><b>{{ isVi ? 'Bắt đầu' : 'Get started' }}</b><router-link to="/login">{{ copy.signIn }}</router-link><button type="button" @click="go(authenticated ? '/dashboard' : '/register')">{{ authenticated ? copy.launch : copy.start }}</button></div>
        <div class="footer-bottom"><span>© 2026 SprintA</span><span>{{ isVi ? 'Quản lý công việc rõ ràng hơn.' : 'Make work visible.' }}</span></div>
      </div>
    </footer>
  </div>
</template>


<style scoped>
:global(*) { box-sizing: border-box; }
:global(html) { scroll-behavior: smooth; }
:global(body) { margin: 0; background: #020b17; }

.landing-page {
  --bg: #020b17;
  --bg-2: #03101f;
  --bg-3: #06172a;
  --surface: #081a2f;
  --surface-2: #0a2238;
  --surface-3: #0d2a44;
  --line: rgba(100, 202, 255, .18);
  --line-strong: rgba(77, 202, 255, .42);
  --ink: #f7fbff;
  --ink-2: #bfd3e6;
  --muted: #88a4be;
  --cyan: #45cfff;
  --mint: #42e6bd;
  --blue: #4b7dff;
  --deep-blue: #2457e6;
  --warm: #ffb247;
  --danger-warm: #ff6a57;
  --tone-cyan-a: #52d6ff;
  --tone-cyan-b: #2f8dff;
  --tone-mint-a: #48e4bf;
  --tone-mint-b: #16c99d;
  --tone-blue-a: #7aa4ff;
  --tone-blue-b: #4f67ff;
  --tone-warm-a: #ffc05a;
  --tone-warm-b: #ff8a3d;
  --tone-coral-a: #ff7a64;
  --tone-coral-b: #ff4f67;
  --shadow: 0 28px 80px rgba(0, 4, 18, .48);
  min-height: 100vh;
  overflow-x: clip;
  background:
    radial-gradient(circle at 50% -10%, rgba(43, 123, 255, .13), transparent 34rem),
    linear-gradient(180deg, #020b17 0%, #03101f 56%, #020b17 100%);
  font-family: Inter, "Avenir Next", ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  -webkit-font-smoothing: antialiased;
  text-rendering: optimizeLegibility;
}

.landing-page.is-light {
  --bg: #eef6fc;
  --bg-2: #f8fbfe;
  --bg-3: #e4eef7;
  --surface: rgba(255,255,255,.88);
  --surface-2: #ffffff;
  --surface-3: #edf4fa;
  --line: rgba(29, 78, 121, .16);
  --line-strong: rgba(12, 119, 174, .36);
  --ink: #0e2238;
  --ink-2: #36536d;
  --muted: #667f96;
  --cyan: #0b82bd;
  --mint: #0c987f;
  --blue: #2859d8;
  --deep-blue: #1e4fd0;
  --warm: #bd6500;
  --danger-warm: #d94b3c;
  --tone-cyan-a: #087ab8;
  --tone-cyan-b: #1764d7;
  --tone-mint-a: #078b73;
  --tone-mint-b: #0ca477;
  --tone-blue-a: #2859d8;
  --tone-blue-b: #5146d9;
  --tone-warm-a: #b65c00;
  --tone-warm-b: #d97900;
  --tone-coral-a: #cf4037;
  --tone-coral-b: #e05245;
  color: var(--ink);
  background:
    radial-gradient(circle at 48% -10%, rgba(59,145,220,.12), transparent 34rem),
    linear-gradient(180deg,#f7fbff 0%,#edf5fb 55%,#e8f1f8 100%);
}

a { color: inherit; text-decoration: none; }
button, a { -webkit-tap-highlight-color: transparent; }
button { font: inherit; }

.shell {
  width: min(1320px, calc(100% - 64px));
  margin-inline: auto;
}

.section { position: relative; padding: 88px 0; }
.section-dark { position: relative; }
.section-copy { color: var(--ink-2); }

.landing-nav {
  position: sticky;
  top: 16px;
  z-index: 60;
  width: min(1320px, calc(100% - 40px));
  margin: 16px auto 0;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: color-mix(in srgb, var(--surface) 86%, transparent);
  box-shadow: 0 14px 44px rgba(0,0,0,.18), inset 0 1px rgba(255,255,255,.04);
  backdrop-filter: blur(22px) saturate(125%);
}

.scroll-progress {
  position: absolute;
  left: 0;
  top: -1px;
  width: 100%;
  height: 2px;
  transform-origin: left;
  border-radius: 999px;
  background: linear-gradient(90deg, var(--mint), var(--cyan), var(--blue));
}

.nav-inner {
  min-height: 68px;
  padding: 0 18px;
  display: flex;
  align-items: center;
  gap: 28px;
}

.brand { display: inline-flex; align-items: center; gap: 10px; min-width: max-content; }
.brand-logo { width: 26px; height: 26px; object-fit: contain; filter: drop-shadow(0 0 12px rgba(65,192,242,.32)); }
.brand-word { font-weight: 850; letter-spacing: .025em; }
.brand-word small { margin-left: 5px; color: var(--cyan); font-size: 10px; letter-spacing: .19em; }

.desktop-nav { display: flex; gap: 26px; margin-inline: auto; }
.desktop-nav a, .text-btn {
  color: var(--ink-2);
  font-size: 13px;
  font-weight: 700;
  transition: color .22s ease;
}
.desktop-nav a:hover, .text-btn:hover { color: var(--ink); }

.nav-actions { display: flex; align-items: center; gap: 10px; }
.icon-btn, .lang-btn, .text-btn, .user-chip {
  min-height: 38px;
  border: 1px solid var(--line);
  border-radius: 10px;
  color: var(--ink-2);
  background: rgba(2, 12, 25, .36);
}
.icon-btn {
  width: 38px;
  display: inline-grid;
  place-items: center;
  cursor: pointer;
}
.lang-btn {
  padding: 0 12px;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: var(--cyan);
  cursor: pointer;
}
.text-btn { padding: 0 8px; display: inline-flex; align-items: center; border: 0; background: transparent; cursor: pointer; }
.btn:hover { transform: translateY(-2px); border-color: var(--line-strong); }
.btn-primary {
  border-color: rgba(84, 212, 255, .62);
  background: linear-gradient(135deg, #0d8fe9 0%, #176be7 54%, #1d54d4 100%);
  box-shadow: inset 0 1px rgba(255,255,255,.2), 0 12px 30px rgba(15,105,228,.26);
}
.glow-btn:hover { box-shadow: inset 0 1px rgba(255,255,255,.24), 0 14px 38px rgba(25,125,255,.4), 0 0 28px rgba(65,192,242,.15); }
.btn-secondary { background: rgba(9, 28, 50, .68); }

.eyebrow {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 14px;
  color: var(--cyan);
  font-size: 11px;
  font-weight: 900;
  letter-spacing: .18em;
  text-transform: uppercase;
}
.eyebrow.centered { justify-content: center; }

.headline-wrap { position:relative; width:max-content; max-width:100%; }
.headline-glyph, .heading-glyph {
  display:inline-grid;
  place-items:center;
  color:var(--cyan);
  filter:drop-shadow(0 0 10px rgba(65,192,242,.24));
}
.hero-glyph { position:absolute; right:-30px; top:4px; animation:glyphFloat 4.5s ease-in-out infinite; }
.ai-glyph { position:absolute; right:-26px; top:2px; color:var(--mint); animation:glyphFloat 5s ease-in-out infinite reverse; }
.heading-glyph { margin-left:10px; vertical-align:.12em; }
.product-title { max-width:980px; margin-inline:auto !important; }
.product-title > span, .product-title > em { display:inline; }

.editorial-headline, h1, h2 {
  overflow: visible;
}
.headline-line {
  display: block;
  overflow: visible;
  padding-block: .04em .08em;
}
.tone-cyan, .tone-mint, .tone-blue, .tone-warm, .tone-coral {
  color: transparent;
  -webkit-background-clip: text;
  background-clip: text;
}
.tone-cyan { background-image: linear-gradient(90deg,var(--tone-cyan-a),var(--tone-cyan-b)); }
.tone-mint { background-image: linear-gradient(90deg,var(--tone-mint-a),var(--tone-mint-b)); }
.tone-blue { background-image: linear-gradient(90deg,var(--tone-blue-a),var(--tone-blue-b)); }
.tone-warm { background-image: linear-gradient(90deg,var(--tone-warm-a),var(--tone-warm-b)); }
.tone-coral { background-image: linear-gradient(90deg,var(--tone-coral-a),var(--tone-coral-b)); }

.hero {
  min-height: 650px;
  display: grid;
  align-items: center;
  padding: 82px 0 78px;
}
.hero-grid {
  position: relative;
  z-index: 2;
  display: grid;
  grid-template-columns: minmax(0, .94fr) minmax(520px, 1.06fr);
  gap: 52px;
  align-items: center;
}
.hero-ambient {
  position: absolute;
  border-radius: 50%;
  filter: blur(0);
  pointer-events: none;
}
.ambient-left {
  width: 560px; height: 560px; left: -260px; top: 80px;
  background: radial-gradient(circle, rgba(29, 163, 216, .12), transparent 68%);
}
.ambient-right {
  width: 720px; height: 720px; right: -240px; top: 40px;
  background: radial-gradient(circle, rgba(34, 98, 232, .14), transparent 64%);
}
.hero-copy h1 {
  margin: 0;
  max-width: 690px;
  font-size: clamp(46px, 3.55vw, 64px);
  line-height: 1.07;
  letter-spacing: -.052em;
  font-weight: 900;
}
.hero-copy .lead {
  max-width: 660px;
  margin: 20px 0 0;
  color: var(--ink-2);
  font-size: 15.5px;
  line-height: 1.72;
}
.hero-actions { display: flex; gap: 12px; margin-top: 28px; }
.proof-row { display: flex; flex-wrap: wrap; gap: 20px; margin-top: 28px; color: var(--muted); font-size: 12px; font-weight: 700; }
.proof-row svg { color: var(--mint); }

.hero-stage {
  position: relative;
  min-height: 500px;
  perspective: 1500px;
  display: grid;
  place-items: center;
}
.dashboard-tilt {
  position: relative;
  z-index: 3;
  width: min(650px, 100%);
  transform: rotateX(4deg) rotateY(-7deg) translateZ(0);
  transform-style: preserve-3d;
  transition: transform .45s cubic-bezier(.2,.8,.2,1);
}
.dashboard-tilt:hover { transform: rotateX(2deg) rotateY(-3deg) translateY(-6px); }
.dashboard-window {
  overflow: hidden;
  border: 1px solid rgba(96, 213, 255, .52);
  border-radius: 20px;
  background: #071526;
  box-shadow:
    0 42px 90px rgba(0, 4, 20, .58),
    0 0 54px rgba(28, 150, 255, .12),
    inset 0 1px rgba(255,255,255,.1);
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

.window-top {
  height: 42px;
  padding: 0 15px;
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--muted);
  background: linear-gradient(180deg, rgba(15,37,64,.95), rgba(8,25,44,.95));
  border-bottom: 1px solid var(--line);
}
.window-top > span { display: inline-flex; gap: 6px; }
.window-top i { width: 8px; height: 8px; border-radius: 50%; background: var(--cyan); opacity: .85; }
.window-top i:nth-child(2) { background: #39d2b0; }
.window-top i:nth-child(3) { background: #2f7cff; }
.dashboard-window img { display: block; width: 100%; height: auto; }
.hero-platform {
  position: absolute;
  z-index: 1;
  width: 82%;
  height: 100px;
  left: 8%;
  bottom: 18%;
  border-radius: 50%;
  background:
    radial-gradient(ellipse at center, rgba(75, 211, 255, .22) 0 25%, rgba(17, 93, 220, .2) 40%, transparent 72%);
  border: 2px solid rgba(65,192,242,.36);
  box-shadow: 0 0 36px rgba(65,192,242,.2), inset 0 0 38px rgba(47,124,255,.18);
  transform: rotateX(69deg);
}
.wire-sphere {
  position: absolute;
  width: 270px; height: 270px;
  right: 22px; top: 52px;
  border-radius: 50%;
  opacity: .22;
  background:
    repeating-radial-gradient(circle at center, transparent 0 17px, rgba(65,192,242,.55) 18px 19px, transparent 20px 32px),
    repeating-linear-gradient(45deg, transparent 0 18px, rgba(65,192,242,.24) 19px 20px, transparent 21px 38px);
  filter: drop-shadow(0 0 22px rgba(65,192,242,.22));
  animation: sphereFloat 8s ease-in-out infinite;
}
.float-card {
  position: absolute;
  z-index: 7;
  min-width: 160px;
  padding: 11px 13px;
  border: 1px solid rgba(90, 214, 255, .32);
  border-radius: 11px;
  display: flex;
  align-items: center;
  gap: 9px;
  background: rgba(7, 29, 53, .9);
  box-shadow: 0 18px 40px rgba(0,0,0,.32), inset 0 1px rgba(255,255,255,.06);
  backdrop-filter: blur(14px);
}
.float-card b { display:block; font-size: 11px; }
.float-card small { display:block; margin-top:2px; color:var(--muted); font-size:9px; }
.mini-icon { width: 30px; height: 30px; display:grid; place-items:center; border-radius:8px; color:var(--cyan); background:rgba(65,192,242,.12); }
.float-card-a { left: -38px; top: 22%; transform: translateZ(48px); }
.float-card-b { right: -42px; top: 29%; transform: translateZ(64px); }
.float-card-c { right: 12px; bottom: -24px; min-width: 150px; color: var(--cyan); }
.float-card-c strong { margin-left:auto; color:var(--ink); }

.center-heading {
  max-width: 1040px;
  margin: 0 auto 38px;
  text-align: center;
}
.center-heading h2 {
  margin: 0;
  font-size: clamp(34px, 3vw, 50px);
  line-height: 1.1;
  letter-spacing: -.045em;
}
.center-heading h2 em { font-style: normal; }
.center-heading p { max-width: 690px; margin: 15px auto 0; color: var(--ink-2); line-height: 1.72; }

.product-section { background: linear-gradient(180deg, #061426, #04111f 82%); }
.product-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0,1fr));
  gap: 18px;
}
.product-card {
  position: relative;
  min-height: 300px;
  border: 1px solid var(--line);
  border-radius: 18px;
  overflow: hidden;
  background:
    linear-gradient(180deg, rgba(15, 43, 72, .78), rgba(7, 25, 46, .92));
  box-shadow: 0 20px 50px rgba(0,0,0,.2), inset 0 1px rgba(255,255,255,.05);
  transition: transform .28s ease, border-color .28s ease, box-shadow .28s ease;
}
.product-card::after,
.spotlight-card::after {
  content:'';
  position:absolute;
  inset:0;
  pointer-events:none;
  background: radial-gradient(330px circle at var(--spot-x, 50%) var(--spot-y, 50%), rgba(65,192,242,.10), transparent 52%);
  opacity:.7;
}
.product-card:hover {
  transform: translateY(-6px);
  border-color: rgba(82,211,255,.42);
  box-shadow: 0 28px 64px rgba(0,0,0,.32), 0 0 30px rgba(65,192,242,.08), inset 0 1px rgba(255,255,255,.07);
}
.card-link {
  position:relative;
  z-index:2;
  width:100%;
  height:100%;
  padding: 26px 25px 24px;
  border:0;
  color:inherit;
  background:transparent;
  text-align:left;
  cursor:pointer;
}
.product-visual {
  position: relative;
  height: 185px;
  display: grid;
  place-items: center;
  perspective: 900px;
  transform-style: preserve-3d;
}
.iso-shadow {
  position:absolute;
  width:150px; height:42px;
  top:116px;
  border-radius:50%;
  background:radial-gradient(ellipse, rgba(43,154,255,.38), transparent 66%);
  filter:blur(5px);
}
.iso-platform {
  position:absolute;
  width:145px; height:96px;
  border:1px solid rgba(100,218,255,.45);
  border-radius:16px;
  transform:rotateX(63deg) rotateZ(-1deg);
  background:linear-gradient(145deg, rgba(41,119,211,.42), rgba(12,39,75,.8));
  box-shadow:0 0 26px rgba(65,192,242,.18), inset 0 1px rgba(255,255,255,.12);
}
.iso-platform.back { transform:translateY(17px) rotateX(63deg); opacity:.45; }
.iso-platform.front { transform:translateY(5px) rotateX(63deg); }
.iso-object {
  position:relative;
  z-index:4;
  width:94px; height:94px;
  display:grid; place-items:center;
  border:1px solid rgba(102,221,255,.44);
  border-radius:18px;
  color:#8ce8ff;
  background:linear-gradient(145deg, rgba(35,101,170,.92), rgba(11,37,70,.94));
  box-shadow:0 22px 42px rgba(0,0,0,.38), inset 0 1px rgba(255,255,255,.16), 0 0 28px rgba(65,192,242,.16);
  transform:translateY(-10px) rotateX(-5deg) rotateY(-12deg);
}
.visual-2 .iso-object { border-radius:50%; transform:translateY(-10px) rotateX(-4deg) rotateY(8deg); }
.visual-3 .iso-object { transform:translateY(-8px) rotateX(-4deg) rotateY(-9deg) rotateZ(2deg); }
.visual-4 .iso-object { transform:translateY(-10px) rotateX(-7deg) rotateY(10deg); }
.visual-5 .iso-object { border-radius:14px 14px 4px 4px; }
.visual-6 .iso-object { border-radius:28px; }
.data-chip { position:absolute; z-index:5; width:30px; height:20px; border:1px solid rgba(115,229,255,.42); border-radius:6px; background:rgba(15,70,120,.72); box-shadow:0 8px 18px rgba(0,0,0,.28); }
.chip-one { left:calc(50% - 70px); top:46px; transform:rotate(-16deg); }
.chip-two { right:calc(50% - 70px); top:76px; transform:rotate(15deg); }
.product-copy { display:block; position:relative; padding-right:30px; }
.product-copy strong { display:block; font-size:18px; }
.product-copy small { display:block; min-height:48px; margin-top:8px; color:var(--ink-2); line-height:1.55; }
.product-arrow { position:absolute; right:0; top:2px; color:var(--cyan); }

.ai-section {
  overflow:hidden;
  background:
    radial-gradient(circle at 68% 50%, rgba(23,105,255,.13), transparent 34rem),
    linear-gradient(180deg, #020b17, #031226 60%, #020b17);
}
.ai-nebula { position:absolute; width:900px; height:560px; right:-180px; top:120px; border-radius:50%; background:radial-gradient(circle,rgba(65,192,242,.08),transparent 64%); }
.ai-grid {
  position:relative;
  z-index:2;
  display:grid;
  grid-template-columns:minmax(380px,.82fr) minmax(560px,1.18fr);
  gap:52px;
  align-items:center;
}
.ai-title {
  margin:0;
  font-size:clamp(40px,3.45vw,58px);
  line-height:1.08;
  letter-spacing:-.048em;
}
.ai-copy .section-copy { max-width:540px; margin:20px 0 0; line-height:1.7; }
.ai-steps { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:12px; margin-top:28px; }
.ai-steps article {
  min-height:154px;
  padding:18px;
  border:1px solid var(--line);
  border-radius:16px;
  background:rgba(9,31,55,.68);
  box-shadow:inset 0 1px rgba(255,255,255,.04);
}
.step-icon { width:46px; height:46px; display:grid; place-items:center; margin-bottom:12px; color:var(--cyan); border:1px solid rgba(65,192,242,.35); border-radius:50%; background:rgba(65,192,242,.08); }
.ai-steps b { color:var(--cyan); font-size:11px; letter-spacing:.08em; }
.ai-steps strong { display:block; margin-top:8px; font-size:14px; }
.ai-steps small { display:block; margin-top:6px; color:var(--muted); font-size:11px; line-height:1.45; }
.ai-copy-actions { display:flex; align-items:center; gap:16px; margin-top:24px; }
.trust-note { display:inline-flex; align-items:center; gap:6px; color:var(--muted); font-size:11px; }

.ai-showcase {
  position:relative;
  min-height:535px;
  display:grid;
  align-items:center;
}
.assistant-panel {
  position:relative;
  z-index:3;
  width:calc(100% - 150px);
  margin-right:150px;
  min-height:500px;
  padding:20px;
  border:1px solid rgba(86,211,255,.38);
  border-radius:20px;
  background:linear-gradient(180deg,rgba(9,34,59,.96),rgba(5,23,42,.98));
  box-shadow:0 38px 90px rgba(0,0,0,.44),0 0 55px rgba(47,124,255,.08),inset 0 1px rgba(255,255,255,.07);
}
.assistant-head { display:flex; align-items:center; justify-content:space-between; padding-bottom:14px; border-bottom:1px solid var(--line); }
.assistant-brand { display:flex; align-items:center; gap:10px; }
.assistant-brand img { width:34px; height:34px; object-fit:contain; }
.assistant-brand div { display:grid; gap:2px; }
.assistant-brand b { font-size:14px; }
.assistant-brand small { display:flex; align-items:center; gap:6px; color:var(--ink-2); font-size:10px; }
.assistant-brand small i { width:7px; height:7px; border-radius:50%; background:var(--mint); box-shadow:0 0 10px rgba(77,226,197,.5); }
.control-pill { display:inline-flex; align-items:center; gap:6px; padding:7px 10px; color:var(--mint); border:1px solid rgba(77,226,197,.25); border-radius:999px; background:rgba(77,226,197,.06); font-size:10px; font-weight:800; }

.chat-line { display:grid; grid-template-columns:28px 1fr; gap:9px; margin-top:15px; }
.chat-avatar { width:28px; height:28px; display:grid; place-items:center; color:var(--ink-2); border-radius:8px; background:rgba(255,255,255,.04); }
.brand-avatar { color:var(--cyan); }
.chat-line small { color:var(--muted); font-size:10px; font-weight:800; }
.user-line p { margin:6px 0 0 auto; max-width:84%; padding:12px 14px; color:var(--ink); border:1px solid rgba(47,124,255,.26); border-radius:14px 14px 4px 14px; background:rgba(20,73,156,.36); font-size:11px; line-height:1.5; }
.analysis-card { padding:14px; border:1px solid var(--line); border-radius:14px; background:rgba(11,38,65,.82); }
.analysis-card > p { margin:6px 0 10px; color:var(--ink-2); font-size:11px; }
.impact-list { display:grid; gap:7px; }
.impact-list > div {
  display:grid;
  grid-template-columns:28px 1fr auto;
  grid-template-rows:auto auto;
  gap:0 8px;
  align-items:center;
  padding:8px 10px;
  border:1px solid rgba(106,180,225,.13);
  border-radius:9px;
  background:rgba(3,17,31,.36);
}
.impact-list > div > span { grid-row:1/3; width:28px; height:28px; display:grid; place-items:center; color:var(--cyan); border-radius:7px; background:rgba(65,192,242,.08); }
.impact-list b { font-size:11px; }
.impact-list small { color:var(--muted); font-size:9px; }
.impact-list em { grid-row:1/3; grid-column:3; padding:4px 7px; color:var(--cyan); border:1px solid rgba(65,192,242,.2); border-radius:999px; background:rgba(65,192,242,.06); font-size:8px; font-style:normal; font-weight:900; }

.confirm-card { margin-top:15px; padding:13px; border:1px solid rgba(65,192,242,.28); border-radius:13px; background:rgba(4,20,36,.72); }
.confirm-card > div:first-child { display:flex; align-items:center; gap:8px; }
.confirm-icon { color:var(--cyan); }
.confirm-card p { margin:7px 0 11px; color:var(--muted); font-size:10px; }
.confirm-actions { display:flex; justify-content:flex-end; gap:8px; }
.confirm-actions .btn { min-height:34px; padding:0 12px; font-size:10px; }

.mascot-stage {
  position:absolute;
  z-index:6;
  right:-18px;
  bottom:74px;
  width:180px;
  height:226px;
  display:grid;
  place-items:end center;
  pointer-events:none;
}
.mascot-stage img { position:relative; z-index:3; width:172px; height:auto; transform:rotate(-2deg); transform-origin:70% 85%; filter:drop-shadow(0 18px 28px rgba(0,0,0,.45)) drop-shadow(0 0 20px rgba(65,192,242,.12)); animation:mascotGuide 5s ease-in-out infinite; }
.mascot-platform { position:absolute; bottom:0; width:166px; height:52px; border-radius:50%; background:radial-gradient(ellipse,rgba(65,218,255,.34),rgba(23,87,225,.18) 44%,transparent 70%); border:1px solid rgba(65,192,242,.42); box-shadow:0 0 28px rgba(65,192,242,.2); transform:rotateX(61deg); }
.ai-rings { position:absolute; z-index:1; right:-16px; bottom:56px; width:230px; height:230px; }
.ai-rings i { position:absolute; inset:0; border:1px solid rgba(65,192,242,.19); border-radius:50%; }
.ai-rings i:nth-child(2){ inset:26px; border-color:rgba(47,124,255,.22); animation:orbit 10s linear infinite; }
.ai-rings i:nth-child(3){ inset:55px; border-color:rgba(77,226,197,.18); animation:orbit 13s linear reverse infinite; }
.ai-float {
  position:absolute;
  z-index:7;
  display:flex;
  align-items:center;
  gap:8px;
  padding:10px 12px;
  border:1px solid rgba(89,207,255,.3);
  border-radius:10px;
  background:rgba(7,28,50,.92);
  box-shadow:0 14px 36px rgba(0,0,0,.35);
}
.ai-float svg { color:var(--cyan); }
.ai-float b { display:block; font-size:10px; }
.ai-float small { display:block; margin-top:2px; color:var(--muted); font-size:8px; }
.project-float { left:-25px; bottom:5px; }
.engine-float { right:0; top:68px; }

.pricing-section { background:linear-gradient(180deg,#04111f,#061526 50%,#020b17); }
.pricing-heading { margin-bottom:42px; }
.billing-pill { display:inline-flex; align-items:center; gap:7px; margin-top:20px; padding:8px 13px; border:1px solid var(--line); border-radius:999px; color:var(--cyan); background:rgba(65,192,242,.06); font-size:11px; font-weight:800; }
.pricing-grid { display:grid; grid-template-columns:repeat(3,minmax(0,1fr)); gap:18px; }
.price-card {
  position:relative;
  min-height:400px;
  padding:24px;
  border:1px solid var(--line);
  border-radius:18px;
  overflow:visible;
  background:linear-gradient(180deg,rgba(12,40,68,.88),rgba(6,24,43,.94));
  box-shadow:0 20px 50px rgba(0,0,0,.22),inset 0 1px rgba(255,255,255,.04);
}
.price-card.featured {
  border-color:rgba(65,204,255,.72);
  box-shadow:0 28px 70px rgba(5,73,174,.3),0 0 34px rgba(65,192,242,.12),inset 0 1px rgba(255,255,255,.07);
  transform:translateY(-6px);
}
.recommended-badge {
  position:absolute;
  top:-13px; left:50%;
  transform:translateX(-50%);
  display:inline-flex; align-items:center; gap:5px;
  padding:6px 10px;
  border:1px solid rgba(87,214,255,.6);
  border-radius:999px;
  color:white;
  background:linear-gradient(90deg,#136de0,#1b5aec);
  box-shadow:0 7px 20px rgba(19,93,222,.36);
  font-size:9px; font-weight:900;
  white-space:nowrap;
}
.price-card-head { display:flex; align-items:center; justify-content:space-between; }
.price-icon { width:42px; height:42px; display:grid; place-items:center; color:var(--cyan); border:1px solid var(--line); border-radius:11px; background:rgba(65,192,242,.07); }
.price-card-head small { color:#728bc7; font-size:9px; font-weight:900; letter-spacing:.18em; }
.price-card h3 { margin:20px 0 0; font-size:21px; }
.price-value { display:flex; align-items:baseline; gap:7px; margin-top:20px; }
.price-value strong { font-size:clamp(28px,2vw,36px); letter-spacing:-.045em; }
.price-value span { color:var(--ink-2); font-size:10px; }
.price-value.pending strong { font-size:26px; color:var(--cyan); }
.price-status { display:flex; align-items:center; gap:7px; min-height:36px; margin:14px 0 0; color:var(--muted); font-size:10px; }
.price-status i { width:7px; height:7px; border-radius:50%; background:var(--cyan); box-shadow:0 0 8px rgba(65,192,242,.45); }
.plan-cta { width:100%; min-height:42px; margin-top:16px; border:1px solid rgba(65,192,242,.33); border-radius:9px; display:flex; align-items:center; justify-content:center; gap:8px; color:var(--cyan); background:rgba(10,31,54,.5); cursor:pointer; font-weight:850; }
.featured .plan-cta { color:white; border-color:rgba(75,204,255,.6); background:linear-gradient(90deg,#126ce2,#1c58e7); }
.price-card { transition:transform .24s ease,border-color .24s ease,box-shadow .24s ease,background .24s ease; }
.price-card:hover { transform:translateY(-5px); border-color:rgba(82,211,255,.46); box-shadow:0 26px 58px rgba(0,0,0,.30),0 0 24px rgba(65,192,242,.08),inset 0 1px rgba(255,255,255,.06); }
.price-card.featured:hover { transform:translateY(-9px); }
.plan-cta { transition:transform .2s ease,color .2s ease,border-color .2s ease,background .2s ease,box-shadow .2s ease; }
.plan-cta:hover {
  transform:translateY(-2px);
  color:#fff;
  border-color:rgba(93,220,255,.78);
  background:linear-gradient(100deg,#0d8fe9,#176be7 58%,#2859d8);
  box-shadow:0 10px 26px rgba(23,107,231,.25),0 0 20px rgba(65,192,242,.12);
}
.plan-cta:active { transform:translateY(0) scale(.99); }
.feature-list { display:grid; gap:10px; margin-top:22px; padding-top:18px; border-top:1px solid var(--line); }
.price-line { display:flex; align-items:flex-start; gap:8px; color:var(--ink-2); font-size:10px; line-height:1.45; }
.price-line > span { width:19px; height:19px; flex:0 0 19px; display:grid; place-items:center; color:var(--cyan); border-radius:6px; background:rgba(65,192,242,.08); }
.price-line.muted { color:var(--muted); }
.pricing-empty, .api-state { max-width:680px; margin:0 auto; padding:18px 20px; border:1px solid var(--line); border-radius:14px; display:flex; align-items:flex-start; gap:12px; color:var(--ink-2); background:rgba(9,31,55,.6); }
.pricing-empty p { margin:4px 0 0; color:var(--muted); }

.video-section { background:linear-gradient(180deg,#020b17,#07172a 60%,#020b17); }
.video-section :deep(.product-video-section) { width:min(1180px,calc(100% - 56px)); }
.video-section :deep(.video-copy h2) { color:var(--ink); font-size:clamp(34px,3vw,50px); line-height:1.08; overflow:visible; }
.video-section :deep(.video-copy > p) { color:var(--ink-2); }
.video-section :deep(.transcript-card) { border-color:var(--line); background:rgba(9,31,55,.68); }
.video-section :deep(.transcript-card span) { color:var(--muted); }
.video-section :deep(.video-shell) { width:min(980px,100%); margin-top:30px; border-color:rgba(71,204,255,.4); background:#061426; box-shadow:0 34px 82px rgba(0,0,0,.4),0 0 36px rgba(65,192,242,.08); transform:perspective(1200px) rotateX(1.5deg); }
.video-section :deep(.video-fallback) { background:#061426; color:var(--ink-2); }

.workflow-section { background:#020b17; }
.workflow-heading { margin-bottom:34px; }
.workflow-track {
  position:relative;
  display:grid;
  grid-template-columns:repeat(6,minmax(0,1fr));
  gap:8px;
  min-height:300px;
  margin-top:6px;
}
.flow-line {
  position:absolute;
  z-index:1;
  left:3%;
  right:3%;
  top:50%;
  height:2px;
  transform:translateY(-50%);
  background:linear-gradient(90deg,rgba(65,192,242,.18),rgba(65,192,242,.72) 12%,rgba(47,124,255,.68) 50%,rgba(77,226,197,.66) 88%,rgba(65,192,242,.18));
  box-shadow:0 0 16px rgba(65,192,242,.08);
}
.flow-arrow {
  position:absolute;
  z-index:3;
  right:1.4%;
  top:50%;
  display:grid;
  place-items:center;
  color:var(--cyan);
  transform:translate(50%,-50%);
}
.flow-signal {
  position:absolute;
  z-index:4;
  top:calc(50% - 5px);
  width:10px;
  height:10px;
  border-radius:50%;
  background:var(--cyan);
  box-shadow:0 0 18px rgba(65,192,242,.82);
  animation:travel 7s linear infinite;
}
.flow-step {
  --flow-accent:var(--cyan);
  position:relative;
  z-index:2;
  min-width:0;
  height:300px;
}
.flow-step:nth-of-type(2) { --flow-accent:#4de2c5; }
.flow-step:nth-of-type(3) { --flow-accent:#65a6ff; }
.flow-step:nth-of-type(4) { --flow-accent:#ffb547; }
.flow-step:nth-of-type(5) { --flow-accent:#57d8ee; }
.flow-step:nth-of-type(6) { --flow-accent:#9b7cff; }
.flow-node {
  position:absolute;
  left:50%;
  top:50%;
  width:58px;
  height:58px;
  display:grid;
  place-items:center;
  color:var(--flow-accent);
  border:1px solid color-mix(in srgb,var(--flow-accent) 58%,transparent);
  border-radius:50%;
  background:color-mix(in srgb,var(--surface-2) 90%,transparent);
  box-shadow:0 14px 30px rgba(0,0,0,.18),0 0 24px color-mix(in srgb,var(--flow-accent) 15%,transparent),inset 0 1px rgba(255,255,255,.05);
  transform:translate(-50%,-50%);
  transition:transform .22s ease,box-shadow .22s ease,border-color .22s ease;
}
.flow-step:hover .flow-node {
  transform:translate(-50%,-50%) scale(1.07);
  box-shadow:0 16px 34px rgba(0,0,0,.2),0 0 30px color-mix(in srgb,var(--flow-accent) 24%,transparent);
}
.flow-copy-card {
  position:absolute;
  left:50%;
  width:min(190px,calc(100% - 8px));
  padding:12px 12px 11px;
  border:1px solid color-mix(in srgb,var(--flow-accent) 24%,var(--line));
  border-radius:13px;
  background:color-mix(in srgb,var(--surface) 86%,transparent);
  box-shadow:0 12px 28px rgba(0,0,0,.12),inset 0 1px rgba(255,255,255,.035);
  transform:translateX(-50%);
}
.flow-step.is-above .flow-copy-card { bottom:calc(50% + 52px); }
.flow-step.is-below .flow-copy-card { top:calc(50% + 52px); }
.flow-stem {
  position:absolute;
  left:50%;
  width:1px;
  height:24px;
  background:color-mix(in srgb,var(--flow-accent) 55%,transparent);
  transform:translateX(-50%);
}
.flow-step.is-above .flow-stem { bottom:50%; margin-bottom:28px; }
.flow-step.is-below .flow-stem { top:50%; margin-top:28px; }
.flow-copy-card small { color:var(--flow-accent); font-size:9px; font-weight:900; letter-spacing:.12em; }
.flow-copy-card b { display:block; margin-top:5px; color:var(--ink); font-size:12px; line-height:1.35; }
.flow-copy-card p { margin:4px 0 0; color:var(--muted); font-size:9px; line-height:1.45; }

.final-cta {
  margin-top:44px;
  padding:20px 24px;
  display:grid;
  grid-template-columns:auto minmax(0,1fr) auto;
  gap:24px;
  align-items:center;
  border:1px solid rgba(68,201,255,.34);
  border-radius:19px;
  background:linear-gradient(110deg,rgba(7,27,48,.96),rgba(8,45,75,.72),rgba(7,24,43,.96));
  box-shadow:0 24px 58px rgba(0,0,0,.24),inset 0 1px rgba(255,255,255,.05);
}
.cta-mascot { position:relative; width:98px; height:90px; display:grid; place-items:end center; }
.cta-mascot img { position:relative; z-index:2; width:78px; }
.cta-mascot span { position:absolute; bottom:2px; width:88px; height:30px; border-radius:50%; background:radial-gradient(ellipse,rgba(65,192,242,.28),transparent 68%); }
.final-cta h2 { margin:0; font-size:clamp(24px,2.2vw,34px); line-height:1.08; letter-spacing:-.04em; }
.final-cta h2 span,.final-cta h2 em { display:inline; font-style:normal; }

.faq-section { padding-top:78px; background:linear-gradient(180deg,#020b17,#03101f); }
.faq-grid { display:grid; grid-template-columns:minmax(300px,.75fr) minmax(520px,1.25fr); gap:80px; align-items:start; }
.faq-heading h2 { margin:0; font-size:clamp(38px,3.3vw,54px); line-height:1.06; letter-spacing:-.045em; overflow:visible; }
.faq-heading h2 span,.faq-heading h2 em { display:block; overflow:visible; font-style:normal; }
.faq-heading h2 em { color:transparent; background:linear-gradient(90deg,var(--tone-coral-a),var(--tone-coral-b)); -webkit-background-clip:text; background-clip:text; }
.faq-heading p { max-width:420px; margin:16px 0 0; color:var(--ink-2); line-height:1.68; }
.faq-item { border-top:1px solid var(--line); }
.faq-item:last-child { border-bottom:1px solid var(--line); }
.faq-item button { width:100%; padding:18px 0; border:0; display:flex; align-items:center; justify-content:space-between; gap:16px; color:var(--ink); background:transparent; text-align:left; cursor:pointer; font-weight:850; }
.faq-item p { margin:-2px 0 18px; color:var(--ink-2); line-height:1.65; }
.rotate { transform:rotate(180deg); }

.footer { padding:30px 0 44px; background:#020b17; }
.footer-panel {
  padding:32px;
  display:grid;
  grid-template-columns:1.5fr repeat(3,1fr);
  gap:34px;
  border:1px solid var(--line);
  border-radius:18px;
  background:linear-gradient(180deg,rgba(10,31,53,.78),rgba(5,20,37,.9));
}
.footer-brand p { max-width:330px; margin:14px 0 0; color:var(--muted); font-size:11px; line-height:1.6; }
.footer-col { display:grid; align-content:start; gap:9px; }
.footer-col b { margin-bottom:5px; font-size:11px; }
.footer-col a,.footer-col button { width:max-content; padding:0; border:0; color:var(--muted); background:transparent; font-size:10px; cursor:pointer; }
.footer-col a:hover,.footer-col button:hover { color:var(--cyan); }
.footer-bottom { grid-column:1/-1; margin-top:8px; padding-top:18px; border-top:1px solid var(--line); display:flex; justify-content:space-between; color:var(--muted); font-size:9px; }

.motion-ready [data-reveal] {
  opacity:0;
  transform:translateY(22px);
  transition:opacity .7s ease,transform .7s cubic-bezier(.2,.8,.2,1);
}
.motion-ready [data-reveal].is-visible,
.motion-complete [data-reveal] { opacity:1; transform:none; }

@keyframes sphereFloat { 0%,100%{transform:translateY(0) rotate(0)}50%{transform:translateY(-10px) rotate(5deg)} }
@keyframes mascotFloat { 0%,100%{transform:translateY(0)}50%{transform:translateY(-7px)} }
@keyframes mascotGuide { 0%,100%{transform:translateY(0) rotate(-2deg)}50%{transform:translateY(-7px) rotate(-1deg)} }
@keyframes glyphFloat { 0%,100%{transform:translateY(0) rotate(0)}50%{transform:translateY(-4px) rotate(8deg)} }
@keyframes orbit { to{transform:rotate(360deg)} }
@keyframes travel { 0%{left:5%;opacity:0}8%{opacity:1}92%{opacity:1}100%{left:94%;opacity:0} }


/* ===== SprintA true dual-theme + semantic text accents ===== */
:global(body:has(.landing-page.is-light)) { background:#edf5fb; color-scheme:light; }
:global(body:has(.landing-page:not(.is-light))) { background:#020b17; color-scheme:dark; }

/* Full-page LIGHT theme: every marketing section follows the light canvas. */
.landing-page.is-light .product-section { background:linear-gradient(180deg,#f8fbfe 0%,#eef5fb 78%,#e9f2f9 100%); }
.landing-page.is-light .ai-section {
  background:
    radial-gradient(circle at 72% 42%,rgba(40,89,216,.08),transparent 31rem),
    radial-gradient(circle at 24% 52%,rgba(12,152,127,.06),transparent 26rem),
    linear-gradient(180deg,#eef6fc 0%,#f8fbfe 55%,#edf5fb 100%);
}
.landing-page.is-light .pricing-section { background:linear-gradient(180deg,#f8fbfe 0%,#edf5fb 50%,#f7fbff 100%); }
.landing-page.is-light .video-section { background:linear-gradient(180deg,#f7fbff 0%,#edf5fb 62%,#f8fbfe 100%); }
.landing-page.is-light .workflow-section { background:#f8fbfe; }
.landing-page.is-light .faq-section { background:linear-gradient(180deg,#f8fbfe 0%,#eef5fb 100%); }
.landing-page.is-light .footer { background:#edf5fb; }

.landing-page.is-light .landing-nav,
.landing-page.is-light .mobile-menu,
.landing-page.is-light .float-card,
.landing-page.is-light .product-card,
.landing-page.is-light .ai-steps article,
.landing-page.is-light .assistant-panel,
.landing-page.is-light .analysis-card,
.landing-page.is-light .confirm-card,
.landing-page.is-light .ai-float,
.landing-page.is-light .price-card,
.landing-page.is-light .final-cta,
.landing-page.is-light .footer-panel {
  color:var(--ink);
  border-color:var(--line);
  background:linear-gradient(180deg,rgba(255,255,255,.97),rgba(244,249,253,.95));
  box-shadow:0 18px 48px rgba(32,72,108,.10),inset 0 1px rgba(255,255,255,.94);
}
.landing-page.is-light .landing-nav {
  background:rgba(255,255,255,.84);
  box-shadow:0 14px 38px rgba(29,72,109,.10),inset 0 1px rgba(255,255,255,.94);
}
.landing-page.is-light .btn-secondary { color:var(--ink-2); border-color:var(--line); background:rgba(255,255,255,.74); }
.landing-page.is-light .text-btn { color:var(--ink-2); }

.landing-page.is-light .proof-row,
.landing-page.is-light .trust-note,
.landing-page.is-light .section-copy,
.landing-page.is-light .center-heading p,
.landing-page.is-light .product-copy small,
.landing-page.is-light .ai-steps small,
.landing-page.is-light .assistant-brand small,
.landing-page.is-light .analysis-card > p,
.landing-page.is-light .impact-list small,
.landing-page.is-light .confirm-card p,
.landing-page.is-light .ai-float small,
.landing-page.is-light .price-status,
.landing-page.is-light .price-line,
.landing-page.is-light .flow-step p,
.landing-page.is-light .faq-heading p,
.landing-page.is-light .faq-item p,
.landing-page.is-light .footer-brand p,
.landing-page.is-light .footer-col a,
.landing-page.is-light .footer-col button,
.landing-page.is-light .footer-bottom { color:var(--ink-2); }

/* Hero: the real dark product screenshot remains dark by design; its surrounding chrome adapts. */
.landing-page.is-light .dashboard-tilt {
  border-color:rgba(13,130,189,.30);
  background:#fbfdff;
  box-shadow:0 34px 72px rgba(38,76,112,.18),0 0 38px rgba(13,130,189,.08),inset 0 1px rgba(255,255,255,.92);
}
.landing-page.is-light .window-top {
  color:#45657e;
  background:linear-gradient(180deg,#f8fbfe,#edf4fa);
  border-bottom:1px solid rgba(29,78,121,.12);
}
.landing-page.is-light .hero-platform { opacity:.58; filter:saturate(.82); }
.landing-page.is-light .wire-sphere { opacity:.34; }

/* Product cards */
.landing-page.is-light .product-card:hover {
  border-color:rgba(11,130,189,.38);
  box-shadow:0 24px 56px rgba(34,76,111,.14),0 0 24px rgba(11,130,189,.07),inset 0 1px rgba(255,255,255,.96);
}
.landing-page.is-light .iso-shadow { opacity:.34; }
.landing-page.is-light .iso-platform.back { background:linear-gradient(145deg,rgba(122,185,235,.34),rgba(222,238,250,.86)); }
.landing-page.is-light .iso-platform.front { background:linear-gradient(145deg,rgba(89,161,220,.42),rgba(217,236,249,.94)); }
.landing-page.is-light .iso-object {
  color:#0b82bd;
  border-color:rgba(11,130,189,.32);
  background:linear-gradient(145deg,#ffffff,#dfeef9);
  box-shadow:0 18px 34px rgba(40,87,126,.16),inset 0 1px rgba(255,255,255,.96),0 0 22px rgba(11,130,189,.07);
}
.landing-page.is-light .data-chip { border-color:rgba(11,130,189,.24); background:rgba(233,246,253,.94); box-shadow:0 8px 18px rgba(37,79,116,.12); }

/* AI */
.landing-page.is-light .ai-nebula { opacity:.42; }
.landing-page.is-light .assistant-panel {
  border-color:rgba(11,130,189,.26);
  background:linear-gradient(180deg,rgba(255,255,255,.99),rgba(241,248,253,.98));
  box-shadow:0 30px 70px rgba(34,73,110,.16),0 0 44px rgba(40,89,216,.05),inset 0 1px rgba(255,255,255,.98);
}
.landing-page.is-light .chat-avatar { background:#edf4fa; }
.landing-page.is-light .user-line p { color:#12314d; border-color:rgba(40,89,216,.18); background:#eaf2ff; }
.landing-page.is-light .analysis-card,
.landing-page.is-light .impact-list > div,
.landing-page.is-light .confirm-card { background:rgba(244,249,253,.97); }
.landing-page.is-light .impact-list > div { border-color:rgba(29,78,121,.10); }
.landing-page.is-light .control-pill { color:#087c69; border-color:rgba(12,152,127,.20); background:rgba(12,152,127,.07); }
.landing-page.is-light .mascot-stage img { filter:drop-shadow(0 18px 24px rgba(32,74,110,.20)) drop-shadow(0 0 18px rgba(11,130,189,.10)); }
.landing-page.is-light .ai-float { background:rgba(255,255,255,.95); }

/* Pricing */
.landing-page.is-light .billing-pill { color:#1764d7; background:rgba(23,100,215,.06); }
.landing-page.is-light .price-card { background:linear-gradient(180deg,#ffffff,#f4f9fd); }
.landing-page.is-light .price-card.featured {
  border-color:rgba(40,89,216,.44);
  box-shadow:0 26px 58px rgba(40,89,216,.14),0 0 28px rgba(11,130,189,.06),inset 0 1px #fff;
}
.landing-page.is-light .plan-cta { color:#1764d7; border-color:rgba(40,89,216,.24); background:#f4f8ff; }
.landing-page.is-light .featured .plan-cta { color:#fff; }
.landing-page.is-light .plan-cta:hover { color:#fff; border-color:#2d79e6; background:linear-gradient(100deg,#158acb,#1764d7 62%,#5146d9); box-shadow:0 10px 24px rgba(23,100,215,.18); }
.landing-page.is-light .price-card:hover { border-color:rgba(23,100,215,.34); box-shadow:0 24px 54px rgba(34,76,111,.15),0 0 20px rgba(11,130,189,.05),inset 0 1px #fff; }
.landing-page.is-light .pricing-empty,
.landing-page.is-light .api-state { background:rgba(255,255,255,.90); }

/* Video: surrounding section is light; actual product media may remain dark. */
.landing-page.is-light .video-section :deep(.transcript-card) { color:var(--ink); border-color:var(--line); background:rgba(255,255,255,.92); }
.landing-page.is-light .video-section :deep(.video-shell) { border-color:rgba(11,130,189,.28); box-shadow:0 28px 62px rgba(34,72,106,.15),0 0 28px rgba(11,130,189,.05); }

/* Workflow + CTA + FAQ + footer */
.landing-page.is-light .flow-copy-card {
  background:rgba(255,255,255,.88);
  box-shadow:0 12px 26px rgba(36,82,119,.08),inset 0 1px #fff;
}
.landing-page.is-light .flow-node {
  color:var(--flow-accent);
  border-color:rgba(11,130,189,.28);
  background:radial-gradient(circle,#ffffff,#e7f2fa);
  box-shadow:0 12px 28px rgba(36,82,119,.10);
}
.landing-page.is-light .flow-line { opacity:.58; }
.landing-page.is-light .final-cta { background:linear-gradient(110deg,#ffffff,#edf7fd 48%,#f5f9fd); }
.landing-page.is-light .faq-item { border-color:rgba(29,78,121,.14); }
.landing-page.is-light .footer-panel { background:linear-gradient(180deg,#ffffff,#f1f7fb); }

/* Semantic emphasis: these colors are intentionally farther apart so key words do not all look cyan. */
.editorial-headline .tone-cyan,
.center-heading .tone-cyan,
.final-cta .tone-cyan,
.editorial-headline .tone-mint,
.center-heading .tone-mint,
.editorial-headline .tone-blue,
.center-heading .tone-blue,
.tone-warm,
.faq-heading h2 em { font-weight:900; }


/* ===== Owner correction pass: scale, accents, theme controls, flow, CTA ===== */

/* Keep Vietnamese diacritics and italic/gradient glyphs from being visually clipped. */
.editorial-headline,
.hero-copy h1,
.center-heading h2,
.ai-title,
.faq-heading h2,
.final-cta h2 {
  font-family: Inter, "Segoe UI", Arial, sans-serif;
  font-kerning: normal;
  font-feature-settings: "kern" 1, "liga" 1;
  text-wrap: balance;
  overflow: visible;
}
.headline-line {
  overflow: visible;
  padding-top: .11em;
  padding-bottom: .09em;
}
.headline-line > span,
.tone-cyan,
.tone-mint,
.tone-blue,
.tone-warm,
.tone-coral {
  -webkit-box-decoration-break: clone;
  box-decoration-break: clone;
  padding-inline-end: .035em;
}

/* One visual language for every section label: icon chip + clean uppercase label. */
.eyebrow {
  min-height: 28px;
  gap: 9px;
  margin-bottom: 16px;
  color: var(--cyan);
  font-size: 12px;
  line-height: 1;
  font-weight: 900;
  letter-spacing: .15em;
  white-space: nowrap;
}
.eyebrow > svg {
  width: 25px;
  height: 25px;
  flex: 0 0 25px;
  padding: 5px;
  border: 1px solid color-mix(in srgb, var(--cyan) 42%, transparent);
  border-radius: 8px;
  background: color-mix(in srgb, var(--cyan) 8%, transparent);
  filter: drop-shadow(0 0 9px color-mix(in srgb, var(--cyan) 16%, transparent));
}
.eyebrow.centered { display: inline-flex; }
.center-heading .eyebrow.centered { margin-inline: auto; }

/* Brand / nav: no clipped A, no underlined login, proper light theme controls. */
.brand {
  overflow: visible;
  padding-right: 6px;
}
.brand-logo {
  flex: 0 0 27px;
  width: 27px;
  height: 27px;
  overflow: visible;
}
.brand-word {
  display: inline-block;
  padding: .08em .05em .1em 0;
  overflow: visible;
  line-height: 1.08;
  letter-spacing: -.015em;
  white-space: nowrap;
}
.text-btn,
.text-btn:link,
.text-btn:visited,
.text-btn:hover,
.text-btn:focus {
  text-decoration: none !important;
}
.desktop-nav a,
.text-btn {
  position: relative;
}
.desktop-nav a::after,
.text-btn::after {
  content: '';
  position: absolute;
  left: 0;
  right: 0;
  bottom: 3px;
  height: 1px;
  transform: scaleX(0);
  transform-origin: center;
  background: linear-gradient(90deg, var(--cyan), var(--blue));
  transition: transform .2s ease;
}
.desktop-nav a:hover::after,
.text-btn:hover::after { transform: scaleX(1); }

.landing-page.is-light .icon-btn,
.landing-page.is-light .lang-btn {
  color: #1764d7;
  border-color: rgba(23,100,215,.24);
  background: rgba(255,255,255,.92);
  box-shadow: inset 0 1px rgba(255,255,255,.98), 0 8px 18px rgba(38,76,112,.07);
}
.landing-page.is-light .icon-btn:hover,
.landing-page.is-light .lang-btn:hover {
  color: #075fbd;
  border-color: rgba(11,130,189,.38);
  background: #eef7ff;
}
.landing-page.is-light .lang-btn {
  outline: 2px solid rgba(65,192,242,.10);
  outline-offset: 1px;
}

/* Readable at 100% zoom: neither billboard-sized nor tiny. */
.shell { width: min(1260px, calc(100% - 56px)); }
.section { padding: 82px 0; }
.hero { min-height: 620px; padding: 76px 0 70px; }
.hero-grid { grid-template-columns: minmax(0,.94fr) minmax(500px,1.06fr); gap: 46px; }
.hero-copy h1 {
  max-width: 650px;
  font-size: clamp(48px, 3.35vw, 62px);
  line-height: 1.10;
  letter-spacing: -.038em;
}
.hero-copy .lead { font-size: 16px; line-height: 1.68; }
.center-heading { max-width: 980px; margin-bottom: 34px; }
.center-heading h2 {
  font-size: clamp(36px, 2.7vw, 48px);
  line-height: 1.10;
  letter-spacing: -.035em;
}
.product-title { max-width: 1000px; }
.product-title > span,
.product-title > em { white-space: normal; }
.ai-title {
  max-width: 590px;
  font-size: clamp(39px, 3vw, 54px);
  line-height: 1.11;
  letter-spacing: -.038em;
}
.video-section :deep(.video-copy h2) {
  font-size: clamp(36px, 2.75vw, 48px);
  line-height: 1.10;
  letter-spacing: -.035em;
}
.video-section :deep(.product-video-section) { width: min(1120px, calc(100% - 48px)); }
.video-section :deep(.video-shell) { width: min(900px, 100%); }

/* Pricing controls are now real controls, and buttons visibly respond on hover. */
.pricing-controls {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  margin-top: 20px;
  padding: 4px;
  border: 1px solid var(--line);
  border-radius: 999px;
  background: color-mix(in srgb, var(--surface) 70%, transparent);
}
.pricing-controls .billing-pill { margin-top: 0; border-color: transparent; }
.benefits-toggle {
  appearance: none;
  cursor: pointer;
  opacity: .76;
  transition: color .2s ease, background .2s ease, opacity .2s ease, transform .2s ease;
}
.benefits-toggle:hover,
.benefits-toggle.active {
  color: #fff;
  opacity: 1;
  background: linear-gradient(100deg, #0d8fe9, #176be7 58%, #2859d8);
  transform: translateY(-1px);
}
.plan-cta:hover {
  color: #fff !important;
  border-color: rgba(93,220,255,.82) !important;
  background: linear-gradient(100deg,#0d8fe9,#176be7 58%,#2859d8) !important;
  box-shadow: 0 10px 26px rgba(23,107,231,.28), 0 0 22px rgba(65,192,242,.14) !important;
}
.landing-page.is-light .pricing-controls {
  background: rgba(255,255,255,.82);
  border-color: rgba(29,78,121,.14);
  box-shadow: 0 10px 24px rgba(34,76,111,.07);
}
.landing-page.is-light .benefits-toggle:not(.active) {
  color: #36536d;
  background: transparent;
}
.landing-page.is-light .benefits-toggle:hover,
.landing-page.is-light .benefits-toggle.active {
  color: #fff;
  background: linear-gradient(100deg,#158acb,#1764d7 62%,#5146d9);
}
.feature-list[v-show="false"] { display: none; }

/* AI composition: mascot stays outside the panel and points toward it instead of covering UI. */
.ai-showcase { min-height: 510px; padding-right: 128px; }
.assistant-panel {
  width: 100%;
  margin-right: 0;
  min-height: 480px;
}
.mascot-stage {
  right: -34px;
  bottom: 64px;
  width: 176px;
  height: 220px;
}
.mascot-stage img {
  width: 168px;
  transform: translateX(8px) rotate(-5deg);
}
.ai-rings { right: -36px; bottom: 52px; }
.engine-float { right: -56px; top: 54px; }
.project-float { left: -18px; bottom: -4px; }

/* Flowchart: closer to the supplied reference — clean line, alternating copy, colorful nodes, no fake cards. */
.workflow-heading { margin-bottom: 24px; }
.workflow-track {
  min-height: 270px;
  margin-top: 0;
  gap: 0;
}
.flow-line {
  left: 2.5%;
  right: 2.5%;
  height: 2px;
  background: linear-gradient(90deg,
    rgba(101,166,255,.70),
    rgba(77,226,197,.72) 20%,
    rgba(255,178,71,.78) 42%,
    rgba(69,207,255,.74) 62%,
    rgba(255,106,87,.72) 82%,
    rgba(155,124,255,.72));
}
.flow-arrow { right: .5%; color: #9b7cff; }
.flow-step { height: 270px; }
.flow-node {
  width: 62px;
  height: 62px;
  border-width: 2px;
  background: color-mix(in srgb, var(--surface-2) 82%, transparent);
  box-shadow: 0 14px 30px rgba(0,0,0,.16), 0 0 24px color-mix(in srgb,var(--flow-accent) 18%,transparent);
}
.flow-copy-card {
  width: min(190px, calc(100% - 16px));
  padding: 0 7px;
  border: 0;
  border-radius: 0;
  background: transparent;
  box-shadow: none;
  text-align: center;
}
.flow-step.is-above .flow-copy-card { bottom: calc(50% + 56px); }
.flow-step.is-below .flow-copy-card { top: calc(50% + 56px); }
.flow-stem { height: 26px; width: 2px; background: var(--flow-accent); opacity: .62; }
.flow-copy-card small { font-size: 10px; color: var(--flow-accent); }
.flow-copy-card b { margin-top: 6px; font-size: 13px; }
.flow-copy-card p { margin-top: 5px; font-size: 10.5px; line-height: 1.42; }

/* CTA should read as a conclusion, not a tiny banner. */
.final-cta {
  min-height: 166px;
  margin-top: 38px;
  padding: 26px 30px;
  grid-template-columns: 112px minmax(0, 1fr) auto;
  gap: 28px;
}
.cta-mascot { width: 108px; height: 104px; }
.cta-mascot img { width: 92px; }
.final-cta h2 {
  max-width: 760px;
  font-size: clamp(38px, 3vw, 48px);
  line-height: 1.08;
  letter-spacing: -.035em;
}
.final-cta h2 span,
.final-cta h2 em { display: inline; }

/* Light theme keeps the same semantic emphasis but with accessible darker accents. */
.landing-page.is-light .eyebrow > svg {
  border-color: rgba(11,130,189,.28);
  background: rgba(11,130,189,.06);
}
.landing-page.is-light .flow-copy-card { background: transparent; box-shadow: none; }
.landing-page.is-light .flow-node { background: #fff; }

@media (min-width: 1121px) {
  .product-section .center-heading { max-width:1120px; }
  .product-section .center-heading h2 { max-width:1080px; margin-inline:auto; font-size:clamp(36px,2.9vw,50px); white-space:nowrap; }
  .workflow-section .center-heading h2 { font-size:clamp(36px,2.9vw,50px); }
  .pricing-section .center-heading h2 { font-size:clamp(34px,2.8vw,48px); }
  .hero-copy .headline-line { white-space:nowrap; }
}

@media (max-width: 1280px) and (min-width: 1121px) {
  .hero-copy h1 { font-size:48px; }
  .hero-copy .headline-line { white-space:normal; }
  .product-section .center-heading h2 { white-space:normal; }
}

@media (max-width: 1120px) {
  .hero-grid { grid-template-columns:1fr; gap:20px; }
  .hero-copy { max-width:760px; }
  .hero-stage { min-height:540px; }
  .ai-grid { grid-template-columns:1fr; }
  .ai-copy { max-width:780px; }
  .ai-showcase { max-width:820px; width:100%; margin-inline:auto; }
  .product-grid,.pricing-grid { grid-template-columns:repeat(2,minmax(0,1fr)); }
  .workflow-track { grid-template-columns:repeat(3,minmax(0,1fr)); gap:18px; min-height:auto; }
  .flow-line,.flow-signal,.flow-arrow,.flow-stem { display:none; }
  .flow-step { height:auto; }
  .flow-node,.flow-copy-card { position:relative; left:auto; top:auto; bottom:auto; transform:none; }
  .flow-node { margin:0 auto 12px; }
  .flow-step:hover .flow-node { transform:scale(1.05); }
  .flow-copy-card { width:100%; min-height:105px; }
  .faq-grid { grid-template-columns:1fr; gap:42px; }
}

@media (max-width: 820px) {
  .shell { width:min(100% - 32px, 680px); }
  .landing-nav { width:calc(100% - 24px); top:8px; margin-top:8px; }
  .desktop-nav,.desktop-only { display:none !important; }
  .mobile-menu { display:grid; }
  .nav-inner { min-height:62px; padding:0 12px; gap:12px; }
  .nav-actions { margin-left:auto; }
  .nav-cta { display:none; }
  .hero { min-height:auto; padding:82px 0 76px; }
  .hero-copy h1 { font-size:clamp(42px,7.2vw,54px); letter-spacing:-.045em; }
  .hero-stage { min-height:440px; }
  .dashboard-tilt { width:95%; transform:none; }
  .dashboard-tilt:hover { transform:translateY(-4px); }
  .float-card { transform:none; }
  .float-card-a { left:0; top:8%; }
  .float-card-b { right:0; top:30%; }
  .float-card-c { right:4%; bottom:2%; }
  .wire-sphere { width:200px; height:200px; right:-10px; }
  .section { padding:72px 0; }
  .product-grid,.pricing-grid { grid-template-columns:1fr; }
  .ai-steps { grid-template-columns:1fr; }
  .ai-showcase { min-height:760px; }
  .assistant-panel { width:100%; min-height:520px; }
  .assistant-panel { width:100%; margin-right:0; }
  .mascot-stage { right:12px; bottom:6px; width:150px; height:190px; }
  .mascot-stage img { width:142px; }
  .project-float { left:12px; bottom:16px; }
  .engine-float { right:8px; top:54px; }
  .workflow-track { grid-template-columns:repeat(2,minmax(0,1fr)); }
  .final-cta { grid-template-columns:auto 1fr; }
  .final-cta .btn { grid-column:1/-1; }
  .footer-panel { grid-template-columns:1fr 1fr; }
  .footer-brand { grid-column:1/-1; }
}

@media (max-width: 540px) {
  .shell { width:calc(100% - 28px); }
  .landing-nav { border-radius:13px; }
  .brand-word small { display:none; }
  .lang-btn { padding:0 8px; }
  .hero-copy h1 { font-size:clamp(38px,10.5vw,48px); line-height:1.08; }
  .hero-copy .lead { font-size:15px; }
  .hero-actions { align-items:stretch; flex-direction:column; }
  .hero-actions .btn { width:100%; }
  .proof-row { gap:11px 15px; }
  .hero-stage { min-height:350px; }
  .float-card { min-width:130px; padding:8px 9px; }
  .float-card-a { top:5%; }
  .float-card-b { top:26%; }
  .float-card-c { display:none; }
  .center-heading h2 { font-size:clamp(31px,8.8vw,40px); }
  .product-card { min-height:310px; }
  .ai-title { font-size:clamp(34px,9.4vw,44px); }
  .ai-copy-actions { align-items:flex-start; flex-direction:column; }
  .ai-showcase { min-height:725px; }
  .assistant-panel { padding:14px; }
  .user-line p { max-width:100%; }
  .assistant-head { align-items:flex-start; gap:8px; }
  .control-pill { font-size:8px; }
  .impact-list > div { grid-template-columns:25px 1fr auto; }
  .mascot-stage { right:6px; bottom:6px; width:130px; height:170px; }
  .mascot-stage img { width:122px; }
  .mascot-platform { width:130px; }
  .ai-rings { right:-26px; bottom:0; width:180px; height:180px; }
  .ai-float { padding:8px 9px; }
  .project-float { left:6px; bottom:6px; }
  .engine-float { display:none; }
  .pricing-grid { gap:15px; }
  .price-card { min-height:auto; padding:22px; }
  .video-section :deep(.product-video-section) { width:calc(100% - 28px); }
  .workflow-track { grid-template-columns:1fr 1fr; gap:16px 12px; }
  .final-cta { grid-template-columns:1fr; text-align:center; }
  .cta-mascot { margin-inline:auto; }
  .final-cta h2 span,.final-cta h2 em { display:inline; }
  .faq-heading h2 { font-size:clamp(34px,9.8vw,44px); }
  .footer-panel { grid-template-columns:1fr; padding:24px; }
  .footer-brand { grid-column:auto; }
  .footer-bottom { flex-direction:column; gap:5px; }
}


@media (max-width: 1120px) {
  .hero-copy h1 { font-size: clamp(46px, 6vw, 58px); }
  .ai-showcase { padding-right: 0; }
  .mascot-stage { right: 8px; bottom: 4px; }
  .engine-float { right: 8px; }
  .final-cta { grid-template-columns: 96px minmax(0,1fr) auto; }
  .final-cta h2 { font-size: clamp(34px, 4.3vw, 44px); }
}

@media (max-width: 820px) {
  .eyebrow { font-size: 11px; letter-spacing: .13em; }
  .hero-copy h1 { font-size: clamp(42px, 7vw, 52px); line-height: 1.11; }
  .center-heading h2 { font-size: clamp(34px, 5.2vw, 44px); }
  .ai-title { font-size: clamp(36px, 6vw, 46px); }
  .final-cta { grid-template-columns: 82px 1fr; gap: 18px; min-height: 0; }
  .final-cta .btn { grid-column: 1 / -1; justify-self: stretch; }
  .final-cta h2 { font-size: clamp(32px, 5.8vw, 42px); }
}

@media (max-width: 540px) {
  .headline-line { padding-top: .13em; padding-bottom: .10em; }
  .hero-copy h1 { font-size: clamp(38px, 10vw, 46px); letter-spacing: -.028em; }
  .center-heading h2 { font-size: clamp(31px, 8vw, 39px); line-height: 1.12; }
  .ai-title { font-size: clamp(33px, 8.8vw, 42px); line-height: 1.12; }
  .pricing-controls { width: 100%; justify-content: center; flex-wrap: wrap; border-radius: 16px; }
  .final-cta { grid-template-columns: 1fr; text-align: center; padding: 24px 20px; }
  .final-cta h2 { max-width: 100%; font-size: clamp(30px, 8.6vw, 38px); }
}



/* ===== SprintA mobile polish + light featured CTA fix ===== */
/* Fix the featured Plus CTA in light mode: keep white text on a real blue surface. */
.landing-page.is-light .price-card.featured .plan-cta {
  color: #fff !important;
  border-color: rgba(41, 112, 235, .72) !important;
  background: linear-gradient(100deg, #1596d2 0%, #1764d7 58%, #5146d9 100%) !important;
  box-shadow: 0 10px 24px rgba(23, 100, 215, .20), inset 0 1px rgba(255,255,255,.20) !important;
}
.landing-page.is-light .price-card.featured .plan-cta:hover {
  background: linear-gradient(100deg, #0d8fe9 0%, #176be7 54%, #3b54e7 100%) !important;
  box-shadow: 0 14px 30px rgba(23, 100, 215, .28), 0 0 18px rgba(65,192,242,.12) !important;
}

/* Tablet/mobile: intentionally re-compose the page instead of merely shrinking desktop. */
@media (max-width: 820px) {
  .landing-page {
    --mobile-side: 16px;
    overflow-x: hidden;
  }

  .shell {
    width: calc(100% - (var(--mobile-side) * 2));
    max-width: 680px;
  }

  .section {
    padding: 58px 0;
  }

  /* compact floating nav */
  .landing-nav {
    top: 8px;
    width: calc(100% - 16px);
    margin-top: 8px;
    border-radius: 18px;
  }
  .nav-inner {
    min-height: 58px;
    padding: 0 10px 0 12px;
    gap: 8px;
  }
  .brand { gap: 8px; }
  .brand-logo { width: 24px; height: 24px; }
  .brand-word { font-size: 15px; }
  .nav-actions { gap: 6px; }
  .icon-btn, .lang-btn { min-height: 36px; height: 36px; }
  .icon-btn { width: 36px; }
  .lang-btn { padding-inline: 9px; }
  .mobile-menu { display: grid; }
  .desktop-nav, .desktop-only, .nav-cta { display: none !important; }
  .mobile-nav {
    margin: 0 8px 8px;
    padding: 10px;
    gap: 3px;
    border: 1px solid var(--line);
    border-radius: 14px;
    background: color-mix(in srgb, var(--surface-2) 94%, transparent);
    box-shadow: 0 18px 42px rgba(0,0,0,.20);
    backdrop-filter: blur(20px);
  }
  .mobile-nav a {
    padding: 11px 10px;
    border-bottom: 0;
    border-radius: 9px;
    font-weight: 750;
  }
  .mobile-nav a:hover { background: color-mix(in srgb, var(--cyan) 10%, transparent); }
  .mobile-nav .btn { margin-top: 5px; width: 100%; }

  /* HERO — readable at 100% zoom, dashboard visible without swallowing the screen */
  .hero {
    min-height: auto;
    padding: 70px 0 48px;
  }
  .hero-grid {
    grid-template-columns: 1fr;
    gap: 34px;
  }
  .hero-copy {
    max-width: 620px;
  }
  .hero-copy h1 {
    max-width: 100%;
    font-size: clamp(35px, 8.7vw, 44px);
    line-height: 1.08;
    letter-spacing: -.035em;
  }
  .hero-copy .headline-line {
    white-space: normal !important;
    padding-block: .07em .10em;
  }
  .hero-glyph { right: -4px; top: -4px; transform: scale(.82); }
  .hero-copy .lead {
    max-width: 58ch;
    margin-top: 16px;
    font-size: 14.5px;
    line-height: 1.62;
  }
  .hero-actions {
    margin-top: 22px;
    gap: 10px;
    flex-wrap: wrap;
  }
  .hero-actions .btn { min-height: 44px; }
  .proof-row {
    margin-top: 20px;
    gap: 10px 16px;
    font-size: 11px;
  }
  .hero-stage {
    min-height: 300px;
    padding: 10px 0 18px;
  }
  .dashboard-tilt {
    width: 100%;
    max-width: 620px;
    transform: none;
  }
  .dashboard-tilt:hover { transform: translateY(-3px); }
  .dashboard-window { border-radius: 15px; }
  .window-top { height: 34px; padding-inline: 11px; }
  .window-top small { font-size: 9px; }
  .hero-platform {
    width: 88%;
    left: 6%;
    bottom: 8%;
    height: 72px;
    opacity: .82;
  }
  .wire-sphere { width: 170px; height: 170px; right: -30px; top: 24px; opacity: .6; }
  .float-card {
    min-width: 118px;
    padding: 7px 9px;
    border-radius: 10px;
  }
  .float-card b { font-size: 9px; }
  .float-card small { font-size: 7.5px; }
  .float-card-a { left: -2px; top: 14%; }
  .float-card-b { right: -2px; top: 30%; }
  .float-card-c { right: 4%; bottom: 1%; }

  /* Headings */
  .center-heading { margin-bottom: 30px; }
  .center-heading h2,
  .product-title,
  .workflow-heading h2,
  .pricing-heading h2 {
    font-size: clamp(30px, 7vw, 40px) !important;
    line-height: 1.10;
    letter-spacing: -.035em;
    white-space: normal !important;
  }
  .center-heading p { max-width: 58ch; margin-inline: auto; font-size: 13.5px; line-height: 1.6; }
  .eyebrow { margin-bottom: 11px; font-size: 10px; letter-spacing: .13em; }

  /* PRODUCT — horizontal snap cards make all modules easy to scan on a phone */
  .product-grid {
    display: flex;
    gap: 14px;
    overflow-x: auto;
    padding: 4px 2px 14px;
    scroll-snap-type: x mandatory;
    scrollbar-width: none;
    overscroll-behavior-inline: contain;
  }
  .product-grid::-webkit-scrollbar { display: none; }
  .product-card {
    flex: 0 0 min(84vw, 340px);
    min-height: 300px;
    scroll-snap-align: center;
  }
  .product-visual { min-height: 160px; }
  .product-copy { padding: 18px 18px 20px; }
  .product-copy strong { font-size: 16px; }
  .product-copy small { font-size: 12px; line-height: 1.5; }

  /* AI — content first; decorative mascot sits outside the panel instead of covering it */
  .ai-grid {
    grid-template-columns: 1fr;
    gap: 34px;
  }
  .ai-copy { max-width: 100%; }
  .ai-title {
    max-width: 100%;
    font-size: clamp(34px, 8vw, 43px) !important;
    line-height: 1.09 !important;
    letter-spacing: -.035em;
  }
  .ai-title .headline-line { white-space: normal; }
  .ai-glyph { right: -3px; top: -3px; transform: scale(.8); }
  .section-copy { font-size: 14px; line-height: 1.62; }
  .ai-steps {
    display: grid;
    grid-template-columns: repeat(3, minmax(0,1fr));
    gap: 9px;
  }
  .ai-steps article {
    min-height: 132px;
    padding: 14px 10px;
  }
  .ai-steps .step-icon { width: 38px; height: 38px; }
  .ai-steps strong { font-size: 11px; }
  .ai-steps small { font-size: 9px; line-height: 1.35; }
  .ai-copy-actions { margin-top: 18px; gap: 10px; flex-wrap: wrap; }
  .trust-note { font-size: 10px; }

  .ai-showcase {
    min-height: auto !important;
    width: 100%;
    max-width: 680px;
    margin-inline: auto;
    padding: 0 0 128px;
  }
  .assistant-panel {
    position: relative;
    width: 100%;
    min-height: 0 !important;
    margin: 0;
    padding: 14px;
    border-radius: 16px;
  }
  .assistant-head { gap: 8px; }
  .assistant-brand b { font-size: 12px; }
  .assistant-brand small { font-size: 9px; }
  .control-pill { font-size: 8px; padding: 6px 8px; }
  .chat-line { gap: 8px; }
  .chat-avatar { width: 28px; height: 28px; }
  .user-line p, .analysis-card, .confirm-card { font-size: 10px; }
  .impact-list > div { min-height: 46px; grid-template-columns: 24px minmax(0,1fr) auto; gap: 7px; }
  .impact-list b { font-size: 10px; }
  .impact-list small { font-size: 8px; }
  .impact-list em { font-size: 7px; padding: 4px 6px; }
  .confirm-card { padding: 12px; }
  .confirm-actions { gap: 8px; flex-wrap: wrap; }
  .confirm-actions .btn { min-height: 36px; padding-inline: 10px; font-size: 9px; }
  .mascot-stage {
    right: 8px !important;
    bottom: -4px !important;
    width: 116px !important;
    height: 128px !important;
    z-index: 5;
  }
  .mascot-stage img { width: 110px !important; }
  .mascot-platform { width: 112px; }
  .ai-rings { width: 150px; height: 150px; right: -16px; bottom: 6px; }
  .project-float {
    left: 8px !important;
    bottom: 22px !important;
    max-width: calc(100% - 142px);
  }
  .engine-float { display: none; }

  /* PRICING — mobile carousel, while keeping real API data and benefits */
  .pricing-controls { width: fit-content; max-width: 100%; margin-inline: auto; flex-wrap: wrap; justify-content: center; }
  .pricing-grid {
    display: flex;
    gap: 14px;
    overflow-x: auto;
    padding: 12px 2px 20px;
    scroll-snap-type: x mandatory;
    scrollbar-width: none;
    overscroll-behavior-inline: contain;
  }
  .pricing-grid::-webkit-scrollbar { display: none; }
  .price-card {
    flex: 0 0 min(84vw, 340px);
    min-height: 430px;
    padding: 22px;
    scroll-snap-align: center;
  }
  .price-card:hover, .price-card.featured:hover { transform: none; }
  .price-card h3 { font-size: 22px; }
  .price-value strong { font-size: clamp(28px, 8vw, 34px); }
  .plan-cta { min-height: 44px; }
  .feature-list { margin-top: 18px; padding-top: 16px; }
  .price-line { font-size: 11px; }

  /* Video */
  .video-section :deep(.product-video-section) {
    width: calc(100% - 24px) !important;
    max-width: 680px;
  }
  .video-section :deep(.video-copy h2) {
    font-size: clamp(30px, 7.2vw, 40px) !important;
    line-height: 1.10;
  }
  .video-section :deep(.video-copy > p) { font-size: 13.5px; line-height: 1.55; }
  .video-section :deep(.video-shell) {
    width: 100%;
    margin-top: 22px;
    transform: none;
    border-radius: 16px;
  }
  .video-section :deep(.transcript-card) { font-size: 11px; }

  /* WORKFLOW — real vertical mobile timeline, not a cramped 2-column desktop grid */
  .workflow-track {
    position: relative;
    display: grid;
    grid-template-columns: 1fr !important;
    gap: 0 !important;
    min-height: 0;
    padding: 6px 0 8px;
  }
  .workflow-track::before {
    content: '';
    position: absolute;
    top: 24px;
    bottom: 24px;
    left: 24px;
    width: 2px;
    border-radius: 99px;
    background: linear-gradient(180deg, var(--cyan), var(--blue), var(--mint));
    opacity: .48;
  }
  .flow-line, .flow-signal, .flow-arrow, .flow-stem { display: none !important; }
  .flow-step,
  .flow-step.is-above,
  .flow-step.is-below {
    position: relative;
    height: auto;
    min-height: 92px;
    display: grid;
    grid-template-columns: 50px minmax(0,1fr);
    align-items: center;
    gap: 12px;
    padding: 8px 0;
  }
  .flow-node {
    position: relative !important;
    left: auto !important;
    top: auto !important;
    bottom: auto !important;
    grid-column: 1;
    grid-row: 1;
    width: 48px;
    height: 48px;
    margin: 0 !important;
    transform: none !important;
    z-index: 2;
  }
  .flow-copy-card {
    position: relative !important;
    left: auto !important;
    top: auto !important;
    bottom: auto !important;
    grid-column: 2;
    grid-row: 1;
    width: 100% !important;
    min-height: 76px !important;
    padding: 12px 14px;
    transform: none !important;
    text-align: left;
  }
  .flow-copy-card small { font-size: 9px; }
  .flow-copy-card b { font-size: 13px; }
  .flow-copy-card p { margin-top: 4px; font-size: 10.5px; line-height: 1.4; }

  /* CTA / FAQ / footer */
  .final-cta {
    grid-template-columns: 64px minmax(0,1fr) !important;
    gap: 14px !important;
    padding: 20px !important;
    text-align: left !important;
  }
  .cta-mascot { width: 60px; height: 72px; margin: 0 !important; }
  .cta-mascot img { width: 58px; }
  .final-cta h2 {
    max-width: none;
    font-size: clamp(26px, 6.8vw, 34px) !important;
    line-height: 1.08;
  }
  .final-cta .btn {
    grid-column: 1 / -1 !important;
    width: 100%;
    justify-self: stretch;
    margin-top: 2px;
  }
  .faq-grid { gap: 28px; }
  .faq-heading h2 { font-size: clamp(32px, 8vw, 40px) !important; line-height: 1.05; }
  .faq-heading p { font-size: 13px; line-height: 1.55; }
  .faq-item button { min-height: 52px; font-size: 12px; }
  .faq-item p { font-size: 12px; line-height: 1.55; }
  .footer-panel {
    grid-template-columns: 1fr 1fr;
    gap: 24px 16px;
    padding: 24px 20px;
  }
  .footer-brand { grid-column: 1 / -1; }
  .footer-brand p { max-width: 42ch; }
  .footer-bottom { grid-column: 1 / -1; }
}

@media (max-width: 540px) {
  .landing-page { --mobile-side: 12px; }
  .section { padding: 50px 0; }

  .landing-nav { width: calc(100% - 12px); border-radius: 16px; }
  .nav-inner { min-height: 56px; padding-inline: 9px; }
  .brand-word { font-size: 14px; }
  .brand-logo { width: 22px; height: 22px; }
  .icon-btn, .lang-btn { min-height: 34px; height: 34px; }
  .icon-btn { width: 34px; }
  .lang-btn { padding-inline: 7px; font-size: 11px; }

  .hero { padding-top: 62px; }
  .hero-copy h1 {
    font-size: clamp(33px, 8.8vw, 38px) !important;
    line-height: 1.085;
    letter-spacing: -.028em;
  }
  .hero-copy .lead { font-size: 14px; }
  .hero-actions { display: grid; grid-template-columns: 1fr 1fr; }
  .hero-actions .btn { width: 100%; padding-inline: 10px; font-size: 11px; }
  .proof-row { display: grid; grid-template-columns: 1fr; gap: 7px; }
  .hero-stage { min-height: 260px; }
  .float-card-a { left: -4px; top: 12%; }
  .float-card-b { right: -4px; top: 32%; }
  .float-card-c { display: none; }

  .center-heading h2,
  .product-title,
  .workflow-heading h2,
  .pricing-heading h2 {
    font-size: clamp(28px, 7.5vw, 34px) !important;
  }
  .center-heading p { font-size: 13px; }

  .product-card, .price-card { flex-basis: 88vw; }
  .product-card { min-height: 288px; }

  .ai-title { font-size: clamp(31px, 8.2vw, 37px) !important; }
  .ai-steps { grid-template-columns: 1fr; }
  .ai-steps article {
    min-height: 86px;
    display: grid;
    grid-template-columns: 42px 30px 1fr;
    grid-template-rows: auto auto;
    align-items: center;
    column-gap: 9px;
    padding: 11px 12px;
    text-align: left;
  }
  .ai-steps .step-icon { grid-row: 1 / span 2; grid-column: 1; }
  .ai-steps article > b { grid-row: 1; grid-column: 2; }
  .ai-steps article > strong { grid-row: 1; grid-column: 3; }
  .ai-steps article > small { grid-row: 2; grid-column: 2 / 4; }
  .ai-copy-actions { flex-direction: column; align-items: stretch; }
  .ai-copy-actions .btn { width: 100%; }
  .trust-note { justify-content: center; }

  .assistant-head { align-items: flex-start; }
  .control-pill { max-width: 112px; text-align: center; }
  .assistant-panel { padding: 12px; }
  .user-line p { max-width: 100%; }
  .confirm-actions .btn { flex: 1 1 120px; }
  .ai-showcase { padding-bottom: 118px; }
  .mascot-stage { width: 106px !important; height: 118px !important; }
  .mascot-stage img { width: 100px !important; }
  .project-float { max-width: calc(100% - 126px); font-size: 8px; }

  .price-card { min-height: 410px; padding: 20px; }
  .pricing-controls { border-radius: 14px; }
  .billing-pill { font-size: 10px; }

  .final-cta {
    grid-template-columns: 54px minmax(0,1fr) !important;
    padding: 18px !important;
  }
  .cta-mascot { width: 52px; height: 62px; }
  .cta-mascot img { width: 50px; }
  .final-cta h2 { font-size: clamp(24px, 6.4vw, 30px) !important; }

  .footer-panel { grid-template-columns: 1fr; }
  .footer-brand { grid-column: auto; }
  .footer-bottom { grid-column: auto; flex-direction: column; gap: 6px; align-items: flex-start; }
}

@media (prefers-reduced-motion: reduce) {
  :global(html) { scroll-behavior:auto; }
  *,*::before,*::after { animation:none !important; transition:none !important; }
  .motion-ready [data-reveal] { opacity:1 !important; transform:none !important; }
  .dashboard-tilt { transform:none !important; }
}
</style>
