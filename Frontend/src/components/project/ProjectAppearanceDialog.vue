<template>
  <el-dialog
    v-model="visibleComp"
    class="project-appearance-dialog sa-data-dialog sa-modal--lg"
    width="880px"
    :show-close="false"
    append-to-body
    destroy-on-close
  >
    <template #header>
      <DataModalHeader icon="bi bi-palette2" title="Customize project" description="Choose a distinct avatar and cover for this project" close-label="Close" @close="handleClose" />
    </template>
    <div class="appearance-shell">

      <div class="appearance-body">
        <DataModalSection icon="bi bi-eye" title="Live preview">
        <aside class="preview-column">
          <div class="project-preview" :style="{ background: previewBackground }">
            <ProjectAvatar :icon="form.icon" :background="form.background" size="xl" />
            <div class="preview-copy" :class="{ light: previewTone === 'light' }">
              <strong>{{ project?.name || 'Untitled project' }}</strong>
              <span>{{ project?.description || 'Project workspace' }}</span>
            </div>
          </div>
          <div class="preview-note">
            <i class="bi bi-check2-circle"></i>
            <span>Avatar contrast is adjusted automatically.</span>
          </div>
        </aside>
        </DataModalSection>

        <DataModalSection icon="bi bi-sliders" title="Appearance options">
        <div class="picker-column">
          <div class="appearance-tabs" role="tablist" aria-label="Project appearance options">
            <button
              type="button"
              role="tab"
              class="appearance-tab"
              :class="{ active: activeTab === 'avatar' }"
              :aria-selected="activeTab === 'avatar'"
              @click="activeTab = 'avatar'"
            >
              Avatar
            </button>
            <button
              type="button"
              role="tab"
              class="appearance-tab"
              :class="{ active: activeTab === 'background' }"
              :aria-selected="activeTab === 'background'"
              @click="activeTab = 'background'"
            >
              Background
            </button>
          </div>

          <section v-show="activeTab === 'avatar'" class="picker-section" role="tabpanel">
            <ProjectAvatarPicker v-model="form.icon" />
          </section>

          <section v-show="activeTab === 'background'" class="picker-section" role="tabpanel">
            <ProjectBackgroundPicker v-model="form.background" />
          </section>
        </div>
        </DataModalSection>
      </div>

      <footer class="appearance-footer">
        <button type="button" class="secondary-action cancel-btn" @click="handleClose"><i class="bi bi-x-lg"></i>Cancel</button>
        <button type="button" class="primary-action" :disabled="saving" @click="saveAppearance">
          <i :class="saving ? 'bi bi-arrow-repeat spin' : 'bi bi-check2'"></i>
          {{ saving ? 'Saving...' : 'Save changes' }}
        </button>
      </footer>
    </div>
  </el-dialog>
</template>

