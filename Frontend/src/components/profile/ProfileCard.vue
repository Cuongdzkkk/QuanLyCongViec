<template>
  <div class="settings-card profile-card" v-loading="isLoading">
    <div class="card-header">
      <h2 class="card-title">{{ t('Profile & Visibility', 'Hồ sơ & Chế độ hiển thị') }}</h2>
      <p class="card-subtitle">{{ t('Manage your public profile information and control who can see it across the workspace.', 'Quản lý thông tin hồ sơ công khai của bạn và kiểm soát những ai có thể nhìn thấy thông tin đó trong hệ thống.') }}</p>
    </div>

    <!-- Hidden file inputs -->
    <input ref="avatarInput" type="file" style="display: none" accept="image/*" @change="onAvatarFileChange" />

    <!-- 1. Profile Photo Section -->
    <div class="form-section">
      <h3 class="section-title">{{ t('Profile photo', 'Ảnh đại diện') }}</h3>
      <div class="profile-photo-block">
        <div class="photo-preview-wrapper">
          <UserAvatar :user="profileData" :size="96" :fontSize="36" />
        </div>
        <div class="photo-actions">
          <div class="photo-buttons">
            <el-button type="primary" size="default" @click="triggerAvatarUpload">
              <i class="fa-solid fa-cloud-arrow-up mr-2"></i> {{ t('Upload photo', 'Tải ảnh lên') }}
            </el-button>
            <el-button v-if="profileData.avatarUrl" type="danger" plain size="default" @click="removeAvatar">
              <i class="fa-solid fa-trash-can mr-2"></i> {{ t('Remove', 'Gỡ bỏ') }}
            </el-button>
          </div>
          <p class="photo-hint">{{ t('Accepts JPEG, PNG, GIF or WebP. Max 5MB.', 'Chấp nhận định dạng JPEG, PNG, GIF hoặc WebP. Tối đa 5MB.') }}</p>
        </div>
      </div>
    </div>

    <hr class="section-divider" />

    <!-- 2. Personal Information & Visibility Settings -->
    <div class="form-section">
      <h3 class="section-title">{{ t('Personal Information', 'Thông tin cá nhân') }}</h3>
      <p class="section-desc">{{ t('Provide details about your role, department, and location. You can also specify visibility settings for each field.', 'Cung cấp chi tiết về vai trò, phòng ban và vị trí của bạn. Bạn cũng có thể thiết lập chế độ hiển thị cho từng trường.') }}</p>

      <div class="atlassian-form-grid">
        <!-- Full Name Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Full name', 'Họ và tên') }}</label>
            <el-input v-model="form.fullName" />
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.fullName">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.fullName)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Public Name Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Display name', 'Tên hiển thị công khai') }}</label>
            <el-input v-model="form.publicName" />
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.publicName">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.publicName)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Job Title Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Job title', 'Chức danh công việc') }}</label>
            <el-input v-model="form.jobTitle" :placeholder="t('Enter job title', 'Nhập chức danh')" />
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.jobTitle">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.jobTitle)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Department Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Department', 'Phòng ban') }}</label>
            <el-input v-model="form.department" :placeholder="t('Enter department', 'Nhập phòng ban')" />
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.department">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.department)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Organization Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Organization', 'Tổ chức') }}</label>
            <el-input v-model="form.organization" :placeholder="t('Enter organization', 'Nhập tổ chức')" />
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.organization">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.organization)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Location Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Location', 'Vị trí địa lý') }}</label>
            <el-input v-model="form.location" :placeholder="t('Example: Hanoi, Vietnam', 'Ví dụ: Hà Nội, Việt Nam')" />
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.location">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.location)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Time Zone Row -->
        <div class="form-row">
          <div class="row-info">
            <label class="row-label">{{ t('Time zone', 'Múi giờ') }}</label>
            <el-select v-model="form.timeZone" style="width: 100%;">
              <el-option value="UTC" label="Coordinated Universal Time (UTC)" />
              <el-option value="GMT+7" label="Bangkok, Hanoi, Jakarta (GMT+7)" />
              <el-option value="GMT+8" label="Singapore, Beijing, Taipei (GMT+8)" />
              <el-option value="GMT-5" label="New York, Toronto (EST)" />
            </el-select>
          </div>
          <div class="row-visibility">
            <label class="row-label">{{ t('Who can see this?', 'Ai có thể xem?') }}</label>
            <el-select v-model="visibility.timeZone">
              <template #prefix>
                <i :class="getVisibilityIcon(visibility.timeZone)"></i>
              </template>
              <el-option value="Anyone" :label="t('Anyone', 'Mọi người')" />
              <el-option value="Organization" :label="t('Organization', 'Tổ chức')" />
              <el-option value="OnlyMe" :label="t('Only me', 'Chỉ mình tôi')" />
            </el-select>
          </div>
        </div>

        <!-- Collaboration rules Row -->
        <div class="form-row full-width">
          <div class="row-info">
            <label class="row-label">{{ t('Working with you', 'Nguyên tắc làm việc chung') }}</label>
            <el-input
              v-model="form.collaboration"
              type="textarea"
              :rows="3"
              :placeholder="t('Help teammates understand how to work with you better', 'Giúp đồng nghiệp hiểu cách làm việc với bạn hiệu quả hơn')"
            />
          </div>
        </div>
      </div>

      <div class="form-save-actions">
        <el-button type="primary" :loading="isSaving" @click="onSubmit">
          {{ t('Save changes', 'Lưu thay đổi') }}
        </el-button>
      </div>
    </div>
  </div>
