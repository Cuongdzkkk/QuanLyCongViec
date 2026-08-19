<template>
  <AdminLayout>
    <div class="admin-page">
      <div class="page-header">
        <div class="breadcrumb">
          <i class="fa-solid fa-desktop"></i>
          <span>{{ t('System', 'System') }} / {{ t('System info', 'System info') }}</span>
        </div>
        <h1 class="text-hero">{{ t('System info', 'System info') }}</h1>
        <p class="text-desc">
          {{ t('View system diagnostics, server resources, database connections, and application runtime metrics.', 'View system diagnostics, server resources, database connections, and application runtime metrics.') }}
        </p>
      </div>

      <div class="info-container">
        <!-- Diagnostic Overview Cards -->
        <div class="metrics-row">
          <div class="metric-card">
            <div class="m-icon bg-blue"><i class="fa-solid fa-server"></i></div>
            <div class="m-content">
              <span class="m-label">{{ t('System status', 'System status') }}</span>
              <span class="m-val success-text">Active & Healthy</span>
            </div>
          </div>

          <div class="metric-card">
            <div class="m-icon bg-purple"><i class="fa-solid fa-database"></i></div>
            <div class="m-content">
              <span class="m-label">{{ t('DB status', 'DB status') }}</span>
              <span class="m-val success-text">Connected (2ms)</span>
            </div>
          </div>

          <div class="metric-card">
            <div class="m-icon bg-orange"><i class="fa-solid fa-clock"></i></div>
            <div class="m-content">
              <span class="m-label">{{ t('System uptime', 'System uptime') }}</span>
              <span class="m-val">12d 4h 18m</span>
            </div>
          </div>

          <div class="metric-card">
            <div class="m-icon bg-teal"><i class="fa-solid fa-users"></i></div>
            <div class="m-content">
              <span class="m-label">{{ t('Active sessions', 'Active sessions') }}</span>
              <span class="m-val">24 users</span>
            </div>
          </div>
        </div>

        <div class="settings-grid">
          <!-- Server Resources -->
          <section class="settings-card">
            <h2 class="text-section"><i class="fa-solid fa-chart-pie" style="margin-right: 8px; color: var(--color-accent);"></i>{{ t('Server resource usage', 'Server resource usage') }}</h2>
            <div class="resource-meters">
              <!-- Memory -->
              <div class="meter-group">
                <div class="meter-labels">
                  <span>{{ t('Memory (RAM)', 'Memory (RAM)') }}</span>
                  <span class="bold">4.2 GB / 8.0 GB (52%)</span>
                </div>
                <div class="progress-bar-wrap">
                  <div class="progress-bar-fill" style="width: 52%; background: linear-gradient(90deg, #3b82f6, #8b5cf6);"></div>
                </div>
              </div>

              <!-- CPU -->
              <div class="meter-group">
                <div class="meter-labels">
                  <span>{{ t('CPU Load', 'CPU Load') }}</span>
                  <span class="bold">18%</span>
                </div>
                <div class="progress-bar-wrap">
                  <div class="progress-bar-fill" style="width: 18%; background: linear-gradient(90deg, #10b981, #3b82f6);"></div>
                </div>
              </div>

              <!-- Disk -->
              <div class="meter-group">
                <div class="meter-labels">
                  <span>{{ t('Disk Space (SSD)', 'Disk Space (SSD)') }}</span>
                  <span class="bold">142 GB / 512 GB (27%)</span>
                </div>
                <div class="progress-bar-wrap">
                  <div class="progress-bar-fill" style="width: 27%; background: linear-gradient(90deg, #10b981, #059669);"></div>
                </div>
              </div>
            </div>
          </section>

          <!-- System Properties -->
          <section class="settings-card">
            <h2 class="text-section"><i class="fa-solid fa-circle-info" style="margin-right: 8px; color: var(--color-accent);"></i>{{ t('System properties', 'System properties') }}</h2>

            <div class="properties-table-wrap">
              <table class="jira-table properties-table">
                <tbody>
                  <tr>
                    <td><strong>{{ t('SprintA Version', 'SprintA Version') }}</strong></td>
                    <td>v2.4.2-stable</td>
                  </tr>
                  <tr>
                    <td><strong>{{ t('API Backend', 'API Backend') }}</strong></td>
                    <td>.NET 8.0 Web API</td>
                  </tr>
                  <tr>
                    <td><strong>{{ t('Database engine', 'Database engine') }}</strong></td>
                    <td>Microsoft SQL Server 2022</td>
                  </tr>
                  <tr>
                    <td><strong>{{ t('Operating System', 'Operating System') }}</strong></td>
                    <td>Windows Server 2022 (10.0.20348)</td>
                  </tr>
                  <tr>
                    <td><strong>{{ t('Server Time', 'Server Time') }}</strong></td>
                    <td>{{ new Date().toLocaleString() }}</td>
                  </tr>
                  <tr>
                    <td><strong>{{ t('Node Environment', 'Node Environment') }}</strong></td>
                    <td>production</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </section>
        </div>
      </div>
    </div>
  </AdminLayout>
</template>

<script setup>
import { ref } from 'vue'
import AdminLayout from '@/components/layout/AdminLayout.vue'
import { useLocale } from '@/composables/useLocale'

const { t } = useLocale()
</script>

<style scoped>


.metrics-row {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 16px;
  margin-bottom: 24px;
}

.metric-card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: 12px;
  padding: 16px;
  display: flex;
  align-items: center;
  gap: 16px;
}

.m-icon {
  width: 48px;
  height: 48px;
  border-radius: 10px;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
}

.bg-blue { background: rgba(59, 130, 246, 0.12); color: #3b82f6; }
.bg-purple { background: rgba(139, 92, 246, 0.12); color: #8b5cf6; }
.bg-orange { background: rgba(249, 115, 22, 0.12); color: #f97316; }
.bg-teal { background: rgba(20, 184, 166, 0.12); color: #14b8a6; }

.m-content {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.m-label {
  font-size: 12px;
  color: var(--color-text-muted);
  font-weight: 500;
}

.m-val {
  font-size: 18px;
  font-weight: 700;
  color: var(--color-text-primary);
}

.success-text {
  color: #10b981;
}

.resource-meters {
  display: flex;
  flex-direction: column;
  gap: 20px;
  margin-top: 16px;
}

.meter-group {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.meter-labels {
  display: flex;
  justify-content: space-between;
  font-size: 13.5px;
  color: var(--color-text-secondary);
}

.bold {
  font-weight: 600;
  color: var(--color-text-primary);
}

.progress-bar-wrap {
  width: 100%;
  height: 10px;
  background: var(--color-border);
  border-radius: 99px;
  overflow: hidden;
}

.progress-bar-fill {
  height: 100%;
  border-radius: 99px;
  transition: width 0.8s cubic-bezier(0.4, 0, 0.2, 1);
}

.properties-table-wrap {
  margin-top: 16px;
  border: 1px solid var(--color-border);
  border-radius: 8px;
  overflow: hidden;
}

.properties-table {
  width: 100%;
  border-collapse: collapse;
}

.properties-table td {
  padding: 10px 14px;
  border-bottom: 1px solid var(--color-border);
  font-size: 13px;
  color: var(--color-text-secondary);
}

.properties-table tr:last-child td {
  border-bottom: none;
}

.properties-table td strong {
  color: var(--color-text-primary);
}
</style>
