<template>
  <div class="jira-for-you-page">
      <!-- Top banner -->
      <div class="welcome-banner">
        <div class="banner-content">
          <div class="date-text">{{ currentDate }}</div>
        <h1 class="welcome-text">{{ homeLabels.greeting }} {{ selectedGreetingAccount }} · {{ userName }}</h1>
        </div>
      </div>

      <div class="content-container">
        <!-- Ứng dụng của bạn -->
        <section class="dashboard-section">
          <div class="section-header">
            <h2>{{ homeLabels.recentOwnerSites }}</h2>
            <a href="#" class="view-all-link" @click.prevent="router.push('/site-selection')">{{ homeLabels.viewAllOwnerSites }} &rarr;</a>
          </div>
          <div class="apps-container">
            <div
              class="app-card"
              v-for="account in ownerAccounts.slice(0, 4)"
              :key="account.id"
              role="button"
              tabindex="0"
              @click="openOwnerDrawer(account)"
              @keydown.enter.prevent="openOwnerDrawer(account)"
            >
              <div class="app-icon">
                <div class="owner-avatar" :class="{ 'has-image': account.avatarUrl }">
                  <img v-if="account.avatarUrl" :src="account.avatarUrl" :alt="account.name" />
                  <span v-else>{{ account.initials }}</span>
                </div>
              </div>
              <div class="app-info">
                <div class="app-name">{{ account.name }}</div>
                <div class="app-url">{{ homeLabels.ownerAccount }}</div>
                <div class="app-meta">
                  <span class="site-owner">{{ account.sites.length }} {{ homeLabels.childSiteCount }}</span>
                  <span v-if="account.isCurrent" class="site-role owner-role">{{ homeLabels.yourAccount }}</span>
                </div>
              </div>
            </div>
            <div class="app-card create-new" @click="openLinkModal">
              <div class="create-icon"><i class="fa-solid fa-link"></i></div>
              <div class="app-info">
                <div class="app-name">{{ homeLabels.requestLink }}</div>
                <div class="app-url">{{ homeLabels.requestLinkDescription }}</div>
              </div>
            </div>
          </div>
        </section>

        <!-- Các site con có quyền truy cập -->
        <section class="dashboard-section">
          <div class="section-header">
            <h2>{{ homeLabels.mySites }}</h2>
          </div>
          <div class="recent-access-container">
            <button type="button" class="create-site-placeholder" @click="openCreateSiteModal">
              <span class="create-icon"><i class="fa-solid fa-plus"></i></span>
              <span class="app-info">
                <span class="app-name">{{ homeLabels.createChildSite }}</span>
                <span class="app-url">{{ homeLabels.createChildSiteDescription }}</span>
              </span>
            </button>
            <div
              class="recent-access-card"
              v-for="site in accessibleSites"
              :key="site.id"
              role="button"
              tabindex="0"
              @click="goToSite(site)"
              @keydown.enter.prevent="goToSite(site)"
            >
              <span class="site-access-avatar">
                <img v-if="site.logo" :src="site.logo" :alt="site.name" />
                <i v-else class="fa-solid fa-folder"></i>
              </span>
              <div class="recent-info">
                <div class="recent-title">{{ site.name }}</div>
                <div class="recent-subtitle">{{ site.ownerName || homeLabels.ownerAccount }} • {{ accessSourceLabel(site) }}</div>
              </div>
            </div>
            <div v-if="accessibleSites.length === 0" class="site-access-empty">
              {{ homeLabels.noAccessibleSites }}
            </div>
          </div>
        </section>

        <!-- Tiếp theo là gì -->
        <section class="dashboard-section">
          <div class="section-header space-between">
            <h2>{{ t('What\'s next') }}</h2>
            <div class="tabs">
              <button class="tab-btn active">{{ t('Worked on') }}</button>
              <button class="tab-btn">{{ t('Viewed') }}</button>
            </div>
          </div>

          <div class="audit-list" v-if="miniActivities.length > 0">
            <div class="time-group">
              <h3 class="time-label">{{ t('Today') }}</h3>
              <div class="audit-item" v-for="activity in miniActivities" :key="activity.id">
                <div class="item-icon light-blue square"><i :class="activity.icon"></i></div>
                <div class="item-details">
                  <div class="item-title">{{ activity.bold || activity.text }}</div>
                  <div class="item-path">{{ activity.text }}</div>
                </div>
                <div class="item-meta">
                  <span class="status-badge pending">{{ activity.raw?.status || 'ACTIVITY' }}</span>
                  <span class="time-ago">{{ activity.time }}</span>
                </div>
              </div>
            </div>
            <button class="view-all-btn" @click="router.push('/home/recent')">{{ t('View all') }}</button>
          </div>
        </section>
      </div>

      <Teleport to="body">
        <div class="modal-overlay sa-data-modal-overlay" v-if="isChildSiteModalVisible" @click.self="isChildSiteModalVisible = false">
          <div class="modal-dialog">
            <div class="modal-header"><DataModalHeader icon="bi bi-folder-plus" :title="homeLabels.createChildSite" :description="homeLabels.createChildSiteDescription" @close="isChildSiteModalVisible = false" /></div>
            <div class="modal-body">
              <DataModalSection icon="bi bi-building" :title="homeLabels.createChildSite">
                <div class="form-group">
                  <label>{{ homeLabels.siteNameLabel }} <span class="required">*</span></label>
                  <input v-model="newChildSiteName" class="text-input" :placeholder="homeLabels.sitePlaceholder" @keyup.enter="submitCreateChildSite" />
                  <div v-if="childSiteError" class="error-message"><i class="fa-solid fa-triangle-exclamation"></i> {{ childSiteError }}</div>
                </div>
              </DataModalSection>
            </div>
            <div class="modal-footer">
              <button class="secondary-btn cancel-btn" @click="isChildSiteModalVisible = false">{{ t('Cancel') }}</button>
              <button class="primary-btn" :disabled="isCreatingChildSite || !newChildSiteName.trim()" @click="submitCreateChildSite">{{ isCreatingChildSite ? homeLabels.creating : homeLabels.createChildSite }}</button>
            </div>
          </div>
        </div>
      </Teleport>

      <!-- Create Site Modal -->
      <Teleport to="body">
      <div class="modal-overlay sa-data-modal-overlay" v-if="isCreateModalVisible" @click.self="isCreateModalVisible = false">
        <div class="modal-dialog">
          <div class="modal-header">
            <DataModalHeader icon="bi bi-link-45deg" :title="homeLabels.requestLinkTitle" :description="homeLabels.requestLinkDescription" @close="isCreateModalVisible = false" />
          </div>
          <div class="modal-body">
            <DataModalSection icon="bi bi-envelope" :title="homeLabels.ownerEmailLabel">
            <div class="form-group">
              <label>{{ homeLabels.ownerEmailLabel }} <span class="required">*</span></label>
              <input type="email" v-model="ownerEmail" :placeholder="homeLabels.ownerEmailPlaceholder" class="text-input" :class="{ 'error': errorMessage }" @keyup.enter="submitLinkRequest" />
              <div v-if="errorMessage" class="error-message">
                <i class="fa-solid fa-triangle-exclamation"></i> {{ errorMessage }}
              </div>
            </div>
            </DataModalSection>
          </div>
          <div class="modal-footer">
            <button class="secondary-btn cancel-btn" @click="isCreateModalVisible = false"><i class="bi bi-x-lg"></i>{{ t('Cancel') }}</button>
            <button class="primary-btn" :disabled="isCreating || !ownerEmail.trim()" @click="submitLinkRequest">
              {{ isCreating ? homeLabels.sendingRequest : homeLabels.requestLink }}
            </button>
          </div>
        </div>
      </div>
      </Teleport>

      <el-drawer
        v-model="isSiteDrawerVisible"
        class="site-children-drawer"
        direction="rtl"
        :with-header="false"
        :size="siteDrawerSize"
        destroy-on-close
      >
        <div class="site-drawer-shell">
          <header class="site-drawer-header">
            <div class="site-drawer-heading">
              <div class="site-drawer-icon" aria-hidden="true">
                <i class="fa-solid fa-user"></i>
              </div>
              <div>
                <p class="site-drawer-eyebrow">{{ homeLabels.ownerAccount }}</p>
                <h2>{{ selectedOwnerAccount?.name }}</h2>
                <p v-if="selectedOwnerAccount?.email">{{ selectedOwnerAccount.email }}</p>
              </div>
            </div>
            <button v-if="selectedOwnerAccount && !selectedOwnerAccount.isCurrent" class="owner-access-button" type="button" @click="accessOwnerAccount(selectedOwnerAccount)">
              <i class="fa-solid fa-arrow-up-right-from-square"></i>
              {{ homeLabels.accessOwnerAccount }}
            </button>
            <button class="site-drawer-close" type="button" :aria-label="homeLabels.close" @click="isSiteDrawerVisible = false">
              <i class="fa-solid fa-xmark"></i>
            </button>
          </header>

          <div class="site-drawer-intro">
            <div>
              <h3>{{ homeLabels.childSites }}</h3>
              <p>{{ homeLabels.childSitesDescription }}</p>
            </div>
            <span class="site-count-badge">{{ childSites.length }}</span>
          </div>

          <div v-if="childSites.length === 0" class="site-drawer-empty">
            <div class="site-drawer-empty-icon"><i class="fa-solid fa-lock"></i></div>
            <h3>{{ homeLabels.noAccessibleChildSites }}</h3>
            <p>{{ homeLabels.noAccessibleChildSitesDescription }}</p>
          </div>

          <div v-else class="child-site-list">
            <button
              v-for="site in childSites"
              :key="site.id"
              class="child-site-card"
              type="button"
              @click="goToSite(site)"
            >
              <span class="child-site-avatar">
                <img v-if="site.logo" :src="site.logo" :alt="site.name" />
                <i v-else class="fa-solid fa-folder"></i>
              </span>
              <span class="child-site-copy">
                <strong>{{ site.name }}</strong>
                <small>{{ site.slug || homeLabels.childSiteFallback }}</small>
              </span>
              <span class="child-site-meta">
                <small>{{ accessSourceLabel(site) }}</small>
                <i class="fa-solid fa-chevron-right"></i>
              </span>
            </button>
          </div>
        </div>
      </el-drawer>

  </div>
