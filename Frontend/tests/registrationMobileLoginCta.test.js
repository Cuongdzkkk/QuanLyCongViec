import test from 'node:test'
import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const read = relativePath => fs.readFileSync(path.join(frontendRoot, relativePath), 'utf8')
const register = read('src/views/Register.vue')
const login = read('src/views/Login.vue')
const home = read('src/views/Home.vue')

test('duplicate registration resets the wizard to a clean email step', () => {
  const sendOtpHandler = register.slice(register.indexOf('const handleSendOtp'))

  assert.match(register, /const resetVerificationState = \(\) => \{/)
  assert.match(register, /otpToken\.value = ''/)
  assert.match(register, /resendCooldown\.value = 0/)
  assert.match(sendOtpHandler, /error\.response\?\.status === 409[\s\S]*?resetVerificationState\(\)/)
  assert.match(sendOtpHandler, /auth\.register\.messages\.emailAlreadyUsed/)
  assert.match(register, /@click\.prevent="resetVerificationState"/)
})

test('registration email changes invalidate OTP verification', () => {
  assert.match(register, /watch\(\(\) => form\.email/)
  assert.match(register, /if \(previousEmail && normalizeEmail\(nextEmail\) !== normalizeEmail\(previousEmail\)\)/)
  assert.match(register, /resetVerificationState\(\)/)
})

test('Step 3 registers exactly once and preserves full name semantics', () => {
  const registerHandler = register.slice(register.indexOf('const handleRegister'))
  const verifyHandler = register.slice(register.indexOf('const handleVerifyOtp'))
  assert.equal((registerHandler.match(/axiosClient\.post\('\/auth\/send-otp'/g) || []).length, 0)
  assert.equal((registerHandler.match(/axiosClient\.post\('\/auth\/register'/g) || []).length, 1)
  assert.match(registerHandler, /fullName: form\.fullName/)
  assert.match(registerHandler, /otpToken: otpToken\.value/)
  assert.doesNotMatch(registerHandler, /otpCode:/)
  assert.match(registerHandler, /if \(!otpToken\.value\)/)
  assert.match(verifyHandler, /response\.data\?\.otpToken/)
  assert.match(verifyHandler, /verificationTokenMissing/)
})

test('public navigation exposes the right auth CTAs without a self-login link', () => {
  const mobileNav = home.slice(home.indexOf('<nav v-if="mobileOpen"'))
  const loginHeader = login.slice(0, login.indexOf('<main'))

  assert.match(mobileNav, /<router-link[^>]+to="\/login"/)
  assert.match(mobileNav, /\/register/)
  assert.match(home, /:aria-expanded="mobileOpen"/)
  assert.match(home, /aria-controls="landing-mobile-nav"/)
  assert.match(mobileNav, /@click\.prevent="go\('\/login'\)"/)
  assert.doesNotMatch(loginHeader, /<router-link[^>]+to="\/login"/)
  assert.match(register, /<router-link[^>]+to="\/login"/)
  assert.match(register, /\/forgot-password/)
  assert.match(home, /mobile-menu[\s\S]*?min-height:\s*44px/)
})
