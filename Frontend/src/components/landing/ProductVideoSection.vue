<script setup>
import { computed, ref } from 'vue'
import { Clapperboard, FileText, Play } from 'lucide-vue-next'
import { language } from '@/i18n'

const props = defineProps({
  title: { type: String, required: true },
  intro: { type: String, required: true },
})

const isLoaded = ref(false)
const languageCode = computed(() => language.value === 'vi' ? 'vi' : 'en')
const videoId = import.meta.env.VITE_SPRINTA_YOUTUBE_VIDEO_ID || 'axxBkMdI-0o'
const embedUrl = computed(() => `https://www.youtube-nocookie.com/embed/${videoId}?autoplay=1&playsinline=1&rel=0&hl=${languageCode.value}&cc_lang_pref=${languageCode.value}&cc_load_policy=1`)
const youtubeUrl = computed(() => `https://youtu.be/${videoId}`)
const copy = computed(() => languageCode.value === 'vi'
  ? {
      transcript: 'Transcript sẵn sàng',
      summary: 'Công việc rải rác → workspace tập trung → AI hỗ trợ có xác nhận → báo cáo rõ ràng.',
      play: 'Phát video',
      fallback: 'Xem trên YouTube'
    }
  : {
      transcript: 'Transcript ready',
      summary: 'Scattered work → one workspace → confirmed AI support → clear reporting.',
      play: 'Play video',
      fallback: 'Watch on YouTube'
    })

const loadVideo = () => { isLoaded.value = true }

const setTilt = (event) => {
  const target = event.currentTarget
  const rect = target.getBoundingClientRect()
  target.style.setProperty('--tilt-x', `${((((event.clientX - rect.left) / rect.width) - 0.5) * 6).toFixed(2)}deg`)
  target.style.setProperty('--tilt-y', `${((((event.clientY - rect.top) / rect.height) - 0.5) * -6).toFixed(2)}deg`)
}

const resetTilt = (event) => {
  event.currentTarget.style.setProperty('--tilt-x', '0deg')
  event.currentTarget.style.setProperty('--tilt-y', '0deg')
}
</script>

<template>
  <section class="product-video-section" aria-labelledby="product-video-title">
    <div class="video-copy">
      <div class="video-eyebrow"><Clapperboard :size="15" aria-hidden="true" /> {{ languageCode === 'vi' ? 'SẢN PHẨM THỰC TẾ' : 'REAL PRODUCT' }}</div>
      <h2 id="product-video-title"><slot name="title">{{ props.title }}</slot></h2>
      <p>{{ props.intro }}</p>
      <div class="transcript-card">
        <FileText :size="17" aria-hidden="true" />
        <div>
          <b>{{ copy.transcript }}</b>
          <span>{{ copy.summary }}</span>
        </div>
      </div>
    </div>

    <div class="video-shell" @pointermove="setTilt" @pointerleave="resetTilt">
      <div v-if="!isLoaded" class="video-poster" role="img" :aria-label="props.title">
        <img src="/videos/sprinta-product-demo-poster.webp" :alt="props.title" />
        <button class="video-play" type="button" @click="loadVideo" :aria-label="copy.play">
          <Play :size="22" fill="currentColor" aria-hidden="true" />
          <span>{{ copy.play }}</span>
        </button>
      </div>
      <iframe
        v-else
        class="video-frame"
        :src="embedUrl"
        :title="props.title"
        allow="autoplay; encrypted-media; picture-in-picture"
        allowfullscreen
      />
      <a class="video-fallback" :href="youtubeUrl" target="_blank" rel="noopener noreferrer">{{ copy.fallback }} ↗</a>
    </div>
  </section>
</template>

<style scoped>
.product-video-section {
  display: block;
  width: min(1240px, 100%);
  margin-inline: auto;
}

.video-copy { max-width: 760px; margin: 0 auto; text-align: center; }
.video-copy .eyebrow { justify-content: center; }
.video-eyebrow { display: inline-flex; align-items: center; gap: 8px; margin-bottom: 14px; color: var(--accent); font-size: 12px; font-weight: 900; letter-spacing: .16em; text-transform: uppercase; }
.video-eyebrow svg { box-sizing: content-box; padding: 5px; border: 1px solid color-mix(in srgb, currentColor 24%, transparent); border-radius: 9px; background: color-mix(in srgb, currentColor 10%, transparent); }