</template>

<script setup>
import { computed, ref, watch, onMounted } from 'vue'
import { useLocale } from '@/composables/useLocale'
import { ElMessage } from 'element-plus'
import UserAvatar from '@/components/common/UserAvatar.vue'

const { t } = useLocale()
const avatarInput = ref(null)

const props = defineProps({
  profileData: {
    type: Object,
    required: true
  },
  isLoading: {
    type: Boolean,
    default: false
  },
  isSaving: {
    type: Boolean,
    default: false
  }
})

const emit = defineEmits(['save-profile', 'upload-avatar', 'remove-avatar'])

const form = ref({
  fullName: '',
  publicName: '',
  jobTitle: '',
  department: '',
  organization: '',
  location: localStorage.getItem('profile_location') || '',
  timeZone: localStorage.getItem('profile_timezone') || 'GMT+7',
  collaboration: '',
  coverPositionY: 50
})

const visibility = ref({
  fullName: 'Anyone',
  publicName: 'Anyone',
  jobTitle: 'Organization',
  department: 'Organization',
  organization: 'Organization',
  location: 'Anyone',
  timeZone: 'Anyone'
})

// Synced load
watch(() => props.profileData, (newData) => {
  if (newData) {
    form.value.fullName = newData.fullName || ''
    form.value.publicName = newData.publicName || ''
    form.value.jobTitle = newData.jobTitle || ''
    form.value.department = newData.department || ''
    form.value.organization = newData.organization || ''
    form.value.collaboration = newData.collaboration || ''
    form.value.coverPositionY = newData.coverPositionY ?? 50
  }
}, { immediate: true })

onMounted(() => {
  // Load visibility settings from local storage to keep state persistent
  const savedVisibility = localStorage.getItem('profile_visibility')
  if (savedVisibility) {
    try {
      visibility.value = { ...visibility.value, ...JSON.parse(savedVisibility) }
    } catch (e) {
      console.error('Failed parsing visibility settings', e)
    }
  }
})



const getVisibilityIcon = (val) => {
  switch (val) {
    case 'Anyone':
      return 'fa-solid fa-earth-americas'
    case 'Organization':
      return 'fa-solid fa-building'
    case 'OnlyMe':
      return 'fa-solid fa-lock'
    default:
      return 'fa-solid fa-earth-americas'
  }
}

