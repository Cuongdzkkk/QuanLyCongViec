<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { Bar, Line, Radar } from 'vue-chartjs'
import {
  ArcElement,
  BarElement,
  CategoryScale,
  Chart as ChartJS,
  Filler,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  RadialLinearScale,
  Title,
  Tooltip
} from 'chart.js'

import axiosClient from '@/api/axiosClient'

ChartJS.register(
  Title,
  Tooltip,
  Legend,
  BarElement,
  CategoryScale,
  LinearScale,
  PointElement,
  LineElement,
  RadialLinearScale,
  Filler,
  ArcElement
)

import { useI18n } from '@/composables/useI18n'

const { t } = useI18n()
const activeTab = ref('Overview')
const analyticsScope = ref('Current project')
const insightDimension = ref('Accuracy')
const workItemMetric = ref('Work item')
const selectedProjectId = ref(localStorage.getItem('currentProjectId') || '')
const projectOptions = ref([])
const loadingPlanning = ref(false)

const totalTasks = ref(0)
const totalProjects = ref(0)
const totalMembers = ref(0)
const myTasks = ref(0)
const overdueTasks = ref(0)
const statusStats = ref([])
const priorityStats = ref([])

const planningOverview = ref({
  totalProjects: 0,
  totalTasks: 0,
  totalCommittedStoryPoints: 0,
  completedStoryPoints: 0,
  carryOverStoryPoints: 0,
  totalEstimatedHours: 0,
  totalActualHours: 0,
  totalLoggedHours: 0
})

const velocitySummary = ref({
  committedStoryPoints: 0,
  completedStoryPoints: 0,
  carryOverStoryPoints: 0,
  completionRate: 0,
  byProject: [],
  bySprint: []
})

const estimateAccuracy = ref({
  averageAccuracyPercent: 100,
  accurateCount: 0,
  underEstimatedCount: 0,
  overEstimatedCount: 0,
  unplannedCount: 0,
  rows: [],
  byUser: []
})

const workloadSummary = ref({
  rows: [],
  overCapacityCount: 0,
  nearLimitCount: 0
})

const managerReview = ref({
  canConfirmBaseline: false,
  selectedProjectId: null,
  projects: [],
  riskSummary: {
    overCapacityMembers: 0,
    nearLimitMembers: 0,
    carryOverProjects: 0,
    unplannedTasks: 0
  }
})

const fetchStats = async () => {
  try {
    const res = await axiosClient.get('/dashboard/stats')
    const data = res.data?.data
    if (!data) return

    totalTasks.value = data.totalTasks || 0
    totalProjects.value = data.totalProjects || 0
    totalMembers.value = data.totalMembers || 0
    myTasks.value = data.myTasks || 0
    overdueTasks.value = data.overdueTasks || 0
    statusStats.value = data.byStatus || []
    priorityStats.value = data.byPriority || []
  } catch (error) {
    console.warn('Unable to load dashboard stats.', error)
  }
}

const fetchProjectOptions = async () => {
  try {
    const res = await axiosClient.get('/projects/discovery')
    projectOptions.value = res.data?.data || []
  } catch {
    projectOptions.value = []
  }
}

const fetchPlanningSummary = async () => {
  loadingPlanning.value = true
  try {
    const effectiveProjectId = analyticsScope.value === 'Current project'
      ? (selectedProjectId.value || localStorage.getItem('currentProjectId') || '')
      : ''

    const res = await axiosClient.get('/analytics/planning-summary', {
      params: effectiveProjectId ? { projectId: effectiveProjectId } : {}
    })

    const data = res.data?.data || {}
    planningOverview.value = data.overview || planningOverview.value
    velocitySummary.value = data.velocity || velocitySummary.value
    estimateAccuracy.value = data.estimateAccuracy || estimateAccuracy.value
    workloadSummary.value = data.workload || workloadSummary.value
    managerReview.value = data.managerReview || managerReview.value
    return true
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to load planning analytics.')
    return false
  } finally {
    loadingPlanning.value = false
  }
}

const confirmPlanningBaseline = async () => {
  const projectId = selectedProjectId.value || managerReview.value.selectedProjectId
  if (!projectId) {
    ElMessage.warning('Select a project before confirming the planning baseline.')
    return
  }

  try {
    await axiosClient.post(`/analytics/projects/${projectId}/confirm-baseline`)
    const refreshed = await fetchPlanningSummary()
    if (!refreshed) return
    ElMessage.success('Planning baseline confirmed.')
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to confirm planning baseline.')
  }
}