</template>

<script setup>
import { ref, computed, onMounted, onUnmounted } from 'vue'
import { useRouter } from 'vue-router'
import { ElMessage } from 'element-plus'
import { useSiteStore } from '@/store/useSiteStore'
import { useI18nStore } from '@/store/useI18nStore'
import { useActivityStore } from '@/store/useActivityStore'
import { useStarredStore } from '@/store/useStarredStore'
import { getStoredUser } from '@/utils/permissions'
import axiosClient from '@/api/axiosClient'
import { signalRService } from '@/api/signalrService'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'

const router = useRouter()
const siteStore = useSiteStore()
const i18nStore = useI18nStore()
const activityStore = useActivityStore()
const starredStore = useStarredStore()
const t = (key) => i18nStore.t(key)

const currentUser = getStoredUser()
const homeLabels = computed(() => i18nStore.locale === 'vi'
  ? {
      recentOwnerSites: 'Các tài khoản site chủ gần đây',
      greeting: 'Xin chào site chủ của',
      mySites: 'Site của chúng ta',
      viewAllOwnerSites: 'Xem tất cả tài khoản',
      ownerAccount: 'Tài khoản site chủ',
      yourAccount: 'TÀI KHOẢN CỦA BẠN',
      childSiteCount: 'site con',
      childSites: 'Các site con bạn có quyền truy cập',
      childSitesDescription: 'Chỉ hiển thị site con bạn được mời trực tiếp hoặc thông qua team.',
      noAccessibleChildSites: 'Tài khoản này chưa có site con khả dụng',
      noAccessibleChildSitesDescription: 'Bạn chỉ truy cập được site con khi chủ tài khoản mời trực tiếp hoặc cấp quyền cho team của bạn.',
      childSiteFallback: 'Workspace',
      accessOwner: 'Site của bạn',
      accessDirect: 'Được mời trực tiếp',
      accessTeam: 'Thông qua team',
      accessibleSites: 'Danh sách các site của bạn',
      noAccessibleSites: 'Chưa có site con nào được cấp quyền truy cập.',
      requestLink: 'Xin tham gia site chủ',
      requestLinkTitle: 'Xin liên kết với tài khoản site chủ',
      requestLinkDescription: 'Nhập Gmail của chủ tài khoản để gửi yêu cầu liên kết.',
      ownerEmailLabel: 'Gmail của chủ tài khoản',
      ownerEmailPlaceholder: 'name@example.com',
      sendingRequest: 'Đang gửi...',
      requestSent: 'Đã gửi yêu cầu liên kết. Vui lòng chờ chủ tài khoản chấp thuận.',
      invalidEmail: 'Vui lòng nhập Gmail hợp lệ.',
      close: 'Đóng',
      project: 'Dự án',
      homeSite: 'Homesite',
      you: 'bạn',
      sitePlaceholder: 'VD: Nhóm sản phẩm của tôi',
      siteNameRequired: 'Bạn cần nhập tên site',
      requestFailed: 'Không thể gửi yêu cầu liên kết.',
      accessOwnerAccount: 'Truy cập site chủ',
      createChildSite: 'Tạo site con', createChildSiteDescription: 'Tạo site con thuộc tài khoản của bạn.', siteNameLabel: 'Tên site', creating: 'Đang tạo...'
    }
  : {
      recentOwnerSites: 'Recent owner accounts',
      greeting: 'Hello, owner site of',
      mySites: 'Our sites',
      viewAllOwnerSites: 'View all accounts',
      ownerAccount: 'Owner account',
      yourAccount: 'YOUR ACCOUNT',
      childSiteCount: 'child sites',
      childSites: 'Child sites you can access',
      childSitesDescription: 'Only child sites shared with you directly or through a team are shown.',
      noAccessibleChildSites: 'This account has no available child sites',
      noAccessibleChildSitesDescription: 'You can access a child site only when its owner invites you directly or grants access to your team.',
      childSiteFallback: 'Workspace',
      accessOwner: 'Your site',
      accessDirect: 'Direct invitation',
      accessTeam: 'Through a team',
      accessibleSites: 'Sites you can access',
      noAccessibleSites: 'No child site access has been granted yet.',
      requestLink: 'Request owner link',
      requestLinkTitle: 'Request a link to an owner account',
      requestLinkDescription: 'Enter the owner account email to send a link request.',
      ownerEmailLabel: 'Owner account email',
      ownerEmailPlaceholder: 'name@example.com',
      sendingRequest: 'Sending...',
      requestSent: 'Link request sent. Wait for the owner to approve it.',
      invalidEmail: 'Please enter a valid email address.',
      close: 'Close',
      project: 'Project',
      homeSite: 'Homesite',
      you: 'you',
      sitePlaceholder: 'e.g. My product team',
      siteNameRequired: 'Space name is required',
      requestFailed: 'Failed to send the link request.',
      accessOwnerAccount: 'Access owner site',
      createChildSite: 'Create child site', createChildSiteDescription: 'Create a child site under your account.', siteNameLabel: 'Site name', creating: 'Creating...'
    })
