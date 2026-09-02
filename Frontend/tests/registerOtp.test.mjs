import assert from 'node:assert/strict'
import fs from 'node:fs'
import path from 'node:path'
import test from 'node:test'

const frontendRoot = path.resolve(import.meta.dirname, '..')
const registerSource = fs.readFileSync(path.join(frontendRoot, 'src/views/Register.vue'), 'utf8')

test('registration OTP send has an in-flight guard, cooldown, and Retry-After handling', () => {
  assert.match(registerSource, /let sendOtpInFlight = false/)
  assert.match(registerSource, /const hasActiveResendCooldown = \(\) => resendCooldown\.value > 0/)
  assert.match(registerSource, /if \(sendOtpInFlight \|\| hasActiveResendCooldown\(\)\) return/)
  assert.match(registerSource, /resendCooldownEmail\.value === normalizeEmail\(form\.email\)/)
  assert.match(registerSource, /sendOtpInFlight = true/)
  assert.match(registerSource, /response\.data\?\.resendCooldownSeconds/)
  assert.match(registerSource, /error\.response\?\.status === 429/)
  assert.match(registerSource, /Retry-After/i)
  assert.match(registerSource, /resendCountdown/)
  assert.match(registerSource, /@click\.prevent="handleSendOtp"/)
  assert.equal((registerSource.match(/axiosClient\.post\('\/auth\/send-otp'/g) || []).length, 1)
})

test('registration OTP send does not advance on an existing account or provider failure', () => {
  const handlerSource = registerSource.slice(registerSource.indexOf('const handleSendOtp'))

  assert.match(registerSource, /const registrationConflict = ref\(false\)/)
  assert.match(registerSource, /v-if="registrationConflict"/)
  assert.match(registerSource, /<router-link to="\/login">/)
  assert.match(handlerSource, /error\.response\?\.status === 409/)
  assert.match(handlerSource, /auth\.register\.messages\.emailAlreadyUsed/)
  assert.match(handlerSource, /error\.response\?\.status === 503/)
  assert.match(handlerSource, /auth\.register\.messages\.sendOtpUnavailable/)
  assert.match(handlerSource, /registrationConflict\.value = true/)
})
