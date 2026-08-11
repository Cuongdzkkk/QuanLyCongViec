<template>
  <div class="settings-card email-card">
    <div class="card-header">
      <h2 class="card-title">{{ t('Email Addresses', 'Địa chỉ Email') }}</h2>
      <p class="card-subtitle">{{ t('Manage the email addresses associated with your account. Primary email is used for security alerts and notifications.', 'Quản lý các địa chỉ email được liên kết với tài khoản của bạn. Email chính được sử dụng cho các cảnh báo bảo mật và thông báo.') }}</p>
    </div>

    <div class="email-list">
      <div
        v-for="(item, index) in emails"
        :key="item.email"
        class="email-item-card"
        :class="{ 'is-primary': item.isPrimary }"
      >
        <div class="email-details">
          <div class="email-address-row">
            <span class="email-text">{{ item.email }}</span>
            <div class="badge-group">
              <el-tag v-if="item.isPrimary" type="success" effect="dark" size="small">{{ t('Primary', 'Chính') }}</el-tag>
              <span v-if="item.isVerified" class="verified-badge" :title="t('Verified', 'Đã xác minh')">
                <i class="fa-solid fa-circle-check text-blue"></i>
              </span>
              <el-tag v-else type="info" size="small">{{ t('Unverified', 'Chưa xác minh') }}</el-tag>
            </div>
          </div>
          <p class="email-added-date" v-if="item.isPrimary">
            {{ t('Used for account security, log in, and billing.', 'Sử dụng để bảo mật tài khoản, đăng nhập và thanh toán.') }}
          </p>
          <p class="email-added-date" v-else>
            {{ t('Added on', 'Đã thêm ngày') }} {{ formatDate(item.addedAt) }}
          </p>
        </div>

        <div class="email-actions">
          <el-button
            v-if="!item.isPrimary && item.isVerified"
            type="primary"
            plain
            size="small"
            @click="makePrimary(index)"
          >
            {{ t('Make primary', 'Đặt làm chính') }}
          </el-button>
          <el-button
            v-if="!item.isPrimary && !item.isVerified"
            type="success"
            plain
            size="small"
            @click="verifyEmail(index)"
          >
            {{ t('Verify', 'Xác minh') }}
          </el-button>
          <el-button
            v-if="!item.isPrimary"
            type="danger"
            plain
            size="small"
            @click="removeEmail(index)"
          >
            <i class="fa-solid fa-trash"></i>
          </el-button>
        </div>
      </div>
    </div>

    <div class="add-email-trigger">
      <el-button type="primary" @click="showAddDialog = true">
        <i class="fa-solid fa-plus mr-2"></i> {{ t('Add email address', 'Thêm địa chỉ email') }}
      </el-button>
    </div>

    <!-- Add Email Dialog -->
    <el-dialog
      v-model="showAddDialog"
      width="450px"
      destroy-on-close
      append-to-body
      class="sa-data-dialog sa-modal--form"
      :show-close="false"
    >
      <template #header>
        <DataModalHeader
          icon="bi bi-envelope-plus"
          :title="t('Add email address', 'Thêm địa chỉ email')"
          :description="t('Add and verify another email address for your account.', 'Thêm và xác thực một địa chỉ email khác cho tài khoản.')"
          :close-label="t('Close', 'Đóng')"
          @close="showAddDialog = false"
        />
      </template>
      <div class="add-email-form">
        <label class="field-label">{{ t('New email address', 'Địa chỉ email mới') }}</label>
        <el-input
          v-model="newEmailInput"
          placeholder="example@workmail.com"
          type="email"
          @keyup.enter="handleAddEmail"
        />
        <p class="dialog-desc">
          {{ t('We will send a verification link to this email to confirm ownership.', 'Chúng tôi sẽ gửi một liên kết xác thực tới email này để xác nhận quyền sở hữu.') }}
        </p>
      </div>
      <template #footer>
        <div class="dialog-actions">
          <el-button class="cancel-btn" @click="showAddDialog = false"><i class="bi bi-x-lg"></i>{{ t('Cancel', 'Hủy') }}</el-button>
          <el-button type="primary" :disabled="!isValidEmail" @click="handleAddEmail">
            {{ t('Add', 'Thêm') }}
          </el-button>
        </div>
      </template>
    </el-dialog>
  </div>
</template>

<script setup>
import { ref, computed, watch, onMounted } from 'vue'
import { useLocale } from '@/composables/useLocale'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'

const { t } = useLocale()

const props = defineProps({
  profileData: {
    type: Object,
    required: true
  }
})

const emails = ref([])
const showAddDialog = ref(false)
const newEmailInput = ref('')
const isLoading = ref(false)

const loadEmails = async () => {
  if (isLoading.value) return
  isLoading.value = true
  try {
    const res = await axiosClient.get('/users/emails')
    if (res.data && res.data.statusCode === 200) {
      emails.value = res.data.data
    } else if (res.data && res.data.data) {
      emails.value = res.data.data
    }
  } catch (error) {
    console.error('Failed to load emails:', error)
    ElMessage.error(t('Failed to load email addresses.', 'Không thể tải danh sách email.'))
  } finally {
    isLoading.value = false
  }
}