const selectAnalyticsScope = (scope) => {
  analyticsScope.value = scope
  if (scope === 'Current project' && !selectedProjectId.value) {
    selectedProjectId.value = localStorage.getItem('currentProjectId') || ''
  }
  fetchPlanningSummary()
}

const selectWorkItemMetric = (metric) => {
  workItemMetric.value = metric
  activeTab.value = 'Work items'
}

const selectInsightDimension = (dimension) => {
  insightDimension.value = dimension
}

const handleProjectChange = (event) => {
  selectedProjectId.value = event.target.value
  localStorage.setItem('currentProjectId', selectedProjectId.value || '')
  fetchPlanningSummary()
}

onMounted(() => {
  fetchStats()
  fetchProjectOptions()
  fetchPlanningSummary()
})

const getPriorityLabel = (val) => {
  const map = { 0: 'None', 1: 'Low', 2: 'Normal', 3: 'High', 4: 'Urgent' }
  return map[val] || 'None'
}

const getPriorityColor = (val) => {
  const map = { 0: 'var(--color-text-muted)', 1: '#10B981', 2: '#3B82F6', 3: '#F97316', 4: '#EF4444' }
  return map[val] || 'var(--color-text-muted)'
}

const radarChartData = computed(() => ({
  labels: ['Committed SP', 'Completed SP', 'Carry-over', 'Accuracy', 'Estimated h', 'Logged h'],
  datasets: [{
    label: t('Planning health'),
    data: [
      Number(velocitySummary.value.committedStoryPoints || 0),
      Number(velocitySummary.value.completedStoryPoints || 0),
      Number(velocitySummary.value.carryOverStoryPoints || 0),
      Number(estimateAccuracy.value.averageAccuracyPercent || 0),
      Number(planningOverview.value.totalEstimatedHours || 0),
      Number(planningOverview.value.totalLoggedHours || 0)
    ],
    fill: true,
    backgroundColor: 'rgba(14, 165, 233, 0.28)',
    borderColor: '#0EA5E9',
    pointBackgroundColor: '#0EA5E9',
    pointBorderColor: '#fff',
    pointHoverBackgroundColor: '#fff',
    pointHoverBorderColor: '#0EA5E9'
  }]
}))

const radarChartOptions = {
  responsive: true,
  maintainAspectRatio: false,
  scales: {
    r: {
      grid: { color: 'var(--color-border)' },
      angleLines: { color: 'var(--color-border)' },
      ticks: { display: false }
    }
  },
  plugins: { legend: { display: false } }
}

const lineChartData = computed(() => ({
  labels: (velocitySummary.value.byProject || []).map(project => project.name),
  datasets: [
    {
      label: 'Committed story points',
      data: (velocitySummary.value.byProject || []).map(project => Number(project.velocityCommitted || 0)),
      borderColor: '#0EA5E9',
      backgroundColor: '#0EA5E9'
    },
    {
      label: 'Completed story points',
      data: (velocitySummary.value.byProject || []).map(project => Number(project.velocityCompleted || 0)),
      borderColor: '#10B981',
      backgroundColor: '#10B981'
    }
  ]
}))

const sprintVelocityChartData = computed(() => ({
  labels: (velocitySummary.value.bySprint || []).map(sprint => sprint.sprintName),
  datasets: [
    {
      label: 'Committed',
      data: (velocitySummary.value.bySprint || []).map(sprint => Number(sprint.committedStoryPoints || 0)),
      backgroundColor: '#3B82F6',
      borderRadius: 4
    },
    {
      label: 'Completed',
      data: (velocitySummary.value.bySprint || []).map(sprint => Number(sprint.completedStoryPoints || 0)),
      backgroundColor: '#10B981',
      borderRadius: 4
    },
    {
      label: 'Carry-over',
      data: (velocitySummary.value.bySprint || []).map(sprint => Number(sprint.carryOverStoryPoints || 0)),
      backgroundColor: '#F59E0B',
      borderRadius: 4
    }
  ]
}))

