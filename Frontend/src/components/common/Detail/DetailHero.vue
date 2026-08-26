<template>
  <div class="sprinta-detail-hero">
    <!-- Cover -->
    <div class="sprinta-hero-cover" :style="coverStyle">
      <!-- Dynamic Pattern -->
      <div v-if="coverPattern !== 'none' && avatarIcon" class="sprinta-cover-pattern-wrapper">
        <div class="sprinta-cover-pattern">
          <div class="pattern-row" v-for="row in 6" :key="'row-'+row">
            <i v-for="col in 20" :key="col" :class="avatarIcon"></i>
          </div>
        </div>
        <div class="sprinta-cover-vignette"></div>
      </div>

      <!-- Back Button -->
      <button v-if="backUrl" class="sprinta-back-btn" @click="handleBack">
        <i class="fa-solid fa-arrow-left"></i>
        <span>{{ backText || 'Quay lại' }}</span>
      </button>

      <div class="sprinta-cover-actions">
        <slot name="cover-actions"></slot>
      </div>
    </div>

    <!-- Header Content (Below Cover) -->
    <div class="sprinta-hero-header-row">
      <!-- Avatar (Overlaps Cover) -->
      <div class="sprinta-hero-avatar-wrapper" :class="avatarType">
        <slot name="avatar">
          <!-- Default Avatar Fallback -->
          <div class="sprinta-default-avatar" :style="{ backgroundColor: avatarColor }">
            <i v-if="avatarIcon" :class="avatarIcon"></i>
            <span v-else>{{ avatarText }}</span>
          </div>
        </slot>
      </div>

      <!-- Main Content Container -->
      <div class="sprinta-hero-content-container">
        <!-- Title Block -->
        <div class="sprinta-hero-title-block">
          <div class="sprinta-title-row">
            <h1 class="sprinta-hero-title" :title="title">{{ title }}</h1>
            <slot name="badges"></slot>
          </div>
          <div class="sprinta-hero-meta" v-if="$slots.meta">
            <slot name="meta"></slot>
          </div>
        </div>

        <!-- Action Area -->
        <div class="sprinta-hero-actions">
          <slot name="actions"></slot>
          
          <!-- Overflow Menu -->
          <div class="sprinta-overflow-menu" v-if="$slots.overflow">
            <button class="sprinta-icon-btn" @click.stop="toggleMenu" title="Menu">
              <i class="fa-solid fa-ellipsis"></i>
            </button>
            <div class="sprinta-dropdown" v-if="isMenuOpen" v-click-outside="closeMenu">
              <slot name="overflow"></slot>
            </div>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
const props = defineProps({
  coverImage: String,
  coverColor: {
    type: String,
    default: '#091E42' // Fallback color
  },
  coverPattern: {
    type: String, // 'goal' | 'team' | 'none'
    default: 'none'
  },
  backUrl: [String, Object],
  backText: String,
  avatarType: {
    type: String,
    default: 'boxy' // 'boxy' | 'circle'
  },
  avatarIcon: String,
  avatarText: String,
  avatarColor: {
    type: String,
    default: '#0052CC'
  },
  title: {
    type: String,
    required: true
  }
})

const router = useRouter()
const isMenuOpen = ref(false)

const handleBack = () => {
  if (typeof props.backUrl === 'string') {
    router.push(props.backUrl)
  } else if (props.backUrl) {
    router.push(props.backUrl)
  } else {
    router.back()
  }
}

const toggleMenu = () => {
  isMenuOpen.value = !isMenuOpen.value
}

const closeMenu = () => {
  isMenuOpen.value = false
}

const coverStyle = computed(() => {
  const styles = {}
  if (props.coverImage) {
    styles.backgroundImage = `url(${props.coverImage})`
    styles.backgroundSize = 'cover'
    styles.backgroundPosition = 'center'
  } else if (props.coverPattern !== 'none') {
    // Both goal and team use the dynamic pattern now
    const baseColor = props.avatarColor || props.coverColor || '#091E42'
    styles.background = `linear-gradient(135deg, ${baseColor} 0%, rgba(0,0,0,0.4) 150%)`
    styles.backgroundColor = baseColor

  } else {
    styles.backgroundColor = props.coverColor
  }
  return styles
})

// Click outside directive implementation
const vClickOutside = {
  mounted(el, binding) {
    el.clickOutsideEvent = (event) => {
      if (!(el === event.target || el.contains(event.target))) {
        binding.value(event)
      }
    }
    document.body.addEventListener('click', el.clickOutsideEvent)
  },
  unmounted(el) {
    document.body.removeEventListener('click', el.clickOutsideEvent)
  }
}
</script>

<style>
.sprinta-detail-hero {
  background-color: #FFFFFF;
  position: relative;
  display: flex;
  flex-direction: column;
}

.sprinta-hero-cover {
  height: 160px; /* Standardized height for all */
  width: 100%;
  position: relative;
  transition: all 0.3s ease;
  overflow: hidden;
}

.sprinta-cover-pattern-wrapper {
  position: absolute;
  top: 0; left: 0; width: 100%; height: 100%;
  overflow: hidden;
  pointer-events: none; /* Let clicks pass through to background/cover actions */
}

.sprinta-cover-pattern {
  position: absolute;
  top: -50%; left: -20%; width: 150%; height: 200%;
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  gap: 32px;
  transform: rotate(-15deg);
}

.pattern-row {
  display: flex;
  gap: 48px;
}

.pattern-row:nth-child(even) {
  transform: translateX(24px); /* Stagger */
}

.pattern-row i {
  color: white;
  font-size: 36px;
  opacity: 0.12; /* Keep it subtle, blend with background */
}

