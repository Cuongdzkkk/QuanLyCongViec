<script setup>
import { Moon, Sun } from 'lucide-vue-next'
import SprintaBrand from '@/components/branding/SprintaBrand.vue'
import { currentTheme, toggleTheme } from '@/utils/theme'

defineProps({
  eyebrow: {
    type: String,
    default: 'SPRINTA'
  },
  title: {
    type: String,
    required: true
  },
  intro: {
    type: String,
    required: true
  }
})
</script>

<template>
  <div class="public-page">
    <header class="public-header">
      <div class="public-header__inner">
        <router-link to="/" class="public-brand" aria-label="SprintA home">
          <SprintaBrand size="compact" />
        </router-link>

        <nav class="public-nav" aria-label="Điều hướng SprintA">
          <router-link to="/about">Giới thiệu</router-link>
          <router-link to="/privacy">Chính sách riêng tư</router-link>
          <router-link to="/terms">Điều khoản</router-link>
        </nav>

        <div class="public-header__actions">
          <button
            type="button"
            class="theme-toggle"
            :aria-label="currentTheme === 'dark' ? 'Chuyển sang giao diện sáng' : 'Chuyển sang giao diện tối'"
            @click="toggleTheme()"
          >
            <Sun v-if="currentTheme === 'dark'" :size="17" aria-hidden="true" />
            <Moon v-else :size="17" aria-hidden="true" />
          </button>
          <router-link to="/login" class="public-login-link">Đăng nhập</router-link>
        </div>
      </div>
    </header>

    <main class="public-main">
      <div class="public-content">
        <header class="public-heading">
          <p class="public-eyebrow">{{ eyebrow }}</p>
          <h1>{{ title }}</h1>
          <p class="public-intro">{{ intro }}</p>
        </header>

        <article class="public-card">
          <slot />
        </article>
      </div>
    </main>

    <footer class="public-footer">
      <div class="public-footer__inner">
        <div class="public-footer__brand">
          <router-link to="/" class="public-brand" aria-label="SprintA home">
            <SprintaBrand size="compact" />
          </router-link>
          <p>Không gian làm việc tập trung cho đội nhóm và công việc rõ ràng hơn.</p>
        </div>
        <nav class="public-footer__links" aria-label="Liên kết SprintA">
          <router-link to="/about">About / Giới thiệu</router-link>
          <router-link to="/privacy">Privacy Policy / Chính sách riêng tư</router-link>
          <router-link to="/terms">Terms of Service / Điều khoản</router-link>
          <router-link to="/login">Login / Đăng nhập</router-link>
        </nav>
        <p class="public-footer__copyright">© 2026 SprintA</p>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.public-page {
  min-height: 100vh;
  display: flex;
  flex-direction: column;
  color: var(--sp-text);
  background:
    radial-gradient(circle at 14% 0%, color-mix(in srgb, var(--sp-primary) 9%, transparent), transparent 35%),
    var(--sp-bg);
}

.public-header {
  position: sticky;
  top: 0;
  z-index: var(--sp-z-sticky);
  border-bottom: 1px solid var(--sp-border);
  background: color-mix(in srgb, var(--sp-bg) 92%, transparent);
  backdrop-filter: blur(14px);
}

.public-header__inner,
.public-content,
.public-footer__inner {
  width: min(100% - 32px, 1120px);
  margin-inline: auto;
}

.public-header__inner {
  min-height: 68px;
  display: flex;
  align-items: center;
  gap: var(--sp-space-6);
}

.public-brand {
  color: var(--sp-text);
  text-decoration: none;
}

.public-nav {
  display: flex;
  align-items: center;
  gap: var(--sp-space-5);
  margin-left: auto;
}

.public-nav a,
.public-login-link,
.public-footer a {
  color: var(--sp-text-muted);
  font-size: 12px;
  font-weight: 700;
  text-decoration: none;
}

.public-nav a:hover,
.public-nav a.router-link-active,
.public-login-link:hover,
.public-footer a:hover {
  color: var(--sp-primary);
}

