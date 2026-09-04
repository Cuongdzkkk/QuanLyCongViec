import test from 'node:test'
import assert from 'node:assert/strict'
import { readFileSync } from 'node:fs'

const sitesForYou = readFileSync(new URL('../src/views/SitesForYou.vue', import.meta.url), 'utf8')
const projectAccess = readFileSync(new URL('../src/config/projectAccess.js', import.meta.url), 'utf8')

test('owner account cards open a drawer and the current account always exists', () => {
  assert.match(sitesForYou, /recentOwnerSites:\s*'Các tài khoản site chủ gần đây'/)
  assert.match(sitesForYou, /v-for="account in ownerAccounts\.slice\(0, 4\)"/)
  assert.match(sitesForYou, /accounts\.set\(String\(currentAccountKey\)/)
  assert.match(sitesForYou, /@click="openOwnerDrawer\(account\)"/)
  assert.match(sitesForYou, /<el-drawer[\s\S]*class="site-children-drawer"/)
})

test('child sites are accessible workspaces grouped by owner, never projects', () => {
  assert.match(sitesForYou, /const ownerId = String\(site\.ownerId/)
  assert.match(sitesForYou, /const childSites = computed\(\(\) => selectedOwnerAccount\.value\?\.sites/)
  assert.match(sitesForYou, /v-for="site in childSites"/)
  assert.match(sitesForYou, /@click="goToSite\(site\)"/)
  assert.doesNotMatch(sitesForYou, /axiosClient\.get\('\/projects'\)/)
  assert.doesNotMatch(sitesForYou, /goToChildSite/)
})

test('project restrictions are not changed to simulate child-site access', () => {
  assert.match(projectAccess, /configuredValue == null\s*\? false/)
})

test('home page requests an owner-account link and lists accessible sites instead of recent projects', () => {
  assert.match(sitesForYou, /requestLink: 'Xin tham gia site chủ'/)
  assert.match(sitesForYou, /axiosClient\.post\('\/site-account-links', \{ email \}\)/)
  assert.match(sitesForYou, /accessibleSites: 'Danh sách các site của bạn'/)
  assert.doesNotMatch(sitesForYou, /Frequently accessed/)
  assert.doesNotMatch(sitesForYou, /recentProjects/)
})
