<template>
  <div class="user-avatar-wrapper" :style="wrapperStyle" @click="handleClick">
    <img v-if="resolvedAvatarUrl" :src="resolvedAvatarUrl" :alt="resolvedName" class="user-avatar-img" @error="handleImageError" />
    <div v-else class="user-avatar-initials" :style="initialsStyle">
      {{ resolvedInitials }}
    </div>
  </div>
</template>

<script setup>
import { computed, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getAvatarColor, getInitials } from '@/utils/avatarHelper'
import { usePeopleStore } from '@/store/usePeopleStore'

const route = useRoute()
const router = useRouter()
const peopleStore = usePeopleStore()

const props = defineProps({
  user: {
    type: Object,
    default: () => ({})
  },
  size: {
    type: Number,
    default: 32
  },
  fontSize: {
    type: Number,
    default: 12
  },
  clickable: {
    type: Boolean,
    default: true
  }
})

// Extract properties robustly, handling backend DTO variations
const imageLoadError = ref(false)
const handleImageError = () => {
  imageLoadError.value = true
}

const resolvedId = computed(() => props.user?.id || props.user?.Id || props.user?.userId || props.user?.UserId || '')
const rawEmail = computed(() => props.user?.email || props.user?.Email || '')
const rawName = computed(() => props.user?.fullName || props.user?.FullName || 
                                props.user?.owner || props.user?.Owner ||
                                props.user?.leadName || props.user?.LeadName || 
                                props.user?.name || props.user?.Name || '')

const storeUserObj = computed(() => {
  if (resolvedId.value) {
    const found = peopleStore.users?.find(u => String(u.id) === String(resolvedId.value))
    if (found) return found
  }
  if (rawEmail.value) {
    const found = peopleStore.users?.find(u => String(u.email || '').toLowerCase() === String(rawEmail.value).toLowerCase())
    if (found) return found
  }
  if (rawName.value) {
    const found = peopleStore.users?.find(u => String(u.fullName || '').toLowerCase() === String(rawName.value).toLowerCase())
    if (found) return found
  }
  return null
})

const resolvedAvatarUrl = computed(() => {
  if (imageLoadError.value) return null
  return props.user?.avatarUrl || props.user?.AvatarUrl ||
         props.user?.ownerAvatarUrl || props.user?.OwnerAvatarUrl ||
         props.user?.leadAvatarUrl || props.user?.LeadAvatarUrl ||
         storeUserObj.value?.avatarUrl || ''
})

const resolvedName = computed(() => {
  return rawName.value || storeUserObj.value?.fullName || ''
})

const resolvedEmail = computed(() => {
  return rawEmail.value || storeUserObj.value?.email || ''
})

const resolvedInitials = computed(() => {
  // If backend provided initials explicitly
  const backendInitials = props.user?.initials || props.user?.Initials || 
                          props.user?.ownerInitials || props.user?.OwnerInitials ||
                          props.user?.leadInitials || props.user?.LeadInitials ||
                          storeUserObj.value?.initials

  if (backendInitials) return backendInitials

  // If there's no name and no email yet, show empty (still loading)
  if (!resolvedName.value && !resolvedEmail.value) return ''

  // Fallback if backend didn't provide
  return getInitials(resolvedName.value, resolvedEmail.value)
})

const resolvedColor = computed(() => {
  const backendColor = props.user?.avatarColor || props.user?.AvatarColor ||
                       props.user?.ownerColor || props.user?.OwnerColor ||
                       props.user?.leadColor || props.user?.LeadColor ||
                       storeUserObj.value?.avatarColor

  if (backendColor) return backendColor

  // If no identifiers are loaded yet, return a neutral skeleton color
  if (!resolvedEmail.value && !resolvedId.value && !resolvedName.value) {
    return 'var(--color-border, #e2e8f0)'
  }

  // Fallback to email, then id, then name hash to guarantee cross-component consistency
  return getAvatarColor(String(resolvedEmail.value || resolvedId.value || resolvedName.value))
})

const isClickable = computed(() => {
  if (props.clickable === false) return false
  if (!resolvedId.value) return false
  if (route.path.includes('/rewards') || route.path.includes('/reward')) return false
  return true
})

const handleClick = (e) => {
  if (!isClickable.value) return
  
  // Prevent redirection if the avatar is inside an interactive trigger, button, dropdown, or popover selection context
  const isInteractiveContext = e.target.closest('button') ||
                               e.target.closest('[role="button"]') ||
                               e.target.closest('.property-trigger') ||
                               e.target.closest('.inline-assignee-trigger') ||
                               e.target.closest('.popover-content') ||
                               e.target.closest('.el-popover') ||
                               e.target.closest('.popover-list') ||
                               e.target.closest('.el-dropdown-menu') ||
                               e.target.closest('.custom-popover') ||
                               e.target.closest('.dropdown-menu') ||
                               e.target.closest('.assignee-trigger') ||
                               e.target.closest('.select-member-trigger')
                               
  if (isInteractiveContext) {
    // Let event bubble up to trigger the parent's actual click handler (select user / toggle dropdown)
    return
  }

  e.stopPropagation()
  if (route.path.startsWith('/space/')) {
    const spaceSlug = route.params.spaceSlug || 'project'
    const projectId = route.params.id || route.params.projectId
    if (projectId) {
      router.push(`/space/${spaceSlug}/${projectId}/profile/${resolvedId.value}`)
    } else {
      router.push(`/profile/${resolvedId.value}`)
    }
  } else if (route.path.startsWith('/home/') || route.path.startsWith('/sites')) {
    router.push(`/home/profile/${resolvedId.value}`)
  } else {
    router.push(`/profile/${resolvedId.value}`)
  }
}

const wrapperStyle = computed(() => ({
  width: `${props.size}px`,
  height: `${props.size}px`,
  borderRadius: '50%',
  overflow: 'hidden',
  display: 'inline-flex',
  alignItems: 'center',
  justifyContent: 'center',
  backgroundColor: resolvedAvatarUrl.value ? 'transparent' : resolvedColor.value,
  flexShrink: 0,
  cursor: isClickable.value ? 'pointer' : 'default'
}))

const initialsStyle = computed(() => ({
  color: '#ffffff',
  fontSize: `${props.fontSize}px`,
  fontWeight: '600',
  lineHeight: '1',
  textTransform: 'uppercase',
  userSelect: 'none',
  letterSpacing: 'normal',
  margin: '0',
  padding: '0'
}))
</script>

<style scoped>
.user-avatar-wrapper {
  /* Default inherited styles */
}

.user-avatar-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  display: block;
}

.user-avatar-initials {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 100%;
  height: 100%;
}
</style>
