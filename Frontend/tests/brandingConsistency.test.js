import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')

const brand = read('src/components/branding/SprintaBrand.vue')
const home = read('src/views/Home.vue')
const login = read('src/views/Login.vue')
const register = read('src/views/Register.vue')
const topbar = read('src/components/layout/AppTopBar.vue')
const siteSelection = read('src/views/SiteSelection.vue')
const forgotPassword = read('src/views/ForgotPassword.vue')
const acceptInvite = read('src/views/AcceptInvite.vue')
const billingCheckout = read('src/views/BillingCheckout.vue')
const homeSiteLayout = read('src/views/HomeSite/HomeSiteLayout.vue')
const googleService = read('src/services/googleIdentityService.js')

test('all top-level brand blocks use the site-selection branding component', () => {
  assert.match(brand, /import logoUrl from ['"]@\/assets\/logo_QLCV\.png['"]/)
  assert.match(brand, /alt="SprintA logo"/)
  assert.match(brand, /sprinta-brand--site-selection/)
  assert.match(brand, /sprinta-brand--compact/)

  for (const source of [home, login, register, topbar, siteSelection, forgotPassword, acceptInvite, billingCheckout, homeSiteLayout]) {
    assert.match(source, /<SprintaBrand\b/)
    assert.doesNotMatch(source, /sprinta-mark-(light|dark)\.png|logo_QLCV\.png/)
  }
})

test('canonical wordmark stays crisp and billing branding has collision-safe layout', () => {
  assert.match(brand, /\.sprinta-brand__name[\s\S]*?opacity: 1;/)
  assert.match(brand, /\.sprinta-brand__name[\s\S]*?filter: none;/)
  assert.match(brand, /-webkit-font-smoothing: antialiased;/)
  assert.match(brand, /text-rendering: geometricPrecision;/)
  assert.match(billingCheckout, /\.checkout-page \.brand \{[\s\S]*?display: flex;[\s\S]*?gap: 12px;[\s\S]*?min-width: 0;/)
  assert.match(billingCheckout, /@media \(max-width: 520px\)[\s\S]*?\.checkout-page \.checkout-nav \{[\s\S]*?height: auto;[\s\S]*?flex-wrap: wrap;/)
  assert.match(billingCheckout, /\.checkout-page \.back-button \{[\s\S]*?min-width: 0;/)
})

test('social buttons have exact labels, ARIA names, and shared dimensions', () => {
  assert.match(login, /class="social-btn google-btn"[\s\S]*?Google[\s\S]*?<\/el-button>/)
  assert.match(login, /class="social-btn github-btn"[\s\S]*?GitHub[\s\S]*?<\/el-button>/)
  assert.equal(login.match(/aria-label="Đăng nhập bằng Google"/g)?.length, 1)
  assert.equal(login.match(/aria-label="Đăng nhập bằng GitHub"/g)?.length, 1)
  assert.match(login, /\.social-btn \{[\s\S]*?height: 44px;[\s\S]*?min-height: 44px;[\s\S]*?border-radius: 10px !important;/)
  assert.match(login, /\.social-login \{[\s\S]*?gap: 12px;/)
  assert.match(login, /\.social-login \{[\s\S]*?grid-template-columns: repeat\(2, minmax\(0, 1fr\)\);/)
  assert.match(login, /@media \(max-width: 640px\)[\s\S]*?\.social-login \{[\s\S]*?grid-template-columns: 1fr;/)
})

test('Google uses the registered callback path and GitHub OAuth handler remains intact', () => {
  assert.match(login, /renderGoogleIdentityButton/)
  assert.match(login, /callback: handleGoogleLogin/)
  assert.match(login, /loginWithGoogleCredential\(credential/)
  assert.match(login, /const handleGitHubLogin = \(\) => \{/)
  assert.match(login, /https:\/\/github\.com\/login\/oauth\/authorize\?client_id=\$\{clientId\}/)
  assert.doesNotMatch(login, /promptGoogleIdentity/)
  assert.match(googleService, /renderButton/)
  assert.doesNotMatch(googleService, /api\.prompt\(\)/)
  assert.doesNotMatch(googleService, /console\.(log|error|warn).*key|apiKey/i)
})

test('auth layouts protect narrow viewports from horizontal overflow', () => {
  for (const source of [login, register]) {
    assert.match(source, /\.auth-page \{[\s\S]*?overflow-x: hidden;/)
  }
  assert.match(brand, /\.sprinta-brand--compact[\s\S]*?height: 24px;/)
  assert.match(brand, /@media \(max-width: 680px\)[\s\S]*?\.sprinta-brand--compact[\s\S]*?height: 20px;/)
  assert.match(login, /@media \(max-width: 640px\)[\s\S]*?\.social-login \{[\s\S]*?flex-direction: column;/)
  assert.match(home, /\.landing-page \{[\s\S]*?overflow-x: (hidden|clip);/)
})