const userName = computed(() => currentUser?.fullName || currentUser?.username || currentUser?.email || homeLabels.value.you)
const selectedGreetingAccount = computed(() => selectedOwnerAccount.value?.name || ownerAccounts.value.find(account => account.isCurrent)?.name || homeLabels.value.ownerAccount)

// Format current date in Vietnamese
const currentDate = computed(() => {
  return new Intl.DateTimeFormat(i18nStore.locale === 'vi' ? 'vi-VN' : 'en-US', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric'
  }).format(new Date())
})

const loading = ref(false)

const isCreateModalVisible = ref(false)
const isChildSiteModalVisible = ref(false)
const newChildSiteName = ref('')
const isCreatingChildSite = ref(false)
const childSiteError = ref('')
const ownerEmail = ref('')
const isCreating = ref(false)
const errorMessage = ref('')
const isSiteDrawerVisible = ref(false)
const selectedOwnerAccount = ref(null)
const siteDrawerSize = 'min(720px, 52vw)'
const linkedOwnerAccounts = ref([])

const currentUserId = String(currentUser?.id || currentUser?.Id || '')
const accountInitials = (name) => String(name || '?')
  .trim()
  .split(/\s+/)
  .filter(Boolean)
  .slice(-2)
  .map(part => part[0]?.toUpperCase())
  .join('') || '?'