watch(() => props.profileData.email, (newVal) => {
  if (newVal) {
    loadEmails()
  }
}, { immediate: true })

onMounted(() => {
  if (props.profileData.email) {
    loadEmails()
  }
})

const formatDate = (dateStr) => {
  if (!dateStr) return ''
  try {
    return new Date(dateStr).toLocaleDateString()
  } catch (e) {
    return dateStr
  }
}

const isValidEmail = computed(() => {
  const re = /^[^\s@]+@[^\s@]+\.[^\s@]+$/
  return re.test(newEmailInput.value)
})

const handleAddEmail = async () => {
  if (!isValidEmail.value) return
  const trimmed = newEmailInput.value.trim()
  const duplicate = emails.value.some(e => e.email.toLowerCase() === trimmed.toLowerCase())
  if (duplicate) {
    ElMessage.warning(t('This email is already added.', 'Email này đã được thêm.'))
    return
  }

  try {
    const res = await axiosClient.post('/users/emails', { email: trimmed })
    if (res.data && res.data.data) {
      emails.value = res.data.data
    } else {
      await loadEmails()
    }
    ElMessage.success(t('Email address added. Please verify ownership.', 'Đã thêm địa chỉ email. Vui lòng xác thực quyền sở hữu.'))
    newEmailInput.value = ''
    showAddDialog.value = false
  } catch (error) {
    const msg = error.response?.data?.message || t('Failed to add email address.', 'Không thể thêm địa chỉ email.')
    ElMessage.error(msg)
  }
}

const makePrimary = async (index) => {
  const emailItem = emails.value[index]
  try {
    const res = await axiosClient.put('/users/emails/primary', { email: emailItem.email })
    if (res.data && res.data.data) {
      emails.value = res.data.data
    } else {
      await loadEmails()
    }
    ElMessage.success(t('Primary email updated successfully.', 'Đã cập nhật email chính thành công.'))
  } catch (error) {
    const msg = error.response?.data?.message || t('Failed to set primary email.', 'Không thể đặt email chính.')
    ElMessage.error(msg)
  }
}

const verifyEmail = async (index) => {
  const emailItem = emails.value[index]
  try {
    const res = await axiosClient.put('/users/emails/verify', { email: emailItem.email })
    if (res.data && res.data.data) {
      emails.value = res.data.data
    } else {
      await loadEmails()
    }
    ElMessage.success(t('Email verified successfully.', 'Đã xác minh email thành công.'))
  } catch (error) {
    const msg = error.response?.data?.message || t('Failed to verify email.', 'Không thể xác minh email.')
    ElMessage.error(msg)
  }
}

const removeEmail = async (index) => {
  const emailItem = emails.value[index]
  try {
    const res = await axiosClient.delete(`/users/emails/${encodeURIComponent(emailItem.email)}`)
    if (res.data && res.data.data) {
      emails.value = res.data.data
    } else {
      await loadEmails()
    }
    ElMessage.success(t('Email address removed.', 'Đã xóa địa chỉ email.'))
  } catch (error) {
    const msg = error.response?.data?.message || t('Failed to remove email.', 'Không thể xóa email.')
    ElMessage.error(msg)
  }
}
</script>

<style scoped>
.email-card {
  background-color: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px !important;
  padding: 32px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
  display: flex;
  flex-direction: column;
  gap: 24px;
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

.email-list {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.email-item-card {
  border: 1px solid var(--color-border);
  border-radius: 8px;
  padding: 18px 20px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  transition: all 0.2s ease;
  background-color: var(--color-surface);
}

.email-item-card.is-primary {
  border-left: 4px solid #0052cc;
}

.email-details {
  display: flex;
  flex-direction: column;
  gap: 6px;
}

.email-address-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.email-text {
  font-size: 15px;
  font-weight: 600;
  color: var(--color-text-primary);
}

.badge-group {
  display: flex;
  align-items: center;
  gap: 8px;
}

.verified-badge {
  font-size: 14px;
  display: flex;
  align-items: center;
}

.text-blue {
  color: #0c66e4;
}

.email-added-date {
  font-size: 12px;
  color: var(--color-text-secondary);
  margin: 0;
}

.email-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.add-email-trigger {
  margin-top: 12px;
}

.add-email-form {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.dialog-desc {
  font-size: 13px;
  color: var(--color-text-secondary);
  margin: 0;
  line-height: 1.5;
}

.mr-2 {
  margin-right: 8px;
}

:deep(.el-input__wrapper) {
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

@media (max-width: 600px) {
  .email-item-card {
    flex-direction: column;
    align-items: flex-start;
    gap: 16px;
  }
  
  .email-actions {
    width: 100%;
    justify-content: flex-end;
  }
}
</style>
