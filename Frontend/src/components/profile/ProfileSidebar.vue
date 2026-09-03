<template>
  <div class="profile-sidebar">
    <div class="sidebar-user-info">
      <div class="user-avatar-wrapper">
        <UserAvatar :user="profileData" :size="48" :fontSize="18" />
      </div>
      <div class="user-detail">
        <h3 class="user-fullname">{{ profileData.fullName || t('Member', 'Thành viên') }}</h3>
        <p class="user-email">{{ profileData.email }}</p>
      </div>
    </div>

    <nav class="sidebar-nav">
      <button
        v-for="item in menuItems"
        :key="item.value"
        class="nav-item"
        :class="{ active: activeTab === item.value }"
        @click="$emit('select-tab', item.value)"
      >
        <span class="nav-icon"><i :class="item.icon"></i></span>
        <span class="nav-text">{{ item.label }}</span>
      </button>
    </nav>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useLocale } from '@/composables/useLocale'
import UserAvatar from '@/components/common/UserAvatar.vue'

const { t } = useLocale()

const props = defineProps({
  profileData: {
    type: Object,
    required: true
  },
  activeTab: {
    type: String,
    required: true
  }
})

defineEmits(['select-tab'])

const menuItems = computed(() => [
  {
    value: 'profile',
    label: t('Profile & Visibility', 'Hồ sơ & Chế độ hiển thị'),
    icon: 'fa-regular fa-user'
  },
  {
    value: 'emails',
    label: t('Email Addresses', 'Địa chỉ Email'),
    icon: 'fa-regular fa-envelope'
  },
  {
    value: 'security',
    label: t('Security', 'Bảo mật'),
    icon: 'fa-solid fa-shield-halved'
  },
  {
    value: 'connected',
    label: t('Connected Accounts', 'Tài khoản liên kết'),
    icon: 'fa-solid fa-link'
  },
  {
    value: 'password',
    label: t('Password', 'Mật khẩu'),
    icon: 'fa-solid fa-lock'
  },
  {
    value: 'mfa',
    label: t('Two-factor Authentication', 'Xác thực 2 yếu tố'),
    icon: 'fa-solid fa-key'
  },
  {
    value: 'sessions',
    label: t('Sessions / Devices', 'Phiên đăng nhập / Thiết bị'),
    icon: 'fa-solid fa-desktop'
  }
])

</script>

<style scoped>
.profile-sidebar {
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 24px;
  display: flex;
  flex-direction: column;
  gap: 24px;
  height: fit-content;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.sidebar-user-info {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  gap: 16px;
  padding-bottom: 20px;
  border-bottom: 1px solid var(--color-border);
}

.user-avatar-wrapper {
  position: relative;
}

.sidebar-avatar {
  height: 96px;
  width: 96px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 32px;
  font-weight: 700;
  border: 4px solid var(--color-bg);
  box-shadow: 0 4px 10px rgba(0, 0, 0, 0.08);
}

.user-detail {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.user-fullname {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.user-email {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin: 0;
  word-break: break-all;
}

.sidebar-nav {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.nav-item {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 10px 14px;
  border-radius: 8px;
  background: transparent;
  border: none;
  width: 100%;
  text-align: left;
  color: var(--color-text-secondary);
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
}

.nav-item:hover {
  background-color: var(--color-surface-hover);
  color: var(--color-text-primary);
}

.nav-item.active {
  background-color: rgba(9, 30, 66, 0.08);
  color: #0052cc;
  font-weight: 600;
}

.dark .nav-item.active {
  background-color: rgba(56, 189, 248, 0.15);
  color: var(--color-accent);
}

.nav-icon {
  font-size: 16px;
  width: 20px;
  display: flex;
  justify-content: center;
  align-items: center;
}

.nav-text {
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
</style>