.video-copy h2 {
  margin: 0 0 12px;
  font-size: clamp(36px, 4vw, 53px);
  line-height: 1.05;
  letter-spacing: -.045em;
}

.video-copy > p {
  max-width: 680px;
  margin: 0 auto;
  color: var(--ink-secondary, var(--muted));
  line-height: 1.7;
}

.transcript-card {
  max-width: 680px;
  margin: 26px auto 0;
  text-align: left;
  display: flex;
  gap: 12px;
  margin-top: 30px;
  padding: 16px;
  border: 1px solid var(--line);
  border-radius: 16px;
  background: var(--surface-2);
}

.transcript-card svg {
  flex: 0 0 auto;
  color: var(--accent);
}

.transcript-card div {
  display: grid;
  gap: 5px;
}

.transcript-card span {
  color: var(--muted);
  font-size: 12px;
  line-height: 1.5;
}

.video-shell {
  position: relative;
  width: min(1180px, 100%);
  margin: 42px auto 0;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, #41C0F2 34%, var(--line));
  border-radius: 22px;
  background: var(--navy);
  box-shadow: inset 0 1px rgba(255,255,255,.12), 0 4px 10px rgba(4,17,31,.08), 0 18px 40px rgba(4,17,31,.14), 0 34px 90px rgba(4,17,31,.22);
  transform: perspective(1200px) rotateX(calc(2deg + var(--tilt-y, 0deg))) rotateY(calc(-2deg + var(--tilt-x, 0deg))) translateZ(10px);
  transform-style: preserve-3d;
  transition: transform .3s cubic-bezier(.16,1,.3,1), border-color .2s ease, box-shadow .2s ease;
}

.video-shell:hover { border-color: #41C0F2; box-shadow: inset 0 1px rgba(255,255,255,.16), 0 6px 14px rgba(4,17,31,.10), 0 22px 48px rgba(4,17,31,.18), 0 42px 100px rgba(4,17,31,.28); }
.video-shell::before { content: ''; position: absolute; inset: 0; z-index: 1; pointer-events: none; border: 1px solid rgba(255,255,255,.16); border-radius: inherit; background: linear-gradient(128deg, color-mix(in srgb, #41C0F2 15%, transparent), transparent 24% 78%, color-mix(in srgb, #0B4FD9 9%, transparent)); }
.video-poster,
.video-frame {
  display: block;
  width: 100%;
  aspect-ratio: 16 / 9;
}

.video-poster {
  position: relative;
}

.video-poster img {
  display: block;
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.video-play {
  position: absolute;
  left: 50%;
  top: 50%;
  display: inline-flex;
  align-items: center;
  gap: 9px;
  padding: 15px 20px;
  transform: translate(-50%, -50%);
  color: #FFFFFF;
  border: 1px solid color-mix(in srgb, #F4F8FC 42%, transparent);
  border-radius: 999px;
  background: #0B4FD9;
  box-shadow: 0 16px 34px color-mix(in srgb, #0B4FD9 38%, transparent);
  cursor: pointer;
  font-weight: 850;
  transition: transform .2s ease, background-color .2s ease, box-shadow .2s ease;
}

.video-play:hover { transform: translate(-50%, -50%) scale(1.05); background: #08428C; box-shadow: 0 22px 44px color-mix(in srgb, #0B4FD9 46%, transparent); }

.video-play:focus-visible,
.video-fallback:focus-visible {
  outline: 3px solid #41C0F2;
  outline-offset: 4px;
}

.video-frame {
  border: 0;
}

.video-fallback {
  display: block;
  padding: 13px 16px;
  color: #DDE8F2;
  background: var(--navy);
  font-size: 12px;
  text-decoration: underline;
}

.video-fallback:hover {
  color: #FFFFFF;
}

@media (prefers-reduced-motion: reduce) {
  .video-shell { transform: none !important; transition: none !important; }
}

@media (max-width: 900px) {
  .product-video-section {
    width: 100%;
  }
  .video-shell { transform: none; }
}

@media (max-width: 640px) {
  .video-copy h2 { font-size: clamp(34px, 11vw, 48px); }
  .video-shell { margin-top: 30px; border-radius: 17px; }
}
</style>
