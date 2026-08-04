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

const setSpotlight = (event) => {
  const target = event.currentTarget
  const rect = target.getBoundingClientRect()
  const x = event.clientX - rect.left
  const y = event.clientY - rect.top
  const centerX = rect.width / 2
  const centerY = rect.height / 2
  const tiltX = ((x - centerX) / centerX) * 6
  const tiltY = -((y - centerY) / centerY) * 6
  target.style.setProperty('--spot-x', `${x}px`)
  target.style.setProperty('--spot-y', `${y}px`)
  target.style.setProperty('--tilt-x', `${tiltX.toFixed(2)}deg`)
  target.style.setProperty('--tilt-y', `${tiltY.toFixed(2)}deg`)
}

const resetSpotlight = (event) => {
  const target = event.currentTarget
  target.style.setProperty('--tilt-x', '0deg')
  target.style.setProperty('--tilt-y', '0deg')
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

    <div class="video-shell spotlight-card" @pointermove="setSpotlight" @pointerleave="resetSpotlight">
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
  width: min(1100px, 100%);
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
  color: var(--muted);
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
  width: min(1040px, 100%);
  margin: 42px auto 0;
  overflow: hidden;
  border: 1px solid color-mix(in srgb, var(--accent) 30%, var(--line));
  border-radius: 26px;
  background: var(--navy);
  box-shadow: 0 32px 85px rgba(4, 28, 46, .28);
  transform: perspective(1200px) rotateX(calc(2deg + var(--tilt-y, 0deg))) rotateY(calc(-3deg + var(--tilt-x, 0deg))) translateZ(12px);
  transform-style: preserve-3d;
  transition: transform .28s cubic-bezier(.16,1,.3,1), box-shadow .25s ease, border-color .25s ease;
}

.video-shell:hover {
  transform: perspective(1200px) rotateX(calc(2deg + var(--tilt-y, 0deg))) rotateY(calc(-3deg + var(--tilt-x, 0deg))) translateZ(26px);
  border-color: color-mix(in srgb, var(--accent) 60%, var(--line));
  box-shadow: 0 42px 110px rgba(4, 28, 46, .38), 0 0 30px color-mix(in srgb, var(--accent) 20%, transparent);
}

.video-shell::before { content: ''; position: absolute; inset: 0; z-index: 1; pointer-events: none; border: 1px solid rgba(255,255,255,.18); border-radius: inherit; }
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
  padding: 15px 22px;
  transform: translate(-50%, -50%) translateZ(35px);
  color: white;
  border: 1px solid rgba(255, 255, 255, .4);
  border-radius: 999px;
  background: #008fb8ee;
  box-shadow: 0 20px 42px rgba(0,143,184,.4);
  cursor: pointer;
  font-weight: 850;
  transition: transform .2s ease, background .2s ease, box-shadow .2s ease;
}

.video-play:hover {
  transform: translate(-50%, -50%) translateZ(45px) scale(1.06);
  background: #009ecb;
  box-shadow: 0 26px 54px rgba(0,143,184,.5);
}

.video-play:focus-visible,
.video-fallback:focus-visible {
  outline: 3px solid #66d9ef;
  outline-offset: 4px;
}

.video-frame {
  border: 0;
}

.video-fallback {
  display: block;
  padding: 13px 16px;
  color: #c4dbe5;
  background: var(--navy);
  font-size: 12px;
  text-decoration: underline;
}

.video-fallback:hover {
  color: white;
}

@media (prefers-reduced-motion: reduce) {
  .video-shell { transform: none !important; perspective: none !important; }
  .video-play { transform: translate(-50%, -50%) !important; }
}

@media (hover: none) and (prefers-reduced-motion: no-preference) {
  .video-shell { --tilt-x: 0deg; --tilt-y: 0deg; }
  .video-play { transform: translate(-50%, -50%) translateZ(35px); }
}

@media (max-width: 900px) {
  .product-video-section {
    width: 100%;
  }
}

@media (max-width: 640px) {
  .video-copy h2 { font-size: clamp(34px, 11vw, 48px); }
  .video-shell { margin-top: 30px; border-radius: 17px; }
}
</style>
