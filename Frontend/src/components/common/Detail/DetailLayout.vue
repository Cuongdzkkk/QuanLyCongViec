<template>
  <div class="sprinta-detail-page">
    <slot name="hero"></slot>
    
    <div class="sprinta-detail-tabs-wrapper">
      <div class="sprinta-detail-tabs-inner">
        <slot name="tabs"></slot>
      </div>
    </div>

    <div class="sprinta-detail-content-wrapper">
      <div class="sprinta-detail-main">
        <slot name="main"></slot>
      </div>
      <div class="sprinta-detail-sidebar" v-if="$slots.sidebar">
        <slot name="sidebar"></slot>
      </div>
    </div>
  </div>
</template>

<script setup>
// Shared Layout for Goal, Team, User Profile
</script>

<style>
/* Global styles for detail pages to reuse */
.sprinta-detail-page {
  display: flex;
  flex-direction: column;
  min-height: 100%;
  background-color: #FAFBFC;
}

.sprinta-detail-tabs-wrapper {
  background-color: #FFFFFF;
  border-bottom: 1px solid #DFE1E6;
  position: sticky;
  top: 0;
  z-index: 10;
}

.sprinta-detail-tabs-inner {
  display: flex;
  gap: 24px;
  padding: 0 40px;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
  box-sizing: border-box;
}

.sprinta-detail-content-wrapper {
  display: flex;
  gap: 32px;
  padding: 32px 40px;
  max-width: 1400px;
  margin: 0 auto;
  width: 100%;
  box-sizing: border-box;
}

.sprinta-detail-main {
  flex: 1;
  min-width: 0;
}

.sprinta-detail-sidebar {
  width: 320px;
  flex-shrink: 0;
}

/* Common Tabs Styling */
.sprinta-tab-btn {
  background: none;
  border: none;
  padding: 12px 0;
  font-size: 14px;
  font-weight: 500;
  color: #5E6C84;
  cursor: pointer;
  border-bottom: 3px solid transparent;
  transition: all 0.2s ease;
  position: relative;
  display: flex;
  align-items: center;
  gap: 8px;
}

.sprinta-tab-btn:hover {
  color: #172B4D;
}

.sprinta-tab-btn.active {
  color: #0052CC;
  border-bottom-color: #0052CC;
}

/* Responsive */
@media (max-width: 1024px) {
  .sprinta-detail-content-wrapper,
  .sprinta-detail-tabs-inner {
    padding: 24px;
  }
  .sprinta-detail-content-wrapper {
    flex-direction: column;
  }
  .sprinta-detail-sidebar {
    width: 100%;
  }
}
@media (max-width: 768px) {
  .sprinta-detail-content-wrapper,
  .sprinta-detail-tabs-inner {
    padding: 16px;
  }
  .sprinta-detail-tabs-inner {
    overflow-x: auto;
    white-space: nowrap;
  }
}
</style>