const barChartData = computed(() => {
  if (insightDimension.value === 'Accuracy') {
    return {
      labels: ['Accurate', 'Under-estimated', 'Over-estimated', 'Unplanned'],
      datasets: [{
        label: 'Estimate accuracy',
        data: [
          estimateAccuracy.value.accurateCount || 0,
          estimateAccuracy.value.underEstimatedCount || 0,
          estimateAccuracy.value.overEstimatedCount || 0,
          estimateAccuracy.value.unplannedCount || 0
        ],
        backgroundColor: ['#10B981', '#F59E0B', '#EF4444', '#64748B'],
        borderRadius: 4
      }]
    }
  }

  if (insightDimension.value === 'Workload') {
    return {
      labels: (workloadSummary.value.rows || []).map(row => row.userName),
      datasets: [
        {
          label: 'Estimated hours',
          data: (workloadSummary.value.rows || []).map(row => Number(row.estimatedHours || 0)),
          backgroundColor: '#3B82F6',
          borderRadius: 4
        },
        {
          label: 'Actual hours',
          data: (workloadSummary.value.rows || []).map(row => Number(row.actualHours || 0)),
          backgroundColor: '#F59E0B',
          borderRadius: 4
        }
      ]
    }
  }

  if (insightDimension.value === 'Status') {
    const labels = statusStats.value.map(item => item.Status)
    const counts = statusStats.value.map(item => item.Count)
    return {
      labels: labels.length ? labels : ['No status data'],
      datasets: [{
        label: 'Work items by status',
        data: counts.length ? counts : [0],
        backgroundColor: '#3B82F6',
        borderRadius: 4
      }]
    }
  }

  const labels = priorityStats.value.map(item => getPriorityLabel(item.Priority))
  const counts = priorityStats.value.map(item => item.Count)
  const bgColors = priorityStats.value.map(item => getPriorityColor(item.Priority))

  return {
    labels: labels.length ? labels : ['None', 'Low', 'Normal', 'High', 'Urgent'],
    datasets: [{
      label: 'Work items by priority',
      data: labels.length ? counts : [0, 0, 0, 0, 0],
      backgroundColor: labels.length ? bgColors : ['var(--color-text-muted)', '#10B981', '#3B82F6', '#F97316', '#EF4444'],
      borderRadius: 4
    }]
  }
})

const chartConfig = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: { legend: { labels: { color: 'var(--color-text-primary)' } } },
  scales: {
    x: { grid: { color: 'var(--color-border)' }, ticks: { color: 'var(--color-text-muted)' } },
    y: { grid: { color: 'var(--color-border)' }, ticks: { color: 'var(--color-text-muted)' }, beginAtZero: true }
  }
}
</script>