.public-header__actions {
  display: flex;
  align-items: center;
  gap: var(--sp-space-3);
}

.theme-toggle {
  width: 36px;
  height: 36px;
  display: inline-grid;
  place-items: center;
  border: 1px solid var(--sp-border);
  border-radius: var(--sp-radius-sm);
  color: var(--sp-text);
  background: var(--sp-surface);
  cursor: pointer;
}

.theme-toggle:hover {
  border-color: var(--sp-primary);
  color: var(--sp-primary);
}

.theme-toggle:focus-visible,
.public-nav a:focus-visible,
.public-login-link:focus-visible,
.public-footer a:focus-visible {
  outline: 3px solid color-mix(in srgb, var(--sp-primary) 35%, transparent);
  outline-offset: 3px;
}

.public-login-link {
  padding: 9px 14px;
  border: 1px solid var(--sp-border-strong);
  border-radius: var(--sp-radius-sm);
  color: var(--sp-primary);
  background: var(--sp-surface);
}

.public-main {
  flex: 1 0 auto;
  padding: clamp(48px, 8vw, 88px) 0 72px;
}

.public-heading {
  max-width: 760px;
  margin-bottom: 32px;
}

.public-eyebrow {
  margin: 0 0 10px;
  color: var(--sp-primary);
  font-size: 11px;
  font-weight: 850;
  letter-spacing: .14em;
}

.public-heading h1 {
  margin: 0;
  color: var(--sp-text);
  font-family: var(--sp-font-display);
  font-size: clamp(34px, 5vw, 58px);
  line-height: 1.08;
  letter-spacing: -.045em;
}

.public-intro {
  max-width: 680px;
  margin: 18px 0 0;
  color: var(--sp-text-muted);
  font-size: 16px;
  line-height: 1.7;
}

.public-card {
  padding: clamp(22px, 4vw, 44px);
  border: 1px solid var(--sp-border);
  border-radius: var(--sp-radius-lg);
  background: var(--sp-surface);
  box-shadow: var(--sp-shadow-sm);
}

.public-footer {
  border-top: 1px solid var(--sp-border);
  background: var(--sp-surface-raised);
}

.public-footer__inner {
  padding: 28px 0 22px;
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1.4fr);
  gap: 24px 48px;
}

.public-footer__brand p {
  max-width: 330px;
  margin: 12px 0 0;
  color: var(--sp-text-muted);
  font-size: 12px;
  line-height: 1.6;
}

.public-footer__links {
  display: flex;
  flex-wrap: wrap;
  align-content: start;
  justify-content: flex-end;
  gap: 10px 20px;
}

.public-footer__copyright {
  grid-column: 1 / -1;
  margin: 0;
  padding-top: 16px;
  border-top: 1px solid var(--sp-border);
  color: var(--sp-text-muted);
  font-size: 11px;
}

@media (max-width: 760px) {
  .public-header__inner {
    min-height: 60px;
    flex-wrap: wrap;
    gap: 12px 16px;
    padding: 10px 0;
  }

  .public-nav {
    order: 3;
    width: 100%;
    justify-content: space-between;
    gap: 10px;
    padding-top: 4px;
    overflow-x: auto;
  }

  .public-nav a {
    flex: 0 0 auto;
    font-size: 11px;
  }

  .public-header__actions {
    margin-left: auto;
  }

  .public-main {
    padding: 42px 0 52px;
  }

  .public-footer__inner {
    grid-template-columns: 1fr;
    gap: 20px;
  }

  .public-footer__links {
    justify-content: flex-start;
    gap: 12px 18px;
  }

  .public-footer__copyright {
    grid-column: auto;
  }
}

@media (max-width: 420px) {
  .public-header__inner,
  .public-content,
  .public-footer__inner {
    width: min(100% - 24px, 1120px);
  }

  .public-heading h1 {
    font-size: clamp(32px, 11vw, 44px);
  }

  .public-intro {
    font-size: 14px;
  }

  .public-card {
    padding: 20px 16px;
    border-radius: var(--sp-radius-md);
  }
}
</style>
