<template>
  <el-dropdown trigger="click" popper-class="user-dropdown-popper" @command="handleCommand" :teleported="true">
    <div class="user-avatar-trigger" :class="{ 'has-text': true }">
      <UserAvatar :user="currentUser" :size="28" :fontSize="11" :clickable="false" />
      <span class="user-trigger-text">{{ userEmailPrefix }}</span>
      <i class="fa-solid fa-chevron-down trigger-arrow"></i>
    </div>
    <template #dropdown>
      <el-dropdown-menu class="jira-user-menu">
        <div class="user-menu-header">
          <UserAvatar :user="currentUser" :size="40" :fontSize="14" :clickable="false" />
          <div class="header-info">
            <div class="user-display-name">{{ userDisplayName }}</div>
            <div class="user-email">{{ userEmail }}</div>
          </div>
        </div>

        <div class="menu-divider"></div>

        <el-dropdown-item command="profile">
          <div class="menu-item-inner">
            <i class="fa-regular fa-user"></i>
            <span>{{ t('My profile') }}</span>
          </div>
        </el-dropdown-item>

        <el-dropdown-item command="settings">
          <div class="menu-item-inner">
            <i class="fa-solid fa-gear"></i>
            <span>{{ t('Account settings') }}</span>
          </div>
        </el-dropdown-item>

        <div class="theme-trigger-item" @click.stop="toggleLangSub">
          <div class="menu-item-inner">
            <i class="fa-solid fa-globe"></i>
            <span>{{ t('Language') }}</span>
            <span class="ms-auto text-xs text-gray-500">{{ i18nStore.locale.toUpperCase() }}</span>
            <i class="fa-solid fa-chevron-right arrow-icon" :class="{ rotated: langSubVisible }"></i>
          </div>

          <transition name="el-zoom-in-top">
            <div v-if="langSubVisible" class="theme-expanded-menu">
              <div class="theme-option" :class="{ active: i18nStore.locale === 'en' }" @click.stop="selectLang('en')">
                <div class="radio-indicator">
                  <i v-if="i18nStore.locale === 'en'" class="fa-solid fa-circle-dot"></i>
                  <i v-else class="fa-regular fa-circle"></i>
                </div>
                <span class="option-label">English (US) 🇺🇸</span>
              </div>
              <div class="theme-option" :class="{ active: i18nStore.locale === 'vi' }" @click.stop="selectLang('vi')">
                <div class="radio-indicator">
                  <i v-if="i18nStore.locale === 'vi'" class="fa-solid fa-circle-dot"></i>
                  <i v-else class="fa-regular fa-circle"></i>
                </div>
                <span class="option-label">Tiếng Việt 🇻🇳</span>
              </div>
            </div>
          </transition>
        </div>

        <div class="menu-divider"></div>

        <el-dropdown-item command="logout" class="logout-item-wrapper">
          <div class="menu-item-inner logout-item">
            <i class="fa-solid fa-arrow-right-from-bracket"></i>
            <span>{{ t('Sign out') }}</span>
          </div>
        </el-dropdown-item>
      </el-dropdown-menu>
    </template>
  </el-dropdown>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { getStoredUser } from '@/utils/permissions'
import { clearAuthSession } from '@/utils/authSession'
import axiosClient from '@/api/axiosClient'
import { useI18nStore } from '@/store/useI18nStore'
import { useSprintStore } from '@/store/useSprintStore'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { signalRService } from '@/api/signalrService'

const router = useRouter()
const langSubVisible = ref(false)
const profileData = ref(null)

const i18nStore = useI18nStore()
const sprintStore = useSprintStore()
const t = (key) => i18nStore.t(key)

const currentUser = computed(() => profileData.value || getStoredUser())
const userDisplayName = computed(() => currentUser.value?.fullName || currentUser.value?.name || currentUser.value?.publicName || currentUser.value?.email?.split('@')?.[0] || 'User')
const userEmail = computed(() => currentUser.value?.email || 'user@example.com')
const userEmailPrefix = computed(() => userEmail.value.split('@')[0])

const fetchProfile = async () => {
  try {
    const response = await axiosClient.get('/users/me')
    profileData.value = response.data?.data
    const stored = getStoredUser()
    if (stored && profileData.value) {
      stored.avatarColor = profileData.value.avatarColor
      localStorage.setItem('sprinta_user', JSON.stringify(stored))
    }
  } catch (error) {
    console.error('Failed to fetch user profile in dropdown', error)
  }
}

const handleAvatarUpdate = (event) => {
  if (profileData.value) {
    profileData.value.avatarUrl = event.detail.avatarUrl
  }
}

const handleUserRealtime = (event) => {
  if (`${event?.entityType || ''}`.toLowerCase() !== 'user') return
  const user = event?.data
  const currentUserId = profileData.value?.id || getStoredUser()?.id
  if (!user?.id || `${user.id}` !== `${currentUserId || ''}`) return
  profileData.value = { ...(profileData.value || {}), ...user }
}

onMounted(() => {
  fetchProfile()
  window.addEventListener('user-avatar-updated', handleAvatarUpdate)
  signalRService.on('EntityChanged', handleUserRealtime)
})

