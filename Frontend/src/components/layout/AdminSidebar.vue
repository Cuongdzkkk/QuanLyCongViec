<template>
  <aside class="admin-sidebar shadow-sm">
    <div class="sidebar-header">
      <router-link to="/dashboard" class="back-link">
        <i class="fa-solid fa-arrow-left"></i>
        <span>{{ t('Jira admin settings', 'Jira admin settings') }}</span>
      </router-link>
    </div>

    <!-- SWITCH SETTINGS Section -->
    <div class="sidebar-section">
      <div class="section-title">{{ t('Switch settings', 'Switch settings') }}</div>
      <el-dropdown trigger="click" @command="handleCategorySwitch" class="switch-settings-dropdown" style="width: 100%;" popper-class="jira-switch-dropdown-popper">
        <div class="switch-trigger-btn">
          <div style="display: flex; align-items: center; gap: 8px;">
            <i :class="currentCategoryIcon"></i>
            <span>{{ t(activeCategory, activeCategory) }}</span>
          </div>
          <i class="fa-solid fa-chevron-down" style="font-size: 10px;"></i>
        </div>
        <template #dropdown>
          <el-dropdown-menu class="jira-switch-dropdown-menu">
            <el-dropdown-item command="/admin/system/general-configuration" :disabled="activeCategory === 'System'">
              <i class="fa-solid fa-desktop" style="margin-right: 8px;"></i> {{ t('System', 'System') }}
            </el-dropdown-item>
            <el-dropdown-item command="/admin/users" :disabled="activeCategory === 'Jira apps'">
              <i class="fa-solid fa-users" style="margin-right: 8px;"></i> {{ t('Jira apps', 'Jira apps') }}
            </el-dropdown-item>
            <el-dropdown-item command="/spaces" :disabled="activeCategory === 'Spaces'">
              <i class="fa-solid fa-rocket" style="margin-right: 8px;"></i> {{ t('Spaces', 'Spaces') }}
            </el-dropdown-item>
            <el-dropdown-item command="/admin/configuration" :disabled="activeCategory === 'Work items'">
              <i class="fa-regular fa-folder-open" style="margin-right: 8px;"></i> {{ t('Work items', 'Work items') }}
            </el-dropdown-item>
          </el-dropdown-menu>
        </template>
      </el-dropdown>
    </div>

    <!-- Sub-navigation menus -->
    <ul class="sidebar-menu">
      <!-- System Category Links -->
      <template v-if="activeCategory === 'System'">
        <li class="menu-heading">{{ t('SYSTEM', 'SYSTEM') }}</li>
        <li>
          <router-link to="/admin/system/general-configuration" class="menu-item" :class="{ active: $route.path === '/admin/system/general-configuration' }">
            {{ t('General configuration', 'General configuration') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/system/info" class="menu-item" :class="{ active: $route.path === '/admin/system/info' }">
            {{ t('System info', 'System info') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/audit-log" class="menu-item" :class="{ active: $route.path === '/admin/audit-log' }">
            {{ t('Audit Log', 'Audit Log') }}
          </router-link>
        </li>

        <li class="menu-heading">{{ t('BILLING', 'BILLING') }}</li>
        <li>
          <router-link to="/admin/billing" class="menu-item" :class="{ active: $route.path === '/admin/billing' }">
            {{ t('Billing & AI Credits', 'Billing & AI Credits') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/billing/payments" class="menu-item" :class="{ active: $route.path === '/admin/billing/payments' }">
            {{ t('Payments', 'Payments') }}
          </router-link>
        </li>

        <li class="menu-heading">{{ t('SECURITY', 'SECURITY') }}</li>
        <li>
          <router-link to="/admin/security/2fa" class="menu-item" :class="{ active: $route.path === '/admin/security/2fa' }">
            {{ t('Two-Factor Auth', 'Two-Factor Auth') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/security/password" class="menu-item" :class="{ active: $route.path === '/admin/security/password' }">
            {{ t('Change Password', 'Change Password') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/security/ip-whitelist" class="menu-item" :class="{ active: $route.path === '/admin/security/ip-whitelist' }">
            {{ t('IP Whitelist', 'IP Whitelist') }}
          </router-link>
        </li>

        <li class="menu-heading">{{ t('INTEGRATIONS', 'INTEGRATIONS') }}</li>
        <li>
          <router-link to="/admin/instance/authentication" class="menu-item" :class="{ active: $route.path === '/admin/instance/authentication' }">
            {{ t('Authentication', 'Authentication') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/instance/email" class="menu-item" :class="{ active: $route.path === '/admin/instance/email' }">
            {{ t('Email / SMTP', 'Email / SMTP') }}
          </router-link>
        </li>
      </template>

      <!-- Jira Apps Category Links -->
      <template v-else-if="activeCategory === 'Jira apps'">
        <li class="menu-heading">{{ t('USER DIRECTORY', 'USER DIRECTORY') }}</li>
        <li>
          <router-link to="/admin/users" class="menu-item" :class="{ active: $route.path === '/admin/users' }">
            {{ t('User Management', 'User Management') }}
          </router-link>
        </li>
        <li>
          <router-link to="/admin/roles" class="menu-item" :class="{ active: $route.path === '/admin/roles' }">
            {{ t('Role Management', 'Role Management') }}
          </router-link>
        </li>
      </template>

      <!-- Spaces Category Links -->
      <template v-else-if="activeCategory === 'Spaces'">
        <li class="menu-heading">{{ t('SPACES MANAGEMENT', 'SPACES MANAGEMENT') }}</li>
        <li>
          <router-link to="/spaces" class="menu-item" :class="{ active: $route.path === '/spaces' }">
            {{ t('Manage spaces', 'Manage spaces') }}
          </router-link>
        </li>
        <li>
          <router-link to="/spaces/categories" class="menu-item" :class="{ active: $route.path === '/spaces/categories' }">
            {{ t('Space categories', 'Space categories') }}
          </router-link>
        </li>
        <li>
          <router-link to="/spaces/trash" class="menu-item" :class="{ active: $route.path === '/spaces/trash' }">
            {{ t('Trash', 'Trash') }}
          </router-link>
        </li>
        <li>
          <router-link to="/spaces/archive" class="menu-item" :class="{ active: $route.path === '/spaces/archive' }">
            {{ t('Archive', 'Archive') }}
          </router-link>
        </li>
      </template>

      <!-- Work Items Category Links -->
      <template v-else-if="activeCategory === 'Work items'">
        <li class="menu-heading">{{ t('SCHEMAS', 'SCHEMAS') }}</li>
        <li>
          <router-link to="/admin/configuration" class="menu-item" :class="{ active: $route.path === '/admin/configuration' }">
            {{ t('Global Configuration', 'Global Configuration') }}
          </router-link>
        </li>
      </template>
    </ul>

    <div style="flex-grow: 1;"></div>

    <div class="sidebar-footer">
      <div class="lang-selector" @click.stop="langMenuOpen = !langMenuOpen">
        <div class="lang-current">
          <span class="lang-flag">{{ locale === 'vi' ? 'VN' : 'EN' }}</span>
          <span class="lang-name">{{ locale === 'vi' ? 'Vietnamese' : 'English' }}</span>
          <i class="fa-solid fa-chevron-down lang-arrow" :class="{ 'is-open': langMenuOpen }"></i>
        </div>

        <Transition name="lang-dropdown">
          <div v-if="langMenuOpen" class="lang-dropdown">
            <button
              type="button"
              class="lang-option"
              :class="{ active: locale === 'vi' }"
              @click.stop="setLocale('vi')"
            >
              <span class="lang-flag">VN</span>
              <span>Vietnamese</span>
              <i v-if="locale === 'vi'" class="fa-solid fa-check lang-check"></i>
            </button>
            <button
              type="button"
              class="lang-option"
              :class="{ active: locale === 'en' }"
              @click.stop="setLocale('en')"
            >
              <span class="lang-flag">EN</span>
              <span>English</span>
              <i v-if="locale === 'en'" class="fa-solid fa-check lang-check"></i>
            </button>
          </div>
        </Transition>
      </div>
    </div>
  </aside>
</template>

<script setup>
import { computed, ref, onMounted, onUnmounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useLocale } from '@/composables/useLocale'
import { canAccessAdminUserDirectory, getStoredUser, hasSystemAdminAccess } from '@/utils/permissions'

const { locale, toggleLocale, t } = useLocale()

const langMenuOpen = ref(false)
const route = useRoute()
const router = useRouter()

const setLocale = (lang) => {
  if (locale.value !== lang) {
    toggleLocale()
  }
  langMenuOpen.value = false
}

const closeLangMenu = () => {
  langMenuOpen.value = false
}

onMounted(() => {
  document.addEventListener('click', closeLangMenu)
})

onUnmounted(() => {
  document.removeEventListener('click', closeLangMenu)
})

const activeCategory = computed(() => {
  const path = route.path
  if (path.startsWith('/admin/system') || path.startsWith('/admin/instance') || path.startsWith('/admin/security') || path.startsWith('/admin/audit-log') || path.startsWith('/admin/billing')) {
    return 'System'
  }
  if (path.startsWith('/admin/users') || path.startsWith('/admin/roles')) {
    return 'Jira apps'
  }
  if (path.startsWith('/spaces')) {
    return 'Spaces'
  }
  if (path.startsWith('/admin/configuration')) {
    return 'Work items'
  }
  return 'System'
})

const currentCategoryIcon = computed(() => {
  switch (activeCategory.value) {
    case 'System': return 'fa-solid fa-desktop'
    case 'Jira apps': return 'fa-solid fa-users'
    case 'Spaces': return 'fa-solid fa-rocket'
    case 'Work items': return 'fa-regular fa-folder-open'
    default: return 'fa-solid fa-desktop'
  }
})

const handleCategorySwitch = (path) => {
  router.push(path)
}
</script>

<style scoped>
.admin-sidebar {
  width: 240px;
  background-color: var(--color-bg);
  display: flex;
  flex-direction: column;
  border-right: 1px solid var(--color-border);
  z-index: 10;
  padding: 24px 16px;
  font-family: 'Inter', sans-serif;
  height: 100vh;
  box-sizing: border-box;
  flex-shrink: 0;
}

.sidebar-header {
  margin-bottom: 24px;
}

.back-link {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-muted);
  text-decoration: none;
  font-size: 13px;
  font-weight: 500;
  transition: color 0.2s;
}

.back-link:hover {
  color: var(--color-text-primary);
}

.sidebar-section {
  margin-bottom: 20px;
}

.section-title {
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  color: var(--color-text-muted);
  margin-bottom: 8px;
  letter-spacing: 0.5px;
}

.switch-trigger-btn {
  display: flex;
  align-items: center;
  justify-content: space-between;
  width: 100%;
  cursor: pointer;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: var(--color-text-primary);
  padding: 8px 12px;
  border-radius: 4px;
  font-size: 13px;
  font-weight: 600;
  transition: all 0.2s;
  box-sizing: border-box;
}

.switch-trigger-btn:hover {
  background: var(--color-surface-hover);
}

.switch-trigger-btn i {
  color: var(--color-text-secondary);
}

.sidebar-menu {
  list-style: none;
  padding: 0 0 0 12px;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: 4px;
  border-left: 1px solid var(--color-border);
}

.menu-heading {
  font-size: 10px;
  font-weight: 700;
  color: var(--color-text-muted);
  margin: 12px 0 4px 12px;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.menu-heading:first-child {
  margin-top: 0;
}

.menu-item {
  display: block;
  padding: 6px 12px;
  border-radius: 4px;
  font-size: 13px;
  color: var(--color-text-secondary);
  text-decoration: none;
  transition: all 0.2s;
}

.menu-item:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.menu-item.active {
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
  color: var(--color-accent);
  font-weight: 600;
}

.sidebar-footer {
  padding-top: 16px;
  border-top: 1px solid var(--color-border);
}

.lang-selector {
  position: relative;
  cursor: pointer;
}

.lang-current {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 8px 12px;
  border: 1px solid var(--color-border);
  border-radius: 6px;
  background: var(--color-surface);
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
  transition: all 0.2s ease;
}

.lang-current:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.lang-flag {
  font-size: 11px;
  font-weight: 700;
  min-width: 18px;
  text-align: center;
}

.lang-name {
  flex: 1;
}

.lang-arrow {
  font-size: 9px;
  color: var(--color-text-muted);
  transition: transform 0.2s ease;
}

.lang-arrow.is-open {
  transform: rotate(180deg);
}

.lang-dropdown {
  position: absolute;
  bottom: calc(100% + 8px);
  left: 0;
  right: 0;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  padding: 4px;
  box-shadow: var(--shadow-md);
  z-index: 999;
}

.lang-option {
  display: flex;
  align-items: center;
  gap: 10px;
  width: 100%;
  padding: 8px 10px;
  border: none;
  border-radius: 4px;
  background: transparent;
  color: var(--color-text-secondary);
  font-size: 12.5px;
  cursor: pointer;
  transition: all 0.15s ease;
  text-align: left;
}

.lang-option:hover {
  background: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.lang-option.active {
  background: color-mix(in srgb, var(--color-accent) 10%, transparent);
  color: var(--color-accent);
  font-weight: 600;
}

.lang-check {
  margin-left: auto;
  font-size: 10px;
  color: var(--color-accent);
}
</style>

<style>
.el-popper.jira-switch-dropdown-popper {
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  padding: 4px 0 !important;
  z-index: 100002 !important;
  box-shadow: 0 10px 30px rgba(0,0,0,0.2) !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item {
  color: var(--color-text-primary) !important;
  display: flex !important;
  align-items: center !important;
  gap: 8px !important;
  font-size: 13px !important;
  padding: 8px 16px !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item:hover {
  background-color: var(--color-surface-hover) !important;
  color: var(--color-text-primary) !important;
}

.jira-switch-dropdown-menu .el-dropdown-menu__item.is-disabled {
  color: var(--color-accent) !important;
  font-weight: 600 !important;
  background: transparent !important;
  cursor: default !important;
}
</style>
