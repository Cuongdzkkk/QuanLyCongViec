<template>
  <div class="home-site-container sprinta-layout-container">
    <AppTopBar @toggle-create="showCreateModal = true" />

    <div class="home-content-wrapper">
      <!-- Sidebar Jira Style -->
      <aside class="sidebar">
        <div class="sidebar-header">
          <div class="site-switcher" @click="goToSiteSelection">
            <div class="site-icon">S</div>
            <div class="site-info">
              <span class="site-name">SprintA Home</span>
              <span class="site-subtitle">{{ t('Site Management') }}</span>
            </div>
            <span class="dropdown-icon">▾</span>
          </div>
        </div>
        
        <nav class="sidebar-nav">
          <router-link to="/home/for-you" class="nav-item" :class="{ 'active-nav': route.path === '/home/for-you' || route.path === '/sites' }">
            <span class="nav-icon"><i class="fa-regular fa-user-circle"></i></span>
            <span>{{ t('For you', 'Dành cho bạn') }}</span>
          </router-link>
          <router-link to="/home/recent" class="nav-item" :class="{ 'active-nav': $route.path === '/home/recent' }">
            <span class="nav-icon"><i class="fa-regular fa-clock"></i></span>
            <span>{{ t('Recent', 'Gần đây') }}</span>
          </router-link>
          <router-link to="/home/starred" class="nav-item" :class="{ 'active-nav': isModule('starred') }">
            <span class="nav-icon"><i class="fa-regular fa-star"></i></span>
            <span>{{ t('Starred', 'Có gắn sao') }}</span>
          </router-link>
          <router-link to="/home/notifications" class="nav-item" :class="{ 'active-nav': isModule('notifications') }">
            <span class="nav-icon"><i class="fa-regular fa-bell"></i></span>
            <span>{{ t('Notifications', 'Thông báo') }}</span>
          </router-link>
          <router-link to="/home/status" class="nav-item" :class="{ 'active-nav': isModule('status') }">
            <span class="nav-icon"><i class="fa-solid fa-bullhorn"></i></span>
            <span>{{ t('Status', 'Cập nhật trạng thái') }}</span>
          </router-link>
          
          <div class="nav-divider"></div>
          
          <router-link to="/site-selection" class="nav-item">
            <span class="nav-icon jira-blue"><i class="fa-brands fa-jira"></i></span>
            <span>SprintA</span>
          </router-link>
          <router-link to="/home/teams" class="nav-item" :class="{ 'active-nav': isModule('teams') }">
            <span class="nav-icon"><i class="fa-solid fa-users"></i></span>
            <span>{{ t('Teams') }}</span>
          </router-link>
          <router-link to="/home/goals" class="nav-item" :class="{ 'active-nav': isModule('goals') }">
            <span class="nav-icon"><i class="fa-solid fa-bullseye"></i></span>
            <span>{{ t('Goals') }}</span>
          </router-link>
          <router-link to="/home/projects" class="nav-item" :class="{ 'active-nav': isModule('projects') }">
            <span class="nav-icon"><i class="fa-solid fa-rocket"></i></span>
            <span>{{ t('Projects') }}</span>
          </router-link>
        </nav>
      </aside>

      <!-- Main Content Area -->
      <main class="main-content" :class="{ 'teams-main-content': isModule('teams') }">
        <slot>
          <router-view></router-view>
        </slot>
      </main>
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { getStoredUser } from '@/utils/permissions'
import AppTopBar from '@/components/layout/AppTopBar.vue'
import { useI18nStore } from '@/store/useI18nStore'

const router = useRouter()
const route = useRoute()
const i18nStore = useI18nStore()
const t = i18nStore.t

const showCreateModal = ref(false)

const currentUser = getStoredUser()
const userName = currentUser?.fullName || currentUser?.username || currentUser?.email || 'bạn'
const userInitials = userName.substring(0, 2).toUpperCase()

const isModule = (moduleName) => {
  if (moduleName === 'people') {
    return route.path.includes('/home/people') || route.path.includes('/home/profile')
  }
  if (moduleName === 'teams') {
    return route.path.includes('/home/teams') || route.path.includes('/home/people') || route.path.includes('/home/profile')
  }
  return route.path.includes(`/home/${moduleName}`)
}

const goToSiteSelection = () => {
  router.push('/site-selection')
}
</script>

<style scoped>
.home-site-container {
  display: flex;
  flex-direction: column;
  height: 100vh;
  background-color: #ffffff;
  color: #172b4d;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  overflow: hidden;
}

/* Topbar */
.home-topbar {
  height: 56px;
  background-color: #ffffff;
  border-bottom: 1px solid #dfe1e6;
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 0 16px;
  flex-shrink: 0;
  z-index: 10;
}

.topbar-left, .topbar-right {
  display: flex;
  align-items: center;
  gap: 16px;
}

