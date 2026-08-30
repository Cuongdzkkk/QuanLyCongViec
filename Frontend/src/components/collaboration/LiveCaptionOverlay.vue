<template>
  <Transition name="caption-dock">
    <section
      v-if="enabled && visibleCaptions.length"
      class="call-live-caption-dock"
      role="log"
      aria-label="Phụ đề trực tiếp"
      aria-live="polite"
      aria-relevant="additions text"
    >
      <article
        v-for="(caption, index) in visibleCaptions"
        :key="caption.id"
        class="call-live-caption-row"
        :class="{ 'is-interim': caption.isInterim, 'is-latest': index === 0 }"
        :aria-atomic="caption.isInterim ? 'false' : 'true'"
      >
        <img
          v-if="caption.avatarUrl"
          class="call-live-caption-avatar"
          :src="caption.avatarUrl"
          :alt="`${caption.speakerDisplayName} avatar`"
        />
        <span v-else class="call-live-caption-avatar is-fallback" aria-hidden="true">
          {{ caption.speakerDisplayName?.charAt(0) || '?' }}
        </span>
        <p class="call-live-caption-copy">
          <strong>{{ caption.speakerDisplayName }}</strong>
          <span>{{ caption.text }}</span>
        </p>
      </article>
    </section>
  </Transition>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  enabled: { type: Boolean, default: false },
  captions: { type: Array, default: () => [] }
})

const visibleCaptions = computed(() => props.captions.slice(-3).reverse())
</script>

<style scoped>
.call-live-caption-dock {
  position: absolute;
  z-index: 3;
  left: 50%;
  bottom: clamp(48px, 8%, 72px);
  display: grid;
  width: min(760px, 70vw, calc(100% - 28px));
  max-height: 216px;
  gap: 5px;
  overflow: hidden;
  transform: translateX(-50%);
  pointer-events: none;
}

.call-live-caption-row {
  display: grid;
  grid-template-columns: 32px minmax(0, 1fr);
  align-items: start;
  gap: 10px;
  min-width: 0;
  padding: 7px 10px 8px;
  border: 1px solid rgba(226, 232, 240, .18);
  border-radius: 10px;
  background: rgba(5, 12, 22, .92);
  box-shadow: 0 12px 30px rgba(1, 7, 16, .26), inset 0 1px rgba(255, 255, 255, .04);
  color: #f8fafc;
  backdrop-filter: blur(14px) saturate(115%);
}

.call-live-caption-row.is-interim {
  border-color: rgba(154, 240, 197, .28);
}

.call-live-caption-row:not(.is-latest) {
  opacity: .72;
}

.call-live-caption-avatar {
  display: block;
  width: 32px;
  height: 32px;
  border-radius: 9px;
  object-fit: cover;
}

.call-live-caption-avatar.is-fallback {
  display: grid;
  place-items: center;
  background: #173247;
  color: #d9f7e8;
  font-size: 12px;
  font-weight: 700;
}

.call-live-caption-copy {
  display: grid;
  min-width: 0;
  gap: 2px;
  margin: 0;
}

.call-live-caption-copy strong {
  overflow: hidden;
  color: #9af0c5;
  font-size: 11px;
  font-weight: 700;
  letter-spacing: .01em;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.call-live-caption-copy span {
  display: -webkit-box;
  min-width: 0;
  overflow: hidden;
  color: #f8fafc;
  font-size: 14px;
  line-height: 1.42;
  overflow-wrap: anywhere;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}

.call-live-caption-row:not(.is-latest) .call-live-caption-copy span {
  -webkit-line-clamp: 2;
}

.caption-dock-enter-active,
.caption-dock-leave-active {
  transition: opacity 180ms ease, transform 180ms ease;
}

.caption-dock-enter-from,
.caption-dock-leave-to {
  opacity: 0;
  transform: translate(-50%, 8px);
}

@media (max-width: 560px) {
  .call-live-caption-dock {
    bottom: 42px;
    width: calc(100% - 16px);
    max-height: 180px;
  }

  .call-live-caption-row {
    grid-template-columns: 28px minmax(0, 1fr);
    padding: 8px 9px;
  }

  .call-live-caption-avatar {
    width: 28px;
    height: 28px;
  }

  .call-live-caption-row:nth-child(n + 3) {
    display: none;
  }
}
</style>