<template>
  <div>
    <div class="analytics-wrapper">
      <header class="an-header">
        <div class="an-top-row">
          <span class="breadcrumb"><i class="fa-solid fa-chart-simple"></i>{{ t('Analytics') }}</span>
          <div class="ml-auto"></div>
        </div>
        <div class="an-bottom-row">
          <div class="an-tabs">
            <button class="tab-btn" :class="{ active: activeTab === 'Overview' }" @click="activeTab = 'Overview'">{{ t('Overview') }}</button>
            <button class="tab-btn" :class="{ active: activeTab === 'Work items' }" @click="activeTab = 'Work items'">{{ t('Work items') }}</button>
          </div>
          <div class="toolbar-stack ms-auto">
            <select class="project-select" :value="selectedProjectId" @change="handleProjectChange">
              <option value="">{{ t('Current project') }}</option>
              <option v-for="project in projectOptions" :key="project.id" :value="project.id">
                {{ project.name }}
              </option>
            </select>
            <el-dropdown trigger="click" @command="selectAnalyticsScope">
              <button class="plane-toolbar-btn" type="button"><i class="fa-solid fa-briefcase"></i> {{ analyticsScope }} <i class="fa-solid fa-chevron-down ms-2"></i></button>
              <template #dropdown>
                <el-dropdown-menu>
                  <el-dropdown-item command="Current project">{{ t('Current project') }}</el-dropdown-item>
                  <el-dropdown-item command="All my projects">All my projects</el-dropdown-item>
                </el-dropdown-menu>
              </template>
            </el-dropdown>
          </div>
        </div>
      </header>

      <div class="an-scrollable" v-if="activeTab === 'Overview'">
        <h2 class="page-title">{{ t('Overview') }}</h2>

        <div class="stats-grid">
          <div class="stat-box">
            <div class="stat-lbl">{{ t('Total work items') }}</div>
            <div class="stat-val">{{ totalTasks }}</div>
          </div>
          <div class="stat-box">
            <div class="stat-lbl">{{ t('My tasks') }}</div>
            <div class="stat-val">{{ myTasks }}</div>
          </div>
          <div class="stat-box">
            <div class="stat-lbl">{{ t('Overdue tasks') }}</div>
            <div class="stat-val danger">{{ overdueTasks }}</div>
          </div>
          <div class="stat-box">
            <div class="stat-lbl">{{ t('Committed story points') }}</div>
            <div class="stat-val">{{ planningOverview.totalCommittedStoryPoints }}</div>
          </div>
          <div class="stat-box">
            <div class="stat-lbl">{{ t('Completion rate') }}</div>
            <div class="stat-val">{{ velocitySummary.completionRate }}%</div>
          </div>
        </div>

        <div class="insights-grid">
          <div class="insight-box">
            <div class="insight-title">{{ t('Planning health') }}</div>
            <div class="radar-container mt-4">
              <Radar :data="radarChartData" :options="radarChartOptions" />
            </div>
          </div>

          <div class="insight-box">
            <div class="insight-title">{{ t('Velocity summary') }}</div>
            <div class="summary-list mt-4">
              <div class="sum-row"><span class="sum-lbl">{{ t('Completed story points') }}</span><span class="sum-val">{{ velocitySummary.completedStoryPoints }}</span></div>
              <div class="sum-row"><span class="sum-lbl">{{ t('Carry-over story points') }}</span><span class="sum-val">{{ velocitySummary.carryOverStoryPoints }}</span></div>
              <div class="sum-row"><span class="sum-lbl">{{ t('Estimated hours') }}</span><span class="sum-val">{{ planningOverview.totalEstimatedHours }}h</span></div>
              <div class="sum-row"><span class="sum-lbl">{{ t('Actual hours') }}</span><span class="sum-val">{{ planningOverview.totalActualHours }}h</span></div>
              <div class="sum-row"><span class="sum-lbl">{{ t('Logged hours') }}</span><span class="sum-val">{{ planningOverview.totalLoggedHours }}h</span></div>
            </div>
          </div>

          <div class="insight-box">
            <div class="insight-title">{{ t('Manager review') }}</div>
            <div class="manager-panel mt-4">
              <div class="pill-row">
                <span class="pill" :class="{ positive: managerReview.canConfirmBaseline }">
                  {{ managerReview.canConfirmBaseline ? 'Can confirm baseline' : 'View only' }}
                </span>
                <span class="pill warning">Over capacity: {{ workloadSummary.overCapacityCount }}</span>
              </div>
              <div class="summary-list">
                <div class="sum-row"><span class="sum-lbl">{{ t('Near-limit members') }}</span><span class="sum-val">{{ managerReview.riskSummary?.nearLimitMembers || 0 }}</span></div>
                <div class="sum-row"><span class="sum-lbl">{{ t('Carry-over projects') }}</span><span class="sum-val">{{ managerReview.riskSummary?.carryOverProjects || 0 }}</span></div>
                <div class="sum-row"><span class="sum-lbl">{{ t('Unplanned tasks') }}</span><span class="sum-val">{{ managerReview.riskSummary?.unplannedTasks || 0 }}</span></div>
              </div>
              <button
                v-if="managerReview.canConfirmBaseline"
                class="manager-action-btn"
                type="button"
                :disabled="loadingPlanning"
                @click="confirmPlanningBaseline"
              >
                {{ loadingPlanning ? 'Confirming...' : 'Confirm baseline' }}
              </button>
              <div class="baseline-list">
                <div v-for="project in managerReview.projects" :key="project.id" class="baseline-row">
                  <strong>{{ project.name }}</strong>
                  <small>{{ project.velocityCompleted }} / {{ project.velocityCommitted }} SP</small>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="an-scrollable" v-else>
        <div class="full-analytics-body">
          <div class="ap-stats-grid">
            <div class="stat-box">
              <span class="lbl">Average accuracy</span>
              <span class="val">{{ estimateAccuracy.averageAccuracyPercent }}%</span>
            </div>
            <div class="stat-box">
              <span class="lbl">Accurate</span>
              <span class="val">{{ estimateAccuracy.accurateCount }}</span>
            </div>
            <div class="stat-box">
              <span class="lbl">Under-estimated</span>
              <span class="val">{{ estimateAccuracy.underEstimatedCount }}</span>
            </div>
            <div class="stat-box">
              <span class="lbl">Over-estimated</span>
              <span class="val">{{ estimateAccuracy.overEstimatedCount }}</span>
            </div>
            <div class="stat-box">
              <span class="lbl">Unplanned</span>
              <span class="val">{{ estimateAccuracy.unplannedCount }}</span>
            </div>
          </div>

          <div class="ap-chart-card mt-4">
            <h4>Velocity by project</h4>
            <div class="line-chart-container">
              <Line :data="lineChartData" :options="chartConfig" />
            </div>
          </div>

          <div class="ap-chart-card mt-4">
            <h4>Velocity by sprint</h4>
            <div class="bar-chart-container mt-4">
              <Bar :data="sprintVelocityChartData" :options="chartConfig" />
            </div>
          </div>

          <div class="ap-chart-card mt-4">
            <div class="flex-between">
              <h4>{{ workItemMetric }} by {{ insightDimension }}</h4>
              <div class="insight-filters">
                <el-dropdown trigger="click" @command="selectWorkItemMetric">
                  <button class="filter-btn" type="button"><i class="fa-solid fa-briefcase"></i> {{ workItemMetric }} <i class="fa-solid fa-chevron-down"></i></button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item command="Work item">Work item</el-dropdown-item>
                      <el-dropdown-item command="Planning">Planning</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
                <el-dropdown trigger="click" @command="selectInsightDimension">
                  <button class="filter-btn" type="button"><i class="fa-solid fa-list"></i> {{ insightDimension }} <i class="fa-solid fa-chevron-down"></i></button>
                  <template #dropdown>
                    <el-dropdown-menu>
                      <el-dropdown-item command="Accuracy">{{ t('Accuracy') }}</el-dropdown-item>
                      <el-dropdown-item command="Workload">Workload</el-dropdown-item>
                      <el-dropdown-item command="Status">Status</el-dropdown-item>
                      <el-dropdown-item command="Priority">Priority</el-dropdown-item>
                    </el-dropdown-menu>
                  </template>
                </el-dropdown>
              </div>
            </div>
            <div class="bar-chart-container mt-4">
              <Bar :data="barChartData" :options="chartConfig" />
            </div>
          </div>

          <div class="ap-chart-card mt-4">
            <div class="flex-between">
              <h4>Top estimate accuracy gaps</h4>
              <span class="muted-copy">{{ estimateAccuracy.rows.length }} rows</span>
            </div>
            <div class="table-wrap mt-4">
              <table class="analytics-table">
                <thead>
                  <tr>
                    <th>Task</th>
                    <th>Estimated</th>
                    <th>Actual</th>
                    <th>Logged</th>
                    <th>{{ t('Accuracy') }}</th>
                    <th>Bucket</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="row in estimateAccuracy.rows" :key="row.id">
                    <td>{{ row.sequenceId || row.title }}</td>
                    <td>{{ row.estimatedHours }}h</td>
                    <td>{{ row.actualHours }}h</td>
                    <td>{{ row.loggedHours }}h</td>
                    <td>{{ row.accuracyPercent }}%</td>
                    <td>{{ row.bucket }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>

          <div class="ap-chart-card mt-4">
            <div class="flex-between">
              <h4>Estimator performance by assignee</h4>
              <span class="muted-copy">{{ estimateAccuracy.byUser.length }} rows</span>
            </div>
            <div class="table-wrap mt-4">
              <table class="analytics-table">
                <thead>
                  <tr>
                    <th>User</th>
                    <th>Tasks</th>
                    <th>Estimated</th>
                    <th>Actual</th>
                    <th>Logged</th>
                    <th>{{ t('Accuracy') }}</th>
                    <th>Progress</th>
                  </tr>
                </thead>
                <tbody>
                  <tr v-for="row in estimateAccuracy.byUser" :key="row.userId">
                    <td>{{ row.userName }}</td>
                    <td>{{ row.taskCount }}</td>
                    <td>{{ row.estimatedHours }}h</td>
                    <td>{{ row.actualHours }}h</td>
                    <td>{{ row.loggedHours }}h</td>
                    <td>{{ row.averageAccuracyPercent }}%</td>
                    <td>{{ row.averageProgressPercent }}%</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.analytics-wrapper { display: flex; flex-direction: column; height: 100vh; background: var(--color-bg); color: var(--color-text-primary); }
.an-header { padding: 16px 24px 0; border-bottom: 1px solid var(--color-border); }
.an-top-row { display: flex; align-items: center; margin-bottom: 24px; }
.breadcrumb { color: var(--color-text-muted); font-size: 14px; display: flex; align-items: center; gap: 8px; font-weight: 500; }
.an-bottom-row { display: flex; align-items: center; margin-bottom: -1px; gap: 16px; }
.an-tabs { display: flex; gap: 24px; }
.tab-btn { background: transparent; border: none; font-size: 13px; font-weight: 500; color: var(--color-text-muted); cursor: pointer; padding: 8px 0; border-bottom: 2px solid transparent; }
.tab-btn:hover { color: var(--color-text-primary); }
.tab-btn.active { color: var(--color-text-primary); border-bottom: 2px solid var(--color-text-primary); }
.toolbar-stack { display: flex; align-items: center; gap: 12px; }
.project-select,
.plane-toolbar-btn,
.filter-btn,
.manager-action-btn {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  color: #d4d4d8;
  font-size: 13px;
  font-weight: 500;
  border-radius: 6px;
  padding: 6px 12px;
}
.project-select { min-width: 180px; }
.plane-toolbar-btn,
.filter-btn,
.manager-action-btn { cursor: pointer; display: inline-flex; align-items: center; gap: 6px; }
.plane-toolbar-btn:hover,
.filter-btn:hover,
.manager-action-btn:hover { background: var(--color-border); }
.manager-action-btn:disabled { opacity: 0.7; cursor: not-allowed; }
.an-scrollable { padding: 32px; overflow-y: auto; flex: 1; }
.page-title { margin: 0 0 32px 0; font-size: 20px; font-weight: 600; }
.stats-grid { display: grid; grid-template-columns: repeat(5, 1fr); gap: 24px; margin-bottom: 40px; }
.stat-box { display: flex; flex-direction: column; gap: 8px; }
.stat-lbl, .lbl, .muted-copy { font-size: 12px; color: var(--color-text-muted); font-weight: 500; }
.stat-val, .val { font-size: 20px; font-weight: 600; color: var(--color-text-primary); }
.danger { color: #ef4444; }
.insights-grid { display: grid; grid-template-columns: 1.1fr 1fr 1fr; gap: 32px; }
.insight-box, .ap-chart-card {
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  border-radius: 16px;
  padding: 20px;
}
.insight-title, .ap-chart-card h4 { font-size: 15px; font-weight: 600; margin: 0; }
.radar-container { height: 260px; }
.summary-list, .baseline-list { display: flex; flex-direction: column; gap: 12px; }
.sum-row, .baseline-row { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.sum-lbl { color: var(--color-text-muted); font-size: 13px; }
.sum-val { color: var(--color-text-primary); font-weight: 600; }
.manager-panel { display: grid; gap: 14px; }
.pill-row { display: flex; gap: 10px; flex-wrap: wrap; }
.pill {
  padding: 6px 10px;
  border-radius: 999px;
  background: rgba(148, 163, 184, 0.15);
  color: #cbd5e1;
  font-size: 12px;
}
.pill.positive { background: rgba(16, 185, 129, 0.18); color: #6ee7b7; }
.pill.warning { background: rgba(245, 158, 11, 0.18); color: #fcd34d; }
.mt-4 { margin-top: 24px; }
.full-analytics-body { width: 100%; max-width: 1100px; }
.ap-stats-grid { display: grid; grid-template-columns: repeat(5, minmax(0, 1fr)); gap: 16px; }
.flex-between { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
.insight-filters { display: flex; gap: 12px; }
.line-chart-container, .bar-chart-container { height: 280px; margin-top: 16px; }
.table-wrap { overflow: auto; }
.analytics-table { width: 100%; border-collapse: collapse; font-size: 13px; }
.analytics-table th, .analytics-table td { padding: 10px 12px; border-bottom: 1px solid var(--color-border); text-align: left; }
.analytics-table th { color: var(--color-text-muted); font-weight: 600; }

@media (max-width: 1200px) {
  .stats-grid,
  .ap-stats-grid,
  .insights-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .an-bottom-row,
  .toolbar-stack,
  .insight-filters,
  .stats-grid,
  .ap-stats-grid,
  .insights-grid {
    grid-template-columns: 1fr;
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