const ownerAccounts = computed(() => {
  const accounts = new Map()
  const currentAccountKey = currentUserId || currentUser?.email || '__current_account__'

  accounts.set(String(currentAccountKey), {
    id: String(currentAccountKey),
    name: currentUser?.fullName || currentUser?.username || currentUser?.email || homeLabels.value.you,
    email: currentUser?.email || '',
    avatarUrl: currentUser?.avatarUrl || currentUser?.avatar || '',
    initials: accountInitials(currentUser?.fullName || currentUser?.username || currentUser?.email),
    isCurrent: true,
    sites: [],
    updatedAt: 0
  })

  for (const site of siteStore.sites || []) {
    const ownerId = String(site.ownerId || (site.workspaceRole === 'OWNER' ? currentAccountKey : '') || site.ownerEmail || site.ownerName || site.id)
    const isCurrent = ownerId === String(currentAccountKey)
    if (!accounts.has(ownerId)) {
      const ownerName = site.ownerName || site.ownerEmail || homeLabels.value.ownerAccount
      accounts.set(ownerId, {
        id: ownerId,
        name: ownerName,
        email: site.ownerEmail || '',
        avatarUrl: site.ownerAvatarUrl || '',
        initials: accountInitials(ownerName),
        isCurrent,
        sites: [],
        updatedAt: 0
      })
    }

    const account = accounts.get(ownerId)
    account.sites.push(site)
    account.updatedAt = Math.max(account.updatedAt, new Date(site.updatedAt || site.createdAt || 0).getTime() || 0)
  }

  for (const linked of linkedOwnerAccounts.value || []) {
    const ownerId = String(linked.id || linked.Id || linked.email || '')
    if (!ownerId || accounts.has(ownerId)) continue
    const ownerName = linked.name || linked.Name || linked.email || linked.Email || homeLabels.value.ownerAccount
    accounts.set(ownerId, {
      id: ownerId,
      name: ownerName,
      email: linked.email || linked.Email || '',
      avatarUrl: linked.avatarUrl || linked.AvatarUrl || '',
      initials: accountInitials(ownerName),
      isCurrent: false,
      sites: [],
      updatedAt: 0
    })
  }

  const recentOwnerId = localStorage.getItem('recent_site_owner_id')
  return [...accounts.values()]
    .map(account => ({
      ...account,
      sites: [...account.sites].sort((left, right) => new Date(right.updatedAt || right.createdAt || 0) - new Date(left.updatedAt || left.createdAt || 0))
    }))
    .sort((left, right) => {
      if (left.id === recentOwnerId) return -1
      if (right.id === recentOwnerId) return 1
      if (left.isCurrent !== right.isCurrent) return left.isCurrent ? -1 : 1
      return right.updatedAt - left.updatedAt
    })
})

const childSites = computed(() => selectedOwnerAccount.value?.sites || [])

const loadSites = async () => {
  loading.value = true
  try {
    await siteStore.fetchSites()
  } catch (error) {
    console.error('Fetch sites error:', error)
  } finally {
    loading.value = false
  }
}

const fetchLinkedOwnerAccounts = async () => {
  try {
    const response = await axiosClient.get('/site-account-links')
    linkedOwnerAccounts.value = response.data?.data || []
  } catch (error) {
    console.error('Fetch linked owner accounts error:', error)
  }
}

onMounted(() => {
  loadSites()
  fetchLinkedOwnerAccounts()
  activityStore.fetchRecentActivities({ limit: 5 })
  fetchRecentViews()
})

const openOwnerDrawer = (account) => {
  if (!account?.id) return
  selectedOwnerAccount.value = account
  localStorage.setItem('recent_site_owner_id', account.id)
  isSiteDrawerVisible.value = true
}

const accessOwnerAccount = (account) => {
  if (!account?.id) return
  selectedOwnerAccount.value = account
  localStorage.setItem('recent_site_owner_id', account.id)
  isSiteDrawerVisible.value = false
}

const accessSourceLabel = (site) => {
  const accessSource = String(site?.accessSource || '').toUpperCase()
  if (accessSource === 'OWNER') return homeLabels.value.accessOwner
  if (accessSource === 'TEAM') return homeLabels.value.accessTeam
  return homeLabels.value.accessDirect
}

const fetchRecentViews = async () => {
  await starredStore.fetchRecentItems({ page: 1, pageSize: 8 }).catch(() => {})
}

let recentViewRefreshTimer = null
const handleRecentViewChanged = (event) => {
  if (event?.entityType !== 'RecentView') return
  if (recentViewRefreshTimer) clearTimeout(recentViewRefreshTimer)
  recentViewRefreshTimer = setTimeout(fetchRecentViews, 50)
}

onMounted(() => {
  signalRService.on('EntityChanged', handleRecentViewChanged)
  signalRService.startAuthenticatedConnection()
})

onUnmounted(() => {
  signalRService.off('EntityChanged', handleRecentViewChanged)
  if (recentViewRefreshTimer) clearTimeout(recentViewRefreshTimer)
})

const accessibleSites = computed(() => [...(siteStore.sites || [])]
  .sort((left, right) => new Date(right.updatedAt || right.createdAt || 0) - new Date(left.updatedAt || left.createdAt || 0)))

const miniActivities = computed(() => (activityStore.activities || []).slice(0, 5))