.app-launcher-icon {
  width: 32px;
  height: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  color: #42526e;
  border-radius: 3px;
}

.app-launcher-icon:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.grid-icon {
  font-size: 18px;
  line-height: 1;
  letter-spacing: -2px;
}

.sprinta-logo {
  display: flex;
  align-items: center;
  gap: 6px;
  margin-right: 16px;
  cursor: pointer;
}

.logo-icon {
  width: 24px;
  height: 24px;
  background: linear-gradient(135deg, #0f172a 0%, #2563eb 100%);
  border-radius: 4px;
}

.logo-text {
  font-size: 20px;
  font-weight: bold;
  color: #172b4d;
  letter-spacing: -0.5px;
}

.topbar-nav {
  display: flex;
  align-items: center;
  gap: 4px;
}

.topbar-link {
  padding: 6px 12px;
  color: #42526e;
  text-decoration: none;
  font-weight: 500;
  font-size: 14px;
  border-radius: 3px;
  transition: background-color 0.2s, color 0.2s;
}

.topbar-link:hover {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172b4d;
}

.topbar-link.active {
  color: #0052cc;
  background-color: rgba(0, 82, 204, 0.08);
}

.create-btn {
  background-color: #0052cc;
  color: white;
  border: none;
  padding: 6px 12px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  margin-left: 8px;
  cursor: pointer;
}

.create-btn:hover {
  background-color: #0047b3;
}

.search-bar {
  position: relative;
  width: 200px;
}

.search-icon {
  position: absolute;
  left: 8px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 12px;
  color: #6b778c;
}

.search-bar input {
  width: 100%;
  padding: 6px 8px 6px 28px;
  border: 2px solid #dfe1e6;
  border-radius: 3px;
  font-size: 14px;
  background-color: #fafbfc;
  transition: all 0.2s;
  box-sizing: border-box;
  outline: none;
}

.search-bar input:focus {
  background-color: #ffffff;
  border-color: #4c9aff;
}

.icon-btn {
  background: none;
  border: none;
  font-size: 16px;
  cursor: pointer;
  width: 32px;
  height: 32px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
}

.icon-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.user-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background-color: #0052cc;
  color: white;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 14px;
  font-weight: bold;
  cursor: pointer;
}

/* Layout Wrapper */
.home-content-wrapper {
  display: flex;
  flex: 1;
  overflow: hidden;
}

/* Sidebar */
.sidebar {
  width: 240px;
  background-color: #f4f5f7;
  border-right: 1px solid #dfe1e6;
  display: flex;
  flex-direction: column;
  flex-shrink: 0;
  overflow-y: auto;
}

.sidebar-header {
  padding: 24px 16px 16px;
}

.site-switcher {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px;
  border-radius: 3px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.site-switcher:hover {
  background-color: rgba(9, 30, 66, 0.08);
}

.site-icon {
  width: 32px;
  height: 32px;
  background-color: #0052cc;
  color: white;
  border-radius: 4px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 16px;
  font-weight: bold;
  flex-shrink: 0;
}

.site-info {
  display: flex;
  flex-direction: column;
  flex: 1;
  overflow: hidden;
}

.site-name {
  font-weight: 600;
  font-size: 14px;
  color: #172b4d;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.site-subtitle {
  font-size: 12px;
  color: #5e6c84;
}

.dropdown-icon {
  color: #6b778c;
  font-size: 12px;
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  padding: 0 16px;
}

.nav-section-title {
  font-size: 11px;
  font-weight: 700;
  color: #5e6c84;
  text-transform: uppercase;
  margin: 16px 0 8px 8px;
  letter-spacing: 0.5px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 8px 12px;
  text-decoration: none;
  color: #42526e;
  font-size: 14px;
  font-weight: 500;
  border-radius: 3px;
  margin-bottom: 2px;
  transition: all 0.2s;
}

.nav-item:hover {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172b4d;
}

.nav-item.router-link-active, .nav-item.active-nav {
  background-color: rgba(0, 82, 204, 0.08);
  color: #0052cc;
}

.nav-item.router-link-active .nav-icon, .nav-item.active-nav .nav-icon {
  color: #0052cc;
}

.nav-item.active-nav[to="/home/starred"] .nav-icon i {
  font-weight: 900; /* Solid star */
}

.nav-icon {
  font-size: 16px;
  width: 20px;
  text-align: center;
  color: #6b778c;
}

.nav-icon.jira-blue {
  color: #0052CC;
}

.nav-divider {
  height: 1px;
  background-color: #dfe1e6;
  margin: 16px 8px;
}

/* Main Content */
.main-content {
  flex: 1;
  overflow-y: auto;
  background-color: #FAFBFC;
  position: relative;
}

.main-content.teams-main-content {
  background-color: #ffffff;
}
</style>