.sprinta-cover-vignette {
  position: absolute;
  top: 0; left: 0; width: 100%; height: 100%;
  background: radial-gradient(circle at center, transparent 30%, rgba(0, 0, 0, 0.4) 150%);
}

.sprinta-back-btn {
  position: absolute;
  top: 24px;
  left: 40px;
  background: rgba(0, 0, 0, 0.4);
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 20px;
  display: flex;
  align-items: center;
  gap: 8px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background 0.2s, transform 0.1s;
  backdrop-filter: blur(4px);
  z-index: 10;
}

.sprinta-back-btn:hover {
  background: rgba(0, 0, 0, 0.6);
  transform: translateY(-1px);
}

.sprinta-cover-actions {
  position: absolute;
  top: 24px;
  right: 40px;
  z-index: 10;
}

.sprinta-hero-header-row {
  padding: 0 40px;
  position: relative;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
  box-sizing: border-box;
}

.sprinta-hero-avatar-wrapper {
  width: 104px; /* 96 + border */
  height: 104px;
  padding: 4px; /* White border illusion */
  background: white;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
  position: absolute;
  top: -52px;
  left: 40px;
}

.sprinta-hero-avatar-wrapper.boxy {
  border-radius: 16px;
}

.sprinta-hero-avatar-wrapper.circle {
  border-radius: 50%;
}

.sprinta-default-avatar {
  width: 100%;
  height: 100%;
  display: flex;
  align-items: center;
  justify-content: center;
  color: white;
  font-size: 32px;
  font-weight: 600;
  border-radius: inherit;
}

.sprinta-hero-content-container {
  display: flex;
  justify-content: space-between;
  align-items: flex-end; /* Align title and buttons horizontally at their bottom line */
  min-height: 52px; /* For when title is small */
  margin-top: 76px; /* 52px (avatar overlap) + 24px gap = 76px */
  padding: 0 0 16px 0;
}

.sprinta-hero-title-block {
  display: flex;
  flex-direction: column;
  gap: 6px;
  max-width: calc(100% - 150px);
}

.sprinta-title-row {
  display: flex;
  align-items: center;
  gap: 12px;
}

.sprinta-hero-title {
  font-size: 28px;
  font-weight: 600;
  color: #172B4D;
  margin: 0;
  line-height: 1.2;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 500px; /* Prevent breaking layout */
}

.sprinta-hero-meta {
  font-size: 14px;
  color: #5E6C84;
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}

.sprinta-hero-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

/* Common Button Styles for Details */
.sprinta-btn {
  height: 36px;
  padding: 0 16px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  cursor: pointer;
  transition: all 0.2s ease;
  border: none;
}

.sprinta-btn-primary {
  background-color: #0052CC;
  color: white;
}
.sprinta-btn-primary:hover:not(:disabled) {
  background-color: #0047B3;
}
.sprinta-btn-primary:disabled {
  background-color: #DFE1E6;
  color: #A5ADBA;
  cursor: not-allowed;
}

.sprinta-btn-secondary {
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526E;
}
.sprinta-btn-secondary:hover:not(:disabled) {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172B4D;
}
.sprinta-btn-secondary:disabled {
  background-color: rgba(9, 30, 66, 0.04);
  color: #A5ADBA;
  cursor: not-allowed;
}

.sprinta-icon-btn {
  width: 36px;
  height: 36px;
  padding: 0;
  border-radius: 3px;
  background-color: rgba(9, 30, 66, 0.04);
  color: #42526E;
  border: none;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s;
}
.sprinta-icon-btn:hover {
  background-color: rgba(9, 30, 66, 0.08);
  color: #172B4D;
}
.sprinta-icon-btn.starred {
  color: #FFAB00;
}

.sprinta-overflow-menu {
  position: relative;
}

.sprinta-dropdown {
  position: absolute;
  top: calc(100% + 4px);
  right: 0;
  background: white;
  border-radius: 3px;
  box-shadow: 0 4px 12px rgba(9, 30, 66, 0.15), 0 0 1px rgba(9, 30, 66, 0.31);
  min-width: 180px;
  padding: 4px 0;
  z-index: 50;
}

.sprinta-menu-item {
  display: flex;
  align-items: center;
  gap: 12px;
  width: 100%;
  padding: 8px 16px;
  background: none;
  border: none;
  text-align: left;
  font-size: 14px;
  color: #172B4D;
  cursor: pointer;
  transition: background 0.1s;
}
.sprinta-menu-item:hover:not(:disabled) {
  background-color: #F4F5F7;
}
.sprinta-menu-item:disabled {
  color: #A5ADBA;
  cursor: not-allowed;
}
.sprinta-menu-item.danger {
  color: #DE350B;
}
.sprinta-menu-item.danger:hover:not(:disabled) {
  background-color: #FFEBE6;
}

/* Responsive */
@media (max-width: 1024px) {
  .sprinta-hero-title {
    max-width: 300px;
    font-size: 24px;
  }
  .sprinta-hero-header-row {
    padding-left: 24px;
    padding-right: 24px;
  }
  .sprinta-hero-avatar-wrapper {
    left: 24px;
  }
  .sprinta-back-btn {
    left: 24px;
  }
  .sprinta-cover-actions {
    right: 24px;
  }
  .sprinta-hero-content-container {
    flex-direction: column;
    align-items: flex-start;
    padding: 60px 0 16px 0; /* Push below avatar in mobile */
  }
  .sprinta-hero-actions {
    margin-top: 16px;
    width: 100%;
    justify-content: flex-end;
  }
}
</style>