const goToSite = (siteOrId) => {
  const id = typeof siteOrId === 'object' ? siteOrId?.id : siteOrId
  if (!id) return
  const site = typeof siteOrId === 'object' ? siteOrId : siteStore.sites.find(s => s.id === id)
  siteStore.setRecentSite(site || { id })
  isSiteDrawerVisible.value = false
  router.push('/dashboard')
}

const openLinkModal = () => {
  isCreateModalVisible.value = true
  ownerEmail.value = ''
  errorMessage.value = ''
}

const openCreateSiteModal = () => {
  newChildSiteName.value = ''
  childSiteError.value = ''
  isChildSiteModalVisible.value = true
}

const submitCreateChildSite = async () => {
  const name = newChildSiteName.value.trim()
  if (!name) return
  isCreatingChildSite.value = true
  childSiteError.value = ''
  try {
    await siteStore.createSite({ name })
    isChildSiteModalVisible.value = false
    ElMessage.success(homeLabels.value.createChildSite)
    await siteStore.fetchSites()
  } catch (error) {
    childSiteError.value = error.response?.data?.message || homeLabels.value.requestFailed
  } finally {
    isCreatingChildSite.value = false
  }
}

const submitLinkRequest = async () => {
  const email = ownerEmail.value.trim().toLowerCase()
  if (!email || !/^\S+@\S+\.\S+$/.test(email)) {
    errorMessage.value = homeLabels.value.invalidEmail
    return
  }
  isCreating.value = true
  errorMessage.value = ''
  try {
    await axiosClient.post('/site-account-links', { email })
    isCreateModalVisible.value = false
    ownerEmail.value = ''
    ElMessage.success(homeLabels.value.requestSent)
  } catch (error) {
    errorMessage.value = error.response?.data?.message || homeLabels.value.requestFailed
  } finally {
    isCreating.value = false
  }
}
</script>

<style scoped>
.jira-for-you-page {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  color: #172B4D;
  background-color: #FFFFFF;
  min-height: calc(100vh - 56px);
  padding: 0;
  display: flex;
  flex-direction: column;
}

/* Welcome Banner */
.welcome-banner {
  background-color: #FFC400;
  background-image: url('data:image/svg+xml;utf8,<svg width="100%" height="100%" xmlns="http://www.w3.org/2000/svg"><defs><pattern id="grid" width="40" height="40" patternUnits="userSpaceOnUse"><path d="M 40 0 L 0 0 0 40" fill="none" stroke="rgba(0,0,0,0.05)" stroke-width="1"/></pattern></defs><rect width="100%" height="100%" fill="url(%23grid)"/><path d="M 600 120 L 700 40 L 800 100 L 900 20" stroke="%23172B4D" stroke-width="3" fill="none" /><circle cx="900" cy="20" r="4" fill="%23172B4D" /></svg>');
  background-position: right center;
  background-repeat: no-repeat;
  width: 100%;
  box-sizing: border-box;
  padding: 32px 40px;
  min-height: 120px;
  display: flex;
  align-items: center;
  border-radius: 4px;
  margin: 24px 40px;
  position: relative;
  overflow: hidden;
}

.banner-content {
  position: relative;
  z-index: 2;
}

.date-text {
  font-size: 14px;
  font-weight: 500;
  color: #172B4D;
  margin-bottom: 4px;
  text-transform: capitalize;
}

.welcome-text {
  font-size: 24px;
  font-weight: 600;
  color: #172B4D;
  margin: 0;
}

.content-container {
  padding: 0 40px 40px;
  max-width: 1000px;
}

.dashboard-section {
  margin-bottom: 40px;
}

.section-header {
  display: flex;
  align-items: center;
  margin-bottom: 16px;
}

.section-header.space-between {
  justify-content: space-between;
}

.section-header h2 {
  font-size: 16px;
  font-weight: 600;
  color: #172B4D;
  margin: 0;
  margin-right: auto;
}

.view-all-link {
  font-size: 13px;
  color: #5E6C84;
  text-decoration: none;
}

.view-all-link:hover {
  text-decoration: underline;
}

/* Apps Container */
.apps-container {
  display: flex;
  gap: 16px;
  flex-wrap: wrap;
}

.child-site-create-section {
  margin-top: 18px;
}

.create-site-row {
  display: flex;
  flex-wrap: wrap;
  gap: 14px;
}

.create-site-placeholder {
  width: 300px;
  min-height: 74px;
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 14px 16px;
  text-align: left;
  color: #0052cc;
  background: #fff;
  border: 1px dashed #b7d9f5;
  border-radius: 8px;
  cursor: pointer;
  transition: border-color .2s, background-color .2s, transform .2s;
}

.create-site-placeholder:hover {
  background: #f0f8ff;
  border-color: #0ea5e9;
  transform: translateY(-1px);
}

.create-site-placeholder .app-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.create-site-placeholder .app-name,
.create-site-placeholder .app-url {
  display: block;
}

.app-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border: 1px solid #DFE1E6;
  border-radius: 4px;
  background: #FFFFFF;
  cursor: pointer;
  transition: box-shadow 0.2s, background-color 0.2s;
  min-width: 220px;
}

.app-card:hover {
  background-color: #FAFBFC;
  box-shadow: 0 1px 2px rgba(9, 30, 66, 0.25);
}

.app-icon .jira-icon-wrapper {
  width: 24px;
  height: 24px;
  background-color: #0052CC;
  color: white;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
}

