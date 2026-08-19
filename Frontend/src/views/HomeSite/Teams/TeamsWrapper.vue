<template>
  <div class="teams-wrapper">
    <header class="module-header" v-if="!isDetailView">
      <div class="header-content">
        <div class="app-shell-title-wrap">
          <h1>{{ t('homeSite.teams.title') }}</h1>
          <div class="app-shell-header-help">
            <span class="app-shell-header-help-btn" aria-label="About Teams">
              <i class="fa-solid fa-question"></i>
            </span>
            <div class="app-shell-header-help-popover" role="tooltip">
              <span>TEAMS</span>
              <p>{{ t('homeSite.teams.emptyDescription') }}</p>
            </div>
          </div>
        </div>
        <div class="header-actions">
          <button class="primary-btn" @click="openCreateTeam">
            {{ t('homeSite.teams.startTeam') }}
          </button>
        </div>
      </div>

      <div class="tabs-nav">
        <router-link :to="teamsBasePath" class="tab-link" exact-active-class="active">
          {{ t('homeSite.teams.forYou') }}
        </router-link>
        <router-link :to="`${teamsBasePath}/list`" class="tab-link" active-class="active">
          {{ t('homeSite.teams.allTeams') }}
        </router-link>
        <router-link :to="`${teamsBasePath}/kudos`" class="tab-link" active-class="active">
          {{ t('homeSite.teams.kudos') }}
        </router-link>
        <router-link :to="`${teamsBasePath}/people`" class="tab-link" active-class="active">
          {{ t('homeSite.teams.everyone') }}
        </router-link>
      </div>
    </header>

    <div class="module-content" :class="{ 'detail-view': isDetailView }">
      <router-view></router-view>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useI18nStore } from '@/store/useI18nStore'

const i18nStore = useI18nStore()
const t = i18nStore.t
const route = useRoute()
const teamsBasePath = computed(() => route.path.startsWith('/teams') ? '/teams' : '/home/teams')
const isDetailView = computed(() => {
  return route.name === 'SpaceTeamDetail' || route.name === 'HomeTeamDetail' || !!route.params.id
})

const openCreateTeam = () => {
  window.dispatchEvent(new CustomEvent('global-create-click'))
}
</script>

<style scoped>
.teams-wrapper {
  display: flex;
  flex-direction: column;
  min-height: 100%;
}

.module-header {
  padding: var(--app-shell-header-top, 18px) var(--app-shell-page-x, 18px) 0;
  background: transparent;
  position: sticky;
  top: 0;
  z-index: 5;
}

.header-content {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 18px;
}

.header-content .app-shell-title-wrap h1 {
  margin: 0 !important;
  color: var(--color-text-primary, #172b4d) !important;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif !important;
  font-size: 26px !important;
  font-weight: 900 !important;
  line-height: 1.15 !important;
  letter-spacing: 0 !important;
}

.primary-btn {
  background-color: #0052cc;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-weight: 500;
  font-size: 14px;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover {
  background-color: #0047b3;
}

.tabs-nav {
  display: flex;
  align-items: center;
  gap: 6px !important;
  width: max-content !important;
  max-width: 100%;
  min-height: 42px;
  margin: 0 !important;
  padding: 4px !important;
  border: 1px solid rgba(148, 163, 184, 0.2) !important;
  border-radius: 9px !important;
  background: transparent !important;
  box-shadow: none !important;
  overflow-x: auto;
}

.tab-link {
  flex: 0 0 auto;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  min-height: 34px !important;
  min-width: max-content;
  padding: 0 16px !important;
  border: 0 !important;
  border-radius: 7px !important;
  background: transparent !important;
  color: #475569 !important;
  font-size: 12.5px !important;
  font-weight: 800 !important;
  line-height: 1;
  text-decoration: none;
  white-space: nowrap;
  transition: background 0.18s ease, color 0.18s ease;
}

.tab-link:hover {
  color: #0f172a !important;
  background: rgba(14, 165, 233, 0.06) !important;
}

.tab-link.active {
  color: #0369a1 !important;
  background: linear-gradient(135deg, rgba(34, 211, 238, 0.20), rgba(45, 212, 191, 0.14)) !important;
  box-shadow: none !important;
}

.module-content {
  padding: 18px var(--app-shell-page-x, 18px) 28px;
  flex: 1;
}

.module-content.detail-view {
  padding: 0 !important;
  max-width: none !important;
}
</style>
