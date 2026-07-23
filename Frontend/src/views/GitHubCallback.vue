<template>
  <div class="callback-page">
    <div class="callback-card">
      <div v-if="isLoading" class="loading-state">
        <el-icon class="is-loading" :size="40" color="#0061ff"><Loading /></el-icon>
        <p>Đang xử lý đăng nhập GitHub...</p>
      </div>
      <div v-else-if="errorMsg" class="error-state">
        <el-icon :size="40" color="#f56c6c"><CircleCloseFilled /></el-icon>
        <p>{{ errorMsg }}</p>
        <el-button type="primary" @click="$router.push('/login')">Quay lại đăng nhập</el-button>
      </div>
    </div>
  </div>
</template>

  <script setup>
  import { ref, onMounted } from 'vue'
  import { useRouter, useRoute } from 'vue-router'
  import axiosClient from '../api/axiosClient'
  import { saveAuthSession } from '@/utils/authSession'
  import { clearLegacyGitHubCredentialStorage } from '@/utils/githubCredentials'
  import { Loading, CircleCloseFilled } from '@element-plus/icons-vue'
  import { ElMessage } from 'element-plus'

const router = useRouter()
const route = useRoute()
const isLoading = ref(true)
const errorMsg = ref('')

onMounted(async () => {
  clearLegacyGitHubCredentialStorage()
  const code = route.query.code

  if (!code) {
    errorMsg.value = 'Không nhận được mã xác thực từ GitHub.'
    isLoading.value = false
    return
  }

  try {
    const res = await axiosClient.post('/auth/github-login', { code })

    saveAuthSession(res.data.data)

    ElMessage.success('Đăng nhập bằng GitHub thành công!')
    router.push('/site-selection')
  } catch (error) {
    errorMsg.value = error.response?.data?.message || 'Không thể đăng nhập bằng GitHub.'
  } finally {
    isLoading.value = false
  }
})
</script>

<style scoped>
.callback-page {
  min-height: 100vh;
  display: flex;
  align-items: center;
  justify-content: center;
  background-color: #f8fafc;
}

.callback-card {
  background: white;
  padding: 60px 48px;
  border-radius: 20px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.04);
  text-align: center;
  min-width: 320px;
}

.loading-state,
.error-state {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
}

.loading-state p,
.error-state p {
  color: var(--color-text-muted);
  font-size: 16px;
}
</style>