<script setup>
import { computed, reactive, ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import axiosClient from '@/api/axiosClient'
import { useProjectStore } from '@/store/useProjectStore'
import ProjectAvatar from '@/components/project/ProjectAvatar.vue'
import ProjectAvatarPicker from '@/components/project/ProjectAvatarPicker.vue'
import ProjectBackgroundPicker from '@/components/project/ProjectBackgroundPicker.vue'
import DataModalHeader from '@/components/common/Foundation/DataModalHeader.vue'
import DataModalSection from '@/components/common/Foundation/DataModalSection.vue'
import {
  DEFAULT_PROJECT_BACKGROUND,
  DEFAULT_PROJECT_ICON,
  getProjectBackground,
  normalizeProjectIcon
} from '@/config/projectAppearance'

const props = defineProps({
  visible: Boolean,
  project: { type: Object, default: null }
})
const emit = defineEmits(['update:visible', 'saved'])
const projectStore = useProjectStore()

const form = reactive({ icon: DEFAULT_PROJECT_ICON, background: DEFAULT_PROJECT_BACKGROUND })
const saving = ref(false)
const activeTab = ref('avatar')

const visibleComp = computed({
  get: () => props.visible,
  set: value => emit('update:visible', value)
})

const previewPreset = computed(() => getProjectBackground(form.background))
const previewBackground = computed(() => previewPreset.value.value)
const previewTone = computed(() => previewPreset.value.tone)

watch(
  () => [props.visible, props.project],
  ([visible]) => {
    if (!visible) return
    activeTab.value = 'avatar'
    form.icon = normalizeProjectIcon(props.project?.icon)
    const currentBackground = getProjectBackground(props.project?.cover)
    form.background = currentBackground.id
  },
  { immediate: true }
)

const handleClose = () => {
  if (saving.value) return
  visibleComp.value = false
}

const saveAppearance = async () => {
  if (!props.project?.id || saving.value) return
  saving.value = true
  try {
    await axiosClient.put(`/projects/${props.project.id}/cover`, {
      coverUrl: form.background,
      coverAltText: `Cover for ${props.project.name || 'project'}`,
      icon: form.icon
    })
    projectStore.applyProjectAppearance(props.project.id, {
      icon: form.icon,
      cover: form.background
    })
    emit('saved', { ...props.project, icon: form.icon, cover: form.background })
    ElMessage.success('Project appearance updated')
    visibleComp.value = false
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Could not update project appearance')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.appearance-shell { display: flex; flex-direction: column; max-height: min(780px, calc(100vh - 96px)); overflow: hidden; color: var(--color-text-primary); }
.appearance-header, .appearance-footer { flex: 0 0 auto; background: var(--color-surface); }
.appearance-header { display: flex; align-items: center; justify-content: space-between; min-height: 76px; padding: 16px 22px; border-bottom: 1px solid var(--color-border); }
.header-copy { display: flex; align-items: center; gap: 13px; }
.header-icon { display: grid; place-items: center; width: 40px; height: 40px; border-radius: 11px; background: color-mix(in srgb, var(--color-accent) 10%, var(--color-surface)); color: var(--color-accent); font-size: 19px; }
.header-copy h2 { margin: 0; font-size: 19px; font-weight: 800; }
.header-copy p { margin: 3px 0 0; color: var(--color-text-muted); font-size: 12px; }
.close-button { display: grid; place-items: center; width: 36px; height: 36px; border: 0; border-radius: 9px; background: transparent; color: var(--color-text-muted); cursor: pointer; }
.close-button:hover { background: var(--color-surface-hover); color: var(--color-text-primary); }
.appearance-body { display: grid; grid-template-columns: 260px minmax(0, 1fr); flex: 1 1 auto; min-height: 0; overflow: hidden; }
.preview-column { padding: 22px; border-right: 1px solid var(--color-border); background: color-mix(in srgb, var(--color-bg) 82%, var(--color-surface)); }
.section-label { margin: 0 0 7px; color: var(--color-text-muted); font-size: 11px; font-weight: 800; text-transform: uppercase; }
.project-preview { display: flex; min-height: 230px; padding: 22px; flex-direction: column; justify-content: flex-end; gap: 16px; border-radius: 14px; box-shadow: 0 16px 36px rgba(15, 23, 42, .14); }
.preview-copy { display: flex; flex-direction: column; gap: 5px; color: #fff; text-shadow: 0 1px 3px rgba(15, 23, 42, .38); }
.preview-copy.light { color: #0f172a; text-shadow: none; }
.preview-copy strong { font-size: 17px; line-height: 1.25; }
.preview-copy span { font-size: 12px; line-height: 1.45; opacity: .78; }
.preview-note { display: flex; gap: 8px; margin-top: 14px; color: var(--color-text-muted); font-size: 11px; line-height: 1.45; }
.preview-note i { color: #10b981; }
.picker-column { display: flex; min-width: 0; min-height: 0; padding: 22px; flex-direction: column; overflow: hidden; }
.appearance-tabs {
  display: inline-flex; align-items: center; gap: 3px; margin-bottom: 18px; padding: 3px;
  border: 1px solid var(--color-border); border-radius: 10px; background: color-mix(in srgb, var(--color-bg) 76%, var(--color-surface));
}
.appearance-tab {
  display: inline-flex; align-items: center; justify-content: center; gap: 7px; height: 34px; padding: 0 14px;
  border: 0; border-radius: 7px; background: transparent; color: var(--color-text-muted); cursor: pointer;
  font-size: 12px; font-weight: 700; transition: background-color .16s ease, color .16s ease, box-shadow .16s ease;
}
.appearance-tab:hover { color: var(--color-text-primary); }
.appearance-tab.active {
  background: var(--color-surface); color: var(--color-text-primary); box-shadow: 0 1px 3px rgba(15, 23, 42, .1);
}
.picker-section { flex: 1 1 auto; min-width: 0; min-height: 0; overflow: hidden; }
.picker-section :deep(.avatar-picker),
.picker-section :deep(.background-picker) { height: 100%; }
.picker-section :deep(.icon-scroll-area),
.picker-section :deep(.background-scroll-area) { max-height: none; }
.appearance-footer { display: flex; justify-content: flex-end; gap: 10px; padding: 14px 22px; border-top: 1px solid var(--color-border); }
.secondary-action, .primary-action { height: 40px; padding: 0 17px; border-radius: 9px; font-size: 13px; font-weight: 750; cursor: pointer; }
.secondary-action { border: 1px solid var(--color-border); background: var(--color-surface); color: var(--color-text-secondary); }
.primary-action { display: inline-flex; align-items: center; gap: 8px; border: 1px solid var(--color-accent); background: var(--color-accent); color: #fff; }
.primary-action:disabled { cursor: wait; opacity: .65; }
.spin { animation: spin .8s linear infinite; }
@keyframes spin { to { transform: rotate(360deg); } }
@media (max-width: 720px) {
  .appearance-body { grid-template-columns: 1fr; overflow: auto; }
  .preview-column { border-right: 0; border-bottom: 1px solid var(--color-border); }
  .project-preview { min-height: 180px; }
  .picker-column { min-height: 430px; overflow: visible; }
}
</style>

<style>
.project-appearance-dialog.el-dialog { max-width: 94vw; margin-top: 48px; border: 1px solid var(--color-border); border-radius: 14px; overflow: hidden; background: var(--color-surface); box-shadow: 0 24px 60px rgba(15, 23, 42, .18); }
.project-appearance-dialog .el-dialog__header { display: none !important; }
.project-appearance-dialog .el-dialog__body { padding: 0 !important; margin: 0 !important; }
</style>