onUnmounted(() => {
  window.removeEventListener('user-avatar-updated', handleAvatarUpdate)
  signalRService.off('EntityChanged', handleUserRealtime)
})

const toggleLangSub = () => {
  langSubVisible.value = !langSubVisible.value
}

const handleCommand = async (cmd) => {
  if (cmd === 'profile' || cmd === 'settings') {
    router.push('/profile')
  } else if (cmd === 'logout') {
    try {
      await axiosClient.post('/auth/logout')
    } catch (error) {
      console.error('Logout error:', error)
    } finally {
      sprintStore.resetScope()
      clearAuthSession()
      router.push('/login')
    }
  }

  langSubVisible.value = false
}

const selectLang = (lang) => {
  i18nStore.setLocale(lang)
  langSubVisible.value = false
}
</script>

<style scoped>
.jira-user-menu {
  width: 300px;
  background-color: var(--color-surface);
  border-radius: 2px;
  padding: 8px 0;
  border: 1px solid var(--color-border);
}
.user-menu-header { display: flex; padding: 12px 16px; gap: 12px; align-items: center; border-bottom: 1px solid var(--color-border); margin-bottom: 4px; }
.header-avatar {
  width: 40px; height: 40px;
  border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-size: 16px; font-weight: 700; color: #fff;
}
.user-display-name { font-size: 14px; font-weight: 700; color: var(--color-text-primary); }
.user-email { font-size: 12px; color: var(--color-text-muted); }

.menu-item-inner {
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--color-text-primary) !important;
  font-size: 13px;
  font-weight: 500;
}
.menu-item-inner i { width: 16px; text-align: center; color: var(--color-text-muted); font-size: 14px; }

.logout-item i, .logout-item span { color: #ef4444 !important; }
.menu-divider { height: 1px; background-color: var(--color-border); margin: 4px 0; }

.theme-trigger-item { padding: 10px 16px; cursor: pointer; transition: all 0.2s; }
.theme-trigger-item:hover { background-color: var(--color-surface-hover); }

.theme-expanded-menu {
  margin: 8px 12px;
  background-color: var(--color-bg);
  border-radius: 4px;
  padding: 4px 0;
  border: 1px solid var(--color-border);
}
.theme-option {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  gap: 12px;
  cursor: pointer;
  color: var(--color-text-secondary);
}
.theme-option:hover {
  background-color: var(--color-surface-hover);
  color: var(--color-text-primary);
}
.theme-option.active {
  background-color: color-mix(in srgb, var(--color-accent) 15%, transparent);
  color: var(--color-accent);
}
.option-label { font-size: 12px; font-weight: 600; }
.radio-indicator { color: var(--color-accent); font-size: 12px; }
.arrow-icon { margin-left: auto; transition: transform 0.2s; color: var(--color-text-muted); font-size: 10px !important; }
.arrow-icon.rotated { transform: rotate(90deg); }

.theme-preview-box {
  width: 36px; height: 24px; border-radius: 2px; border: 1px solid var(--color-border);
  overflow: hidden; display: flex; flex-direction: column;
}
.light.theme-preview-box { background: #ffffff; }
.dark.theme-preview-box { background: #0f172a; border-color: #334155; }
.p-header { height: 4px; background: #f1f5f9; }
.dark .p-header { background: #1e293b; }
.p-body { flex: 1; display: flex; }
.p-sidebar { width: 10px; background: #f8fafc; border-right: 1px solid #e2e8f0; }
.dark .p-sidebar { background: #020617; border-color: #1e293b; }
.p-content { flex: 1; background: #ffffff; }
.dark .p-content { background: #0f172a; }

.user-avatar-trigger {
  display: flex; align-items: center; gap: 6px; cursor: pointer;
  padding: 4px 6px; border-radius: 24px; transition: background-color 0.2s;
}
.user-avatar-trigger:hover {
  background-color: rgba(255, 255, 255, 0.1);
}
.avatar-circle {
  width: 28px; height: 28px;
  color: #fff; border-radius: 50%;
  display: flex; align-items: center; justify-content: center;
  font-weight: 700; font-size: 11px;
  border: 1px solid rgba(255,255,255,0.1);
}
.user-trigger-text {
  font-size: 14px;
  font-weight: 500;
  color: #DEEBFF;
}
.user-avatar-trigger:hover .user-trigger-text,
.user-avatar-trigger:hover .trigger-arrow {
  color: #FFFFFF;
}
.trigger-arrow {
  font-size: 10px;
  color: #DEEBFF;
  margin-left: 2px;
}
</style>

<style>
.user-dropdown-popper.el-popper {
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
  border-radius: 12px !important;
  padding: 0 !important;
  box-shadow: none !important;
}
.user-dropdown-popper .el-dropdown-menu__item {
  padding: 10px 16px !important;
  background-color: transparent !important;
  color: var(--color-text-primary) !important;
}
.user-dropdown-popper .el-dropdown-menu__item:hover {
  background-color: var(--color-surface-hover) !important;
}
.user-dropdown-popper .el-popper__arrow::before {
  background: var(--color-surface) !important;
  border: 1px solid var(--color-border) !important;
}
</style>