.owner-avatar,
.child-site-avatar {
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  overflow: hidden;
  color: #ffffff;
  background: linear-gradient(145deg, #0f6ad8, #0ea5e9);
  font-weight: 750;
}

.owner-avatar {
  width: 36px;
  height: 36px;
  border-radius: 10px;
  font-size: 11px;
  box-shadow: 0 5px 14px rgba(14, 116, 200, 0.2);
}

.owner-avatar img,
.child-site-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.create-new {
  border-style: dashed;
  color: #0052CC;
}

.create-new .app-name {
  color: #0052CC;
}

.create-icon {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
}

.app-name {
  font-size: 12px;
  color: #172B4D;
  font-weight: 500;
}

.app-url {
  font-size: 11px;
  color: #5E6C84;
}

.app-meta {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-top: 5px;
  min-height: 18px;
}

.site-owner {
  max-width: 130px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  font-size: 10px;
  color: var(--home-muted, #5E6C84);
}

.site-role {
  border-radius: 999px;
  padding: 2px 6px;
  font-size: 9px;
  font-weight: 700;
  letter-spacing: 0;
  line-height: 1;
}

.site-role.owner-role {
  background: rgba(14, 165, 233, 0.14);
  color: #0369a1;
}

.site-role.member-role {
  background: rgba(100, 116, 139, 0.12);
  color: #475569;
}

/* Recent Access */
.recent-access-card {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 12px 16px;
  border: 1px solid #DFE1E6;
  border-radius: 4px;
  background: #FFFFFF;
  max-width: 300px;
}

.recent-access-container {
  display: flex;
  align-items: stretch;
  gap: 14px;
  flex-wrap: wrap;
}

.recent-access-container .recent-access-card,
.recent-access-container .create-site-placeholder {
  flex: 0 0 300px;
  box-sizing: border-box;
}

.site-access-avatar {
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  width: 36px;
  height: 36px;
  overflow: hidden;
  border-radius: 10px;
  color: #ffffff;
  background: linear-gradient(145deg, #0f6ad8, #0ea5e9);
  font-size: 15px;
}

.site-access-avatar img {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.site-access-empty {
  width: 100%;
  padding: 20px;
  border: 1px dashed #dfe1e6;
  border-radius: 10px;
  color: var(--home-muted, #5e6c84);
  text-align: center;
}

.recent-icon.purple {
  width: 32px;
  height: 32px;
  background-color: #EAE6FF;
  color: #403294;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
}

.recent-title {
  font-size: 14px;
  font-weight: 500;
  color: #172B4D;
}

.recent-subtitle {
  font-size: 12px;
  color: #5E6C84;
}

/* Audit List Tabs */
.tabs {
  display: flex;
  background: #F4F5F7;
  border-radius: 3px;
  padding: 2px;
}

.tab-btn {
  background: transparent;
  border: none;
  padding: 6px 12px;
  font-size: 13px;
  font-weight: 500;
  color: #5E6C84;
  border-radius: 3px;
  cursor: pointer;
}

.tab-btn.active {
  background: #FFFFFF;
  color: #172B4D;
  box-shadow: 0 1px 1px rgba(9, 30, 66, 0.25);
}

/* Audit List */
.audit-list {
  display: flex;
  flex-direction: column;
  gap: 24px;
}

.time-group {
  display: flex;
  flex-direction: column;
}

.time-label {
  font-size: 12px;
  font-weight: 600;
  color: #5E6C84;
  margin: 0 0 8px 0;
}

.audit-item {
  display: flex;
  align-items: center;
  padding: 12px 0;
  border-bottom: 1px solid #DFE1E6;
  gap: 16px;
}

.audit-item:last-child {
  border-bottom: none;
}

.item-icon {
  width: 24px;
  height: 24px;
  display: flex;
  align-items: center;
  justify-content: center;
  border-radius: 50%;
  font-size: 12px;
}

.item-icon.square {
  border-radius: 3px;
}

.yellow-bg {
  background: #FFFAE6;
  font-size: 16px;
}

.light-blue {
  background: #E6FCFF;
  color: #00B8D9;
}

.item-details {
  flex: 1;
}

.item-title {
  font-size: 14px;
  font-weight: 500;
  color: #172B4D;
  margin-bottom: 2px;
}

.item-path {
  font-size: 12px;
  color: #5E6C84;
}

.item-meta {
  display: flex;
  align-items: center;
  gap: 16px;
}

.status-badge {
  font-size: 11px;
  font-weight: 700;
  padding: 2px 6px;
  border-radius: 3px;
}

.status-badge.pending {
  background: #DFE1E6;
  color: #42526E;
}

.status-badge.draft {
  background: #DFE1E6;
  color: #42526E;
}

.status-badge.todo {
  background: #DFE1E6;
  color: #42526E;
}

.time-ago {
  font-size: 12px;
  color: #5E6C84;
  min-width: 80px;
  text-align: right;
}

.view-all-btn {
  background: transparent;
  border: 1px solid #DFE1E6;
  border-radius: 3px;
  padding: 8px 16px;
  font-size: 14px;
  font-weight: 500;
  color: #172B4D;
  cursor: pointer;
  align-self: flex-start;
  transition: background-color 0.2s;
}

.view-all-btn:hover {
  background: #F4F5F7;
}

/* Modal styles preserved from original */
.modal-overlay {
  position: fixed;
  top: 0; left: 0; right: 0; bottom: 0;
  background-color: rgba(9, 30, 66, 0.54);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}
.modal-dialog {
  background-color: #FFFFFF;
  border-radius: 3px;
  width: 400px;
  box-shadow: 0 8px 16px -4px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
}
.modal-header {
  padding: 20px 24px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid #DFE1E6;
}
.modal-header h2 { margin: 0; font-size: 20px; font-weight: 500; color: #172B4D; }
.close-btn { background: none; border: none; font-size: 16px; color: #6B778C; cursor: pointer; padding: 4px; border-radius: 3px; }
.close-btn:hover { background-color: rgba(9, 30, 66, 0.08); }
.modal-body { padding: 24px; }
.form-group label { display: block; font-size: 12px; font-weight: 600; color: #5E6C84; margin-bottom: 8px; }
.required { color: #DE350B; }
.text-input { width: 100%; padding: 8px 12px; border: 2px solid #DFE1E6; border-radius: 3px; font-size: 14px; color: #091E42; box-sizing: border-box; outline: none; }
.text-input:focus { border-color: #4C9AFF; }
.error-message { color: #DE350B; font-size: 12px; margin-top: 8px; }
.modal-footer { padding: 16px 24px; display: flex; justify-content: flex-end; gap: 8px; border-top: 1px solid #DFE1E6; }
.primary-btn { background-color: #0052CC; color: white; border: none; padding: 6px 12px; border-radius: 3px; cursor: pointer; }
.primary-btn:hover { background-color: #0047B3; }
.primary-btn:disabled { background-color: #EBECF0; color: #A5ADBA; cursor: not-allowed; }
.secondary-btn { background: #F4F5F7; border: none; padding: 6px 12px; border-radius: 3px; cursor: pointer; }
.secondary-btn:hover { background: #EBECF0; }

/* Theme-aware home refresh */
.jira-for-you-page {
  background: var(--home-bg, #ffffff);
  color: var(--home-text, #172b4d);
}

.welcome-banner {
  background-color: transparent;
  background-image:
    linear-gradient(135deg, rgba(14, 165, 233, 0.16), rgba(34, 197, 94, 0.10)),
    url('data:image/svg+xml;utf8,<svg width="100%" height="100%" xmlns="http://www.w3.org/2000/svg"><defs><pattern id="grid2" width="44" height="44" patternUnits="userSpaceOnUse"><path d="M 44 0 L 0 0 0 44" fill="none" stroke="rgba(14,165,233,0.14)" stroke-width="1"/></pattern></defs><rect width="100%" height="100%" fill="url(%23grid2)"/><path d="M 620 115 L 700 48 L 790 104 L 900 28" stroke="%230ea5e9" stroke-width="3" fill="none" /><circle cx="900" cy="28" r="4" fill="%230ea5e9" /></svg>');
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 12px;
  box-shadow: 0 18px 45px rgba(2, 6, 23, 0.10);
}

[data-theme='dark'] .welcome-banner {
  background-image:
    linear-gradient(135deg, rgba(14, 165, 233, 0.18), rgba(15, 23, 42, 0.92)),
    url('data:image/svg+xml;utf8,<svg width="100%" height="100%" xmlns="http://www.w3.org/2000/svg"><defs><pattern id="grid3" width="44" height="44" patternUnits="userSpaceOnUse"><path d="M 44 0 L 0 0 0 44" fill="none" stroke="rgba(125,211,252,0.10)" stroke-width="1"/></pattern></defs><rect width="100%" height="100%" fill="url(%23grid3)"/><path d="M 620 115 L 700 48 L 790 104 L 900 28" stroke="%237dd3fc" stroke-width="3" fill="none" /><circle cx="900" cy="28" r="4" fill="%237dd3fc" /></svg>');
}

.date-text,
.welcome-text,
.section-header h2,
.app-name,
.recent-title,
.item-title,
.modal-header h2 {
  color: var(--home-text, #172b4d);
}

.app-url,
.recent-subtitle,
.view-all-link,
.time-label,
.item-path,
.time-ago,
.form-group label {
  color: var(--home-muted, #5e6c84);
}

.content-container {
  width: min(100%, 1120px);
  max-width: none;
}

.app-card,
.recent-access-card,
.audit-item,
.modal-dialog {
  background: var(--home-panel, #ffffff);
  border-color: var(--home-border, #dfe1e6);
}

.app-card:hover,
.recent-access-card:hover,
.audit-item:hover {
  background: var(--home-panel-strong, #fafbfc);
  border-color: rgba(56, 189, 248, 0.55);
}

.tabs {
  background: var(--home-panel-strong, #f4f5f7);
  border: 1px solid var(--home-border, #dfe1e6);
}

.tab-btn {
  color: var(--home-muted, #5e6c84);
}

.tab-btn.active {
  background: var(--home-panel, #ffffff);
  color: var(--home-text, #172b4d);
}

.view-all-btn,
.secondary-btn,
.text-input {
  background: var(--home-panel, #ffffff);
  border-color: var(--home-border, #dfe1e6);
  color: var(--home-text, #172b4d);
}

.text-input:focus {
  background: var(--home-panel, #ffffff);
}

:global(.site-children-drawer.el-drawer) {
  background: var(--home-bg, #f8fafc);
  box-shadow: -24px 0 60px rgba(15, 23, 42, 0.18);
}

:global(.site-children-drawer .el-drawer__body) {
  padding: 0;
  overflow: hidden;
}

.site-drawer-shell {
  display: flex;
  flex-direction: column;
  height: 100%;
  min-height: 0;
  color: var(--home-text, #172b4d);
}

.site-drawer-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 24px;
  padding: 24px 28px 20px;
  border-bottom: 1px solid var(--home-border, #dfe1e6);
  background: var(--home-panel, #ffffff);
}

.site-drawer-heading {
  display: flex;
  align-items: center;
  gap: 14px;
  min-width: 0;
}

.site-drawer-heading > div:last-child {
  min-width: 0;
}

.site-drawer-icon,
.site-drawer-empty-icon {
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  width: 44px;
  height: 44px;
  border: 1px solid rgba(14, 165, 233, 0.25);
  border-radius: 12px;
  color: #0284c7;
  background: linear-gradient(145deg, rgba(14, 165, 233, 0.14), rgba(56, 189, 248, 0.05));
}

.site-drawer-eyebrow {
  margin: 0 0 3px;
  color: #0284c7 !important;
  font-size: 11px;
  font-weight: 750;
  letter-spacing: 0.08em;
  text-transform: uppercase;
}

.site-drawer-heading h2 {
  overflow: hidden;
  margin: 0;
  color: var(--home-text, #172b4d);
  font-size: 21px;
  font-weight: 700;
  line-height: 1.25;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.site-drawer-heading p:last-child {
  margin: 4px 0 0;
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
}

.site-drawer-close {
  display: grid;
  place-items: center;
  flex: 0 0 auto;
  width: 36px;
  height: 36px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 10px;
  color: var(--home-muted, #5e6c84);
  background: var(--home-panel-strong, #f4f5f7);
  cursor: pointer;
  transition: border-color 0.2s, color 0.2s, background 0.2s;
}

.site-drawer-close:hover {
  border-color: rgba(14, 165, 233, 0.5);
  color: #0284c7;
  background: rgba(14, 165, 233, 0.08);
}

.owner-access-button {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 14px;
  border: 1px solid #b7d9f5;
  border-radius: 8px;
  background: #eff8ff;
  color: #0369a1;
  font-weight: 700;
  cursor: pointer;
}

.owner-access-button:hover {
  background: #dff2ff;
  border-color: #0ea5e9;
}

.site-drawer-intro {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 22px 28px 14px;
}

.site-drawer-intro h3 {
  margin: 0 0 5px;
  color: var(--home-text, #172b4d);
  font-size: 15px;
  font-weight: 700;
}

.site-drawer-intro p {
  margin: 0;
  color: var(--home-muted, #5e6c84);
  font-size: 12px;
  line-height: 1.55;
}

.site-count-badge {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-width: 28px;
  height: 28px;
  padding: 0 9px;
  border-radius: 999px;
  color: #0369a1;
  background: rgba(14, 165, 233, 0.12);
  font-size: 12px;
  font-weight: 750;
}

.child-site-list {
  display: grid;
  gap: 10px;
  min-height: 0;
  padding: 10px 28px 28px;
  overflow-y: auto;
}

.child-site-card {
  display: flex;
  align-items: center;
  width: 100%;
  gap: 14px;
  padding: 14px 16px;
  border: 1px solid var(--home-border, #dfe1e6);
  border-radius: 12px;
  color: var(--home-text, #172b4d);
  text-align: left;
  background: var(--home-panel, #ffffff);
  cursor: pointer;
  transition: transform 0.18s ease, border-color 0.18s ease, box-shadow 0.18s ease;
}

.child-site-card:hover {
  border-color: rgba(14, 165, 233, 0.55);
  box-shadow: 0 12px 28px rgba(15, 23, 42, 0.09);
  transform: translateY(-1px);
}

.child-site-avatar {
  width: 42px;
  height: 42px;
  border-radius: 11px;
  font-size: 16px;
}

.child-site-copy {
  display: grid;
  flex: 1;
  min-width: 0;
  gap: 4px;
}

.child-site-copy strong,
.child-site-copy small {
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.child-site-copy strong {
  font-size: 14px;
  font-weight: 700;
}

.child-site-copy small,
.child-site-meta small {
  color: var(--home-muted, #5e6c84);
  font-size: 11px;
}

.child-site-meta {
  display: flex;
  align-items: center;
  flex: 0 0 auto;
  gap: 12px;
}

.child-site-meta i {
  color: #94a3b8;
  font-size: 11px;
}

.site-drawer-state,
.site-drawer-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  flex: 1;
  min-height: 240px;
  margin: 10px 28px 28px;
  border: 1px dashed var(--home-border, #dfe1e6);
  border-radius: 14px;
  color: var(--home-muted, #5e6c84);
  background: color-mix(in srgb, var(--home-panel, #ffffff) 82%, transparent);
}

.site-drawer-state {
  gap: 9px;
  font-size: 13px;
}

.site-drawer-state button {
  margin-left: 4px;
  border: 0;
  color: #0284c7;
  font: inherit;
  font-weight: 700;
  background: transparent;
  cursor: pointer;
}

.site-drawer-error {
  color: #dc2626;
}

.site-drawer-empty {
  flex-direction: column;
  padding: 32px;
  text-align: center;
}

.site-drawer-empty-icon {
  width: 52px;
  height: 52px;
  margin-bottom: 14px;
  color: #64748b;
  border-color: rgba(100, 116, 139, 0.22);
  background: rgba(100, 116, 139, 0.08);
}

.site-drawer-empty h3 {
  margin: 0 0 7px;
  color: var(--home-text, #172b4d);
  font-size: 15px;
}

.site-drawer-empty p {
  max-width: 400px;
  margin: 0;
  font-size: 12px;
  line-height: 1.6;
}

@media (max-width: 780px) {
  :global(.site-children-drawer.el-drawer) {
    width: 100% !important;
  }

  .site-drawer-header,
  .site-drawer-intro {
    padding-right: 20px;
    padding-left: 20px;
  }

  .child-site-list {
    padding-right: 20px;
    padding-left: 20px;
  }

  .child-site-meta small {
    display: none;
  }
}
</style>