const triggerAvatarUpload = () => {
  avatarInput.value?.click()
}

const onAvatarFileChange = (event) => {
  const file = event.target.files?.[0]
  if (!file) return
  emit('upload-avatar', event)
}

const removeAvatar = () => {
  emit('remove-avatar')
}

const onSubmit = () => {
  // Save location and timezone to localstorage
  localStorage.setItem('profile_location', form.value.location)
  localStorage.setItem('profile_timezone', form.value.timeZone)
  localStorage.setItem('profile_visibility', JSON.stringify(visibility.value))

  emit('save-profile', {
    fullName: form.value.fullName,
    publicName: form.value.publicName,
    jobTitle: form.value.jobTitle,
    department: form.value.department,
    organization: form.value.organization,
    collaboration: form.value.collaboration,
    coverPositionY: form.value.coverPositionY
  })
}
</script>

<style scoped>
.profile-card {
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px !important;
  padding: 32px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  display: flex;
  flex-direction: column;
  gap: 32px;
}

.card-header {
  margin-bottom: 8px;
}

.card-title {
  font-size: 24px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin-bottom: 8px;
}

.card-subtitle {
  font-size: 14px;
  color: var(--color-text-secondary);
  line-height: 1.5;
  margin: 0;
}

.form-section {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.section-title {
  font-size: 18px;
  font-weight: 600;
  color: var(--color-text-primary);
  margin: 0;
}

.section-desc {
  font-size: 14px;
  color: var(--color-text-secondary);
  margin: 0;
  line-height: 1.5;
}

.profile-photo-block {
  display: flex;
  align-items: center;
  gap: 24px;
}

.photo-preview-wrapper {
  flex-shrink: 0;
}

.large-avatar-preview {
  height: 96px;
  width: 96px;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 36px;
  font-weight: 700;
  border: 1px solid var(--color-border);
}

.photo-actions {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.photo-buttons {
  display: flex;
  gap: 12px;
}

.photo-hint {
  font-size: 12px;
  color: var(--color-text-muted);
  margin: 0;
}

.section-divider {
  border: none;
  border-top: 1px solid var(--color-border);
  margin: 0;
}

.atlassian-form-grid {
  display: flex;
  flex-direction: column;
  gap: 20px;
  margin-top: 12px;
}

.form-row {
  display: grid;
  grid-template-columns: 2fr 1.2fr;
  gap: 20px;
  align-items: start;
}

.form-row.full-width {
  grid-template-columns: 1fr;
}

.row-info, .row-visibility {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.row-label {
  font-size: 12px;
  font-weight: 700;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.05em;
  margin: 0;
}

.mr-2 {
  margin-right: 8px;
}

.form-save-actions {
  margin-top: 24px;
  display: flex;
  justify-content: flex-start;
}

:deep(.el-input__wrapper),
:deep(.el-select .el-input__wrapper) {
  padding: 8px 12px !important;
  height: 40px !important;
  border-radius: 6px !important;
  background-color: var(--color-input-bg) !important;
  border: 1px solid var(--color-input-border) !important;
  box-shadow: none !important;
}

:deep(.el-input__inner) {
  border: none !important;
  background: transparent !important;
  padding: 0 !important;
  height: auto !important;
}

:deep(.el-select .el-input__wrapper:hover),
:deep(.el-input__wrapper:hover) {
  border-color: var(--color-border-hover) !important;
}

:deep(.el-input__wrapper.is-focus),
:deep(.el-select .el-input.is-focus .el-input__wrapper) {
  border-color: #0052cc !important;
}

@media (max-width: 768px) {
  .form-row {
    grid-template-columns: 1fr;
    gap: 12px;
  }
  
  .profile-photo-block {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }
}
</style>
