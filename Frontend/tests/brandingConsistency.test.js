import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')

const home = read('src/views/Home.vue')
const login = read('src/views/Login.vue')
const register = read('src/views/Register.vue')
const topbar = read('src/components/layout/AppTopBar.vue')
const googleService = read('src/services/googleIdentityService.js')

test('auth and app chrome reuse the landing logo assets', () => {
  assert.match(home, /<img class="brand-logo" src="\/sprinta-mark-light\.png"/)

  for (const source of [login, register]) {
    assert.match(source, /class="custom-logo"><\/span>/)
    assert.match(source, /background: center \/ contain no-repeat url\('\/sprinta-mark-light\.png'\)/)
    assert.match(source, /background-image: url\('\/sprinta-mark-dark\.png'\)/)
  }

  assert.match(topbar, /class="sprinta-logo-mark"><\/span>/)
  assert.match(topbar, /background: center \/ contain no-repeat url\('\/sprinta-mark-light\.png'\)/)
  assert.match(topbar, /background-image: url\('\/sprinta-mark-dark\.png'\)/)
})

test('social buttons have exact labels, ARIA names, and shared dimensions', () => {
  assert.match(login, /class="social-btn google-btn"[\s\S]*?Google[\s\S]*?<\/el-button>/)
  assert.match(login, /class="social-btn github-btn"[\s\S]*?GitHub[\s\S]*?<\/el-button>/)
  assert.equal(login.match(/aria-label="Đăng nhập bằng Google"/g)?.length, 1)
  assert.equal(login.match(/aria-label="Đăng nhập bằng GitHub"/g)?.length, 1)
  assert.match(login, /\.social-btn \{[\s\S]*?height: 44px;[\s\S]*?min-height: 44px;[\s\S]*?border-radius: 10px !important;/)
  assert.match(login, /\.social-login \{[\s\S]*?gap: 12px;/)
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
    assert.match(source, /\.custom-logo \{[\s\S]*?background: center \/ contain/)
  }
  assert.match(login, /@media \(max-width: 640px\)[\s\S]*?\.social-login \{[\s\S]*?flex-direction: column;/)
  assert.match(home, /\.landing-page \{[\s\S]*?overflow-x: (hidden|clip);/)
})
