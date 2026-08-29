<template>
  <section class="rewards-page">
    <header class="page-header app-shell-page-header">
      <div class="title-with-filter">
        <h1>{{ t('rewards.title') }}</h1>
        <div class="sprint-filter-badge">
          <span>Sprint hiện tại</span>
          <i class="fa-solid fa-chevron-down text-xs ml-1"></i>
        </div>
      </div>
      <button class="refresh-btn" type="button" :disabled="loading" @click="loadRewards">
        <i class="fa-solid fa-rotate" :class="{ 'fa-spin': loading }"></i> {{ loading ? t('rewards.refreshing') : t('rewards.refresh') }}
      </button>
    </header>

    <section v-if="seasonDashboard.currentSeason" class="season-v1-panel panel">
      <div class="season-v1-heading">
        <div>
          <span class="season-v1-eyebrow">SprintA Reward System V1</span>
          <h2>{{ seasonDashboard.currentSeason.name }}</h2>
          <p>{{ seasonDashboard.currentSeason.status }} · {{ formatDate(seasonDashboard.currentSeason.startAt) }} — {{ formatDate(seasonDashboard.currentSeason.endAt) }} · {{ seasonTimeRemaining }}</p>
        </div>
        <div class="season-v1-xp"><strong>{{ seasonDashboard.careerXp }}</strong><span>career XP</span></div>
      </div>
      <div class="season-v1-grid">
        <div>
          <h3>Season leaderboard</h3>
          <div v-if="seasonDashboard.leaderboard.length === 0" class="empty-list-small">No finalized events yet.</div>
          <div v-for="entry in seasonDashboard.leaderboard.slice(0, 5)" :key="entry.userId" class="season-v1-row">
            <span>#{{ entry.rank }} {{ entry.userName }}</span><strong>{{ entry.seasonPoints }} pts</strong>
          </div>
        </div>
        <div class="season-v1-stats">
          <h3>My progress</h3>
          <div class="season-v1-stat-grid">
            <span>Rank <strong>#{{ seasonDashboard.myRank || '—' }}</strong></span>
            <span>Season points <strong>{{ seasonDashboard.mySeasonPoints }}</strong></span>
            <span>XP / level <strong>{{ seasonDashboard.careerXp }} / {{ seasonDashboard.careerLevel }}</strong></span>
            <span>On-time rate <strong>{{ Math.round(Number(seasonDashboard.myOnTimeRate || 0)) }}%</strong></span>
          </div>
        </div>
        <div>
          <h3>{{ seasonDashboard.canManage ? 'Pending manager review' : 'Open rewards' }}</h3>
          <div v-if="seasonDashboard.canManage && seasonDashboard.pendingEvents.length" class="season-v1-review-list">
            <div v-for="event in seasonDashboard.pendingEvents" :key="event.id" class="season-v1-row">
              <span>{{ event.userName }} · {{ event.points }} pts</span>
              <span class="season-v1-actions">
                <button type="button" @click="reviewSeasonEvent(event, true)">Approve</button>
                <button type="button" class="reject" @click="reviewSeasonEvent(event, false)">Reject</button>
              </span>
            </div>
          </div>
          <div v-else-if="seasonDashboard.openRewards.length" class="season-v1-review-list">
            <div v-for="grant in seasonDashboard.openRewards" :key="grant.id" class="season-v1-row"><span>{{ grant.rewardName }}</span><strong>{{ grant.status }}</strong></div>
          </div>
          <div v-else class="empty-list-small">Nothing pending.</div>
        </div>
      </div>
      <div v-if="seasonDashboard.rewardProgress.length" class="season-v1-progress">
        <h3>Reward conditions</h3>
        <div v-for="reward in seasonDashboard.rewardProgress" :key="reward.rewardDefinitionId" class="season-v1-progress-row">
          <span>{{ reward.name }} · {{ reward.conditionLabel }}</span>
          <span>{{ reward.currentValue }} / {{ reward.goalValue }}</span>
          <div class="season-v1-progress-track"><div class="season-v1-progress-fill" :style="{ width: `${reward.progressPercent}%` }"></div></div>
        </div>
      </div>
      <div v-if="!seasonDashboard.canManage && seasonDashboard.rewardHistory.length" class="season-v1-history">
        <h3>My rewards</h3>
        <div v-for="grant in seasonDashboard.rewardHistory" :key="grant.id" class="season-v1-row">
          <span>{{ grant.rewardName }} · {{ grant.rewardType }}</span><strong>{{ grant.status }}</strong>
        </div>
      </div>
    </section>

    <section v-if="seasonDashboard.canManage" class="reward-manager-panel panel">
      <div class="manager-panel-heading">
        <div><span class="season-v1-eyebrow">Manager controls</span><h2>Reward operations</h2></div>
        <span class="manager-note">Cash rewards are descriptive only. No wallet or payout is connected.</span>
      </div>
      <div class="manager-columns">
        <div>
          <h3>Seasons</h3>
          <div v-if="managerSeasons.length" class="manager-season-list">
            <div v-for="season in managerSeasons" :key="season.id" class="manager-season-row">
              <div><strong>{{ season.name }}</strong><small>{{ season.type }} · {{ formatDate(season.startAt) }} — {{ formatDate(season.endAt) }}</small></div>
              <div class="manager-row-actions">
                <span class="status-pill">{{ season.status }}</span>
                <button v-if="season.status === 'Draft'" type="button" @click="activateSeason(season)">Activate</button>
                <button v-if="season.status === 'Active'" type="button" class="danger" @click="closeSeason(season)">Close</button>
              </div>
            </div>
          </div>
          <div v-else class="empty-list-small">No seasons yet.</div>
          <form class="manager-form" @submit.prevent="createSeason">
            <h4>Create season</h4>
            <input v-model="seasonForm.name" placeholder="Season name" aria-label="Season name" />
            <select v-model="seasonForm.type" aria-label="Season type">
              <option value="Sprint">Sprint</option><option value="Month">Month</option><option value="EntireProject">Entire Project</option><option value="Custom">Custom</option>
            </select>
            <input v-model="seasonForm.startAt" type="date" aria-label="Season start" />
            <input v-if="seasonForm.type === 'Custom'" v-model="seasonForm.endAt" type="date" aria-label="Season end" />
            <input v-model="seasonForm.timeZone" placeholder="Workspace timezone (optional)" aria-label="Timezone" />
            <button type="submit" :disabled="managerBusy">Create season</button>
          </form>
        </div>
        <div>
          <h3>Rewards</h3>
          <form class="manager-form" @submit.prevent="createReward">
            <h4>Create reward</h4>
            <select v-model="rewardForm.seasonId" aria-label="Reward season"><option value="">Choose season</option><option v-for="season in managerSeasons" :key="season.id" :value="season.id">{{ season.name }}</option></select>
            <input v-model="rewardForm.name" placeholder="Reward name" aria-label="Reward name" />
            <textarea v-model="rewardForm.description" placeholder="Description" aria-label="Reward description" rows="2"></textarea>
            <select v-model="rewardForm.rewardType" aria-label="Reward type"><option v-for="type in rewardTypes" :key="type" :value="type">{{ type }}</option></select>
            <select v-model="rewardForm.condition" aria-label="Reward condition"><option v-for="condition in rewardConditions" :key="condition.key" :value="condition.key">{{ condition.label }}</option></select>
            <input v-if="rewardForm.condition === 'TopN'" v-model.number="rewardForm.rankTo" type="number" min="1" step="1" placeholder="Top N" aria-label="Top N" />
            <input v-else v-model.number="rewardForm.threshold" type="number" min="0" step="0.01" placeholder="Threshold" aria-label="Reward threshold" />
            <label class="manager-checkbox"><input v-model="rewardForm.requireActiveMember" type="checkbox" /> Require active member at settlement</label>
            <button type="submit" :disabled="managerBusy">Create reward</button>
          </form>
          <div class="manager-grants">
            <h4>Qualifying recipients</h4>
            <template v-if="managedGrants.length">
              <div v-for="grant in managedGrants" :key="grant.id" class="manager-grant-row">
                <span><strong>{{ grant.rewardName }}</strong> · {{ grant.recipientName }}<small>{{ grant.status }}</small></span>
                <span class="manager-row-actions">
                  <button v-if="grant.requiresManagerResolution" type="button" @click="resolveGrant(grant, true)">Award tie</button>
                  <button v-if="grant.requiresManagerResolution" type="button" class="danger" @click="resolveGrant(grant, false)">Decline</button>
                  <button v-else-if="grant.status === 'PendingFulfillment' || grant.status === 'Earned'" type="button" @click="fulfillGrant(grant)">Mark fulfilled</button>
                </span>
              </div>
            </template>
            <div v-else class="empty-list-small">Settle a closed season to see recipients.</div>
          </div>
        </div>
      </div>
    </section>

    <div class="rewards-dashboard-container">
      <!-- Left Column: Leaderboard Card -->
      <div class="leaderboard-main-area">
        <!-- Top 3 section (Outside panel) -->
        <div class="top-three-section">
          <!-- Rank #2 (Silver) -->
          <div v-if="top2" class="top-three-col silver-col" :class="{ active: selectedUser?.userId === top2.userId }" @click="selectUser(top2)">
            <div class="avatar-wrapper">
              <div class="crown-badge silver-crown">
                <i class="fa-solid fa-crown"></i>
              </div>
              <UserAvatar :user="{ ...top2, fullName: top2.userName, id: top2.userId }" :size="80" :fontSize="24" class="card-avatar" />
              <div class="rank-badge silver-badge">
                <svg viewBox="0 0 100 36" class="ribbon-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="silver-grad-main" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#A1B0C4" />
                      <stop offset="40%" stop-color="#64748B" />
                      <stop offset="100%" stop-color="#475569" />
                    </linearGradient>
                    <linearGradient id="silver-grad-tail" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#475569" />
                      <stop offset="100%" stop-color="#334155" />
                    </linearGradient>
                  </defs>
                  <path d="M 14 24 L 18 24 L 18 29 Z" class="ribbon-shadow" />
                  <path d="M 86 24 L 82 24 L 82 29 Z" class="ribbon-shadow" />
                  <path d="M 4 13 L 18 8 L 18 24 L 2 26 L 6 19 Z" class="ribbon-tail" fill="url(#silver-grad-tail)" />
                  <path d="M 96 13 L 82 8 L 82 24 L 98 26 L 94 19 Z" class="ribbon-tail" fill="url(#silver-grad-tail)" />
                  <path d="M 14 5 L 86 5 L 86 24 L 14 24 Z" class="ribbon-main" fill="url(#silver-grad-main)" />
                </svg>
                <span class="rank-text">#2</span>
              </div>
            </div>
            <strong class="card-name">{{ top2.userName }}</strong>
            <span class="card-points">{{ top2.totalPoints }} pts</span>
            <span class="card-title">{{ top2.careerTitle || 'Contributor' }}</span>
          </div>
          <div v-else class="top-three-col silver-col empty-col">
            <div class="avatar-wrapper">
              <div class="crown-badge silver-crown">
                <i class="fa-solid fa-crown"></i>
              </div>
              <div class="card-avatar avatar-empty">-</div>
              <div class="rank-badge silver-badge">
                <svg viewBox="0 0 100 36" class="ribbon-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="silver-grad-main-empty" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#A1B0C4" />
                      <stop offset="40%" stop-color="#64748B" />
                      <stop offset="100%" stop-color="#475569" />
                    </linearGradient>
                    <linearGradient id="silver-grad-tail-empty" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#475569" />
                      <stop offset="100%" stop-color="#334155" />
                    </linearGradient>
                  </defs>
                  <path d="M 14 24 L 18 24 L 18 29 Z" class="ribbon-shadow" />
                  <path d="M 86 24 L 82 24 L 82 29 Z" class="ribbon-shadow" />
                  <path d="M 4 13 L 18 8 L 18 24 L 2 26 L 6 19 Z" class="ribbon-tail" fill="url(#silver-grad-tail-empty)" />
                  <path d="M 96 13 L 82 8 L 82 24 L 98 26 L 94 19 Z" class="ribbon-tail" fill="url(#silver-grad-tail-empty)" />
                  <path d="M 14 5 L 86 5 L 86 24 L 14 24 Z" class="ribbon-main" fill="url(#silver-grad-main-empty)" />
                </svg>
                <span class="rank-text">#2</span>
              </div>
            </div>
            <strong class="card-name">Trống</strong>
            <span class="card-points">-</span>
          </div>

          <!-- Rank #1 (Gold) -->
          <div v-if="top1" class="top-three-col gold-col" :class="{ active: selectedUser?.userId === top1.userId }" @click="selectUser(top1)">
            <div class="avatar-wrapper">
              <div class="crown-badge gold-crown">
                <i class="fa-solid fa-crown"></i>
              </div>
              <UserAvatar :user="{ ...top1, fullName: top1.userName, id: top1.userId }" :size="96" :fontSize="28" class="card-avatar" />
              <div class="rank-badge gold-badge">
                <svg viewBox="0 0 100 36" class="ribbon-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="gold-grad-main" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#FFE79A" />
                      <stop offset="35%" stop-color="#FFB300" />
                      <stop offset="75%" stop-color="#E58A00" />
                      <stop offset="100%" stop-color="#B25900" />
                    </linearGradient>
                    <linearGradient id="gold-grad-tail" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#E58A00" />
                      <stop offset="100%" stop-color="#6E3300" />
                    </linearGradient>
                  </defs>
                  <path d="M 14 24 L 18 24 L 18 29 Z" class="ribbon-shadow" />
                  <path d="M 86 24 L 82 24 L 82 29 Z" class="ribbon-shadow" />
                  <path d="M 4 13 L 18 8 L 18 24 L 2 26 L 6 19 Z" class="ribbon-tail" fill="url(#gold-grad-tail)" />
                  <path d="M 96 13 L 82 8 L 82 24 L 98 26 L 94 19 Z" class="ribbon-tail" fill="url(#gold-grad-tail)" />
                  <path d="M 14 5 L 86 5 L 86 24 L 14 24 Z" class="ribbon-main" fill="url(#gold-grad-main)" />
                </svg>
                <span class="rank-text">#1</span>
              </div>
            </div>
            <strong class="card-name">{{ top1.userName }}</strong>
            <span class="card-points">{{ top1.totalPoints }} pts</span>
            <span class="card-title">{{ top1.careerTitle || 'Specialist' }}</span>
          </div>
          <div v-else class="top-three-col gold-col empty-col">
            <div class="avatar-wrapper">
              <div class="crown-badge gold-crown">
                <i class="fa-solid fa-crown"></i>
              </div>
              <div class="card-avatar avatar-empty">-</div>
              <div class="rank-badge gold-badge">
                <svg viewBox="0 0 100 36" class="ribbon-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="gold-grad-main-empty" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#FFE79A" />
                      <stop offset="35%" stop-color="#FFB300" />
                      <stop offset="75%" stop-color="#E58A00" />
                      <stop offset="100%" stop-color="#B25900" />
                    </linearGradient>
                    <linearGradient id="gold-grad-tail-empty" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#E58A00" />
                      <stop offset="100%" stop-color="#6E3300" />
                    </linearGradient>
                  </defs>
                  <path d="M 14 24 L 18 24 L 18 29 Z" class="ribbon-shadow" />
                  <path d="M 86 24 L 82 24 L 82 29 Z" class="ribbon-shadow" />
                  <path d="M 4 13 L 18 8 L 18 24 L 2 26 L 6 19 Z" class="ribbon-tail" fill="url(#gold-grad-tail-empty)" />
                  <path d="M 96 13 L 82 8 L 82 24 L 98 26 L 94 19 Z" class="ribbon-tail" fill="url(#gold-grad-tail-empty)" />
                  <path d="M 14 5 L 86 5 L 86 24 L 14 24 Z" class="ribbon-main" fill="url(#gold-grad-main-empty)" />
                </svg>
                <span class="rank-text">#1</span>
              </div>
            </div>
            <strong class="card-name">Trống</strong>
            <span class="card-points">-</span>
          </div>

          <!-- Rank #3 (Bronze) -->
          <div v-if="top3" class="top-three-col bronze-col" :class="{ active: selectedUser?.userId === top3.userId }" @click="selectUser(top3)">
            <div class="avatar-wrapper">
              <div class="crown-badge bronze-crown">
                <i class="fa-solid fa-crown"></i>
              </div>
              <UserAvatar :user="{ ...top3, fullName: top3.userName, id: top3.userId }" :size="68" :fontSize="20" class="card-avatar" />
              <div class="rank-badge bronze-badge">
                <svg viewBox="0 0 100 36" class="ribbon-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="bronze-grad-main" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#B87333" />
                      <stop offset="40%" stop-color="#8B4513" />
                      <stop offset="100%" stop-color="#5C2E16" />
                    </linearGradient>
                    <linearGradient id="bronze-grad-tail" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#5C2E16" />
                      <stop offset="100%" stop-color="#3D1A00" />
                    </linearGradient>
                  </defs>
                  <path d="M 14 24 L 18 24 L 18 29 Z" class="ribbon-shadow" />
                  <path d="M 86 24 L 82 24 L 82 29 Z" class="ribbon-shadow" />
                  <path d="M 4 13 L 18 8 L 18 24 L 2 26 L 6 19 Z" class="ribbon-tail" fill="url(#bronze-grad-tail)" />
                  <path d="M 96 13 L 82 8 L 82 24 L 98 26 L 94 19 Z" class="ribbon-tail" fill="url(#bronze-grad-tail)" />
                  <path d="M 14 5 L 86 5 L 86 24 L 14 24 Z" class="ribbon-main" fill="url(#bronze-grad-main)" />
                </svg>
                <span class="rank-text">#3</span>
              </div>
            </div>
            <strong class="card-name">{{ top3.userName }}</strong>
            <span class="card-points">{{ top3.totalPoints }} pts</span>
            <span class="card-title">{{ top3.careerTitle || 'Contributor' }}</span>
          </div>
          <div v-else class="top-three-col bronze-col empty-col">
            <div class="avatar-wrapper">
              <div class="crown-badge bronze-crown">
                <i class="fa-solid fa-crown"></i>
              </div>
              <div class="card-avatar avatar-empty">-</div>
              <div class="rank-badge bronze-badge">
                <svg viewBox="0 0 100 36" class="ribbon-svg" xmlns="http://www.w3.org/2000/svg">
                  <defs>
                    <linearGradient id="bronze-grad-main-empty" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#B87333" />
                      <stop offset="40%" stop-color="#8B4513" />
                      <stop offset="100%" stop-color="#5C2E16" />
                    </linearGradient>
                    <linearGradient id="bronze-grad-tail-empty" x1="0%" y1="0%" x2="0%" y2="100%">
                      <stop offset="0%" stop-color="#5C2E16" />
                      <stop offset="100%" stop-color="#3D1A00" />
                    </linearGradient>
                  </defs>
                  <path d="M 14 24 L 18 24 L 18 29 Z" class="ribbon-shadow" />
                  <path d="M 86 24 L 82 24 L 82 29 Z" class="ribbon-shadow" />
                  <path d="M 4 13 L 18 8 L 18 24 L 2 26 L 6 19 Z" class="ribbon-tail" fill="url(#bronze-grad-tail-empty)" />
                  <path d="M 96 13 L 82 8 L 82 24 L 98 26 L 94 19 Z" class="ribbon-tail" fill="url(#bronze-grad-tail-empty)" />
                  <path d="M 14 5 L 86 5 L 86 24 L 14 24 Z" class="ribbon-main" fill="url(#bronze-grad-main-empty)" />
                </svg>
                <span class="rank-text">#3</span>
              </div>
            </div>
            <strong class="card-name">Trống</strong>
            <span class="card-points">-</span>
          </div>
        </div>

        <!-- Leaderboard Table Card -->
        <div class="leaderboard-card panel">
          <!-- Rankings Table -->
          <div class="rankings-table-container">
            <div v-if="restLeaders.length === 0" class="empty">
              Không có thành viên xếp hạng tiếp theo.
            </div>
            <table v-else class="rankings-table">
              <tbody>
                <tr v-for="(item, index) in restLeaders" :key="item.userId" class="ranking-row" :class="{ active: selectedUser?.userId === item.userId, 'is-me': item.userId === wallet.userId }" @click="selectUser(item)">
                  <td class="col-rank">
                    <span class="rank-number">#{{ index + 4 < 10 ? '0' + (index + 4) : index + 4 }}</span>
                  </td>
                  <td class="col-user">
                    <div class="user-cell">
                      <UserAvatar :user="{ ...item, fullName: item.userName, id: item.userId }" :size="28" :fontSize="10" />
                      <span class="user-name">{{ item.userName }}</span>
                      <span v-if="item.userId === wallet.userId" class="me-tag">Bạn</span>
                    </div>
                  </td>
                  <td class="col-level">
                    <span class="level-text">{{ item.careerTitle || 'Cấp độ ' + item.level }}</span>
                  </td>
                  <td class="col-points">
                    <strong class="points-text">{{ item.totalPoints }} pts</strong>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>

      <!-- Right Column: Sidebar area -->
      <div class="profile-details-sidebar">
        <!-- If selected user is the logged-in user -->
        <template v-if="selectedUser && selectedUser.isMe">
          <!-- YOUR PROGRESS Card -->
          <div class="panel progress-card">
            <div class="card-header">
              <h3>HẠNG CỦA BẠN</h3>
              <span class="sub-label">Sprint này</span>
            </div>
            
            <div class="progress-hero-vertical">
              <!-- RANK -->
              <div class="hero-primary-rank">
                <span class="rank-label">HẠNG SPRINT</span>
                <div class="rank-val" :class="{ 'not-ranked': myRankIndex === -1 }">
                  {{ myRankIndex !== -1 ? myRankDisplay : '—' }}
                </div>
                <div class="rank-status" :class="{ 'not-ranked': myRankIndex === -1 }">
                  {{ myRankIndex !== -1 ? 'Đang xếp hạng' : 'Chưa xếp hạng' }}
                </div>
                <div class="rank-desc">
                  {{ wallet.totalPoints }} pts trong sprint này
                </div>
              </div>
              
              <div class="divider-line"></div>
              
              <!-- LEVEL -->
              <div class="hero-level-section">
                <span class="level-label">CẤP ĐỘ</span>
                <div class="level-val">{{ career.title || 'Contributor' }}</div>
              </div>
              
              <!-- LEVEL & PROGRESS -->
              <div class="progress-bar-section">
                <div class="bar-info">
                  <span class="bar-level-remaining">{{ pointsToNext }} pts đến cấp tiếp theo</span>
                  <span class="bar-percentage">{{ career.progressPercent }}%</span>
                </div>
                <div class="bar-track">
                  <div class="bar-fill" :style="{ width: `${career.progressPercent}%` }"></div>
                </div>
              </div>
            </div>
          </div>

          <!-- SPRINT SUMMARY Card -->
          <div class="panel summary-card">
            <div class="card-header">
              <h3>TÓM TẮT SPRINT</h3>
              <span class="sub-label">Chu kỳ này</span>
            </div>
            
            <div class="summary-grid">
              <div class="summary-item">
                <span class="label">ĐÃ HOÀN THÀNH</span>
                <strong class="value">{{ summary.completedTasks }}</strong>
              </div>
              <div class="summary-item">
                <span class="label">THƯỞNG TIẾN ĐỘ</span>
                <strong class="value">{{ summary.earlyBonuses }}</strong>
              </div>
              <div class="summary-item">
                <span class="label">TỔNG ĐIỂM NHẬN</span>
                <strong class="value">{{ summary.basePoints + summary.bonusPoints }}</strong>
              </div>
            </div>
          </div>

          <!-- YOUR ACTIVITIES Card -->
          <div class="panel activities-card">
            <div class="custom-tabs-header">
              <button class="tab-btn" :class="{ active: activeTab === 'tasks' }" @click="activeTab = 'tasks'">Công việc</button>
              <button class="tab-btn" :class="{ active: activeTab === 'history' }" @click="activeTab = 'history'">Lịch sử điểm</button>
            </div>
            
            <div class="custom-tabs-content">
              <div v-if="activeTab === 'tasks'" class="tab-pane">
                <div v-if="spotlightTasks.length === 0" class="empty-list-small">Chưa có công việc tiêu biểu.</div>
                <div class="mini-task-list" v-else>
                  <div v-for="task in spotlightTasks" :key="task.id" class="mini-task-item">
                    <div class="task-info">
                      <strong>{{ task.sequenceId }}</strong>
                      <div class="task-title">{{ task.title }}</div>
                    </div>
                    <span class="pts-badge">+{{ task.fairPoints }}đ</span>
                  </div>
                </div>
              </div>
              
              <div v-else-if="activeTab === 'history'" class="tab-pane">
                <div v-if="transactions.length === 0" class="empty-list-small">Chưa có giao dịch điểm nào.</div>
                <div class="mini-tx-list" v-else>
                  <div v-for="tx in transactions" :key="tx.id" class="mini-tx-item">
                    <div class="tx-info">
                      <div class="tx-title">{{ tx.taskSequenceId || tx.taskTitle || tx.reason }}</div>
                      <time>{{ formatDate(tx.createdAt) }}</time>
                    </div>
                    <strong class="tx-pts" :class="{ negative: tx.amount < 0 }">
                      {{ tx.amount > 0 ? '+' : '' }}{{ tx.amount }}đ
                    </strong>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>

        <!-- If another user is selected -->
        <template v-else-if="selectedUser">
          <!-- SELECTED MEMBER PROFILE -->
          <div class="panel progress-card">
            <div class="card-header flex-between items-center">
              <h3>HỒ SƠ THÀNH VIÊN</h3>
              <button class="back-to-me-btn" @click="resetToMe">
                <i class="fa-solid fa-house-user mr-1"></i> Hồ sơ của tôi
              </button>
            </div>
            
            <div class="profile-hero-section">
              <UserAvatar :user="{ ...selectedUser, fullName: selectedUser.userName, id: selectedUser.userId }" :size="56" :fontSize="18" class="card-avatar mb-3" />
              <h4 class="profile-name">{{ selectedUser.userName }}</h4>
              <span class="profile-level-badge">{{ selectedUserCareer.title }}</span>
            </div>

            <div class="progress-hero-vertical mt-4">
              <!-- RANK -->
              <div class="hero-primary-rank">
                <span class="rank-label">HẠNG SPRINT</span>
                <div class="rank-val">
                  #{{ leaderboard.findIndex(u => String(u.userId) === String(selectedUser.userId)) + 1 }}
                </div>
                <div class="rank-status">Đang xếp hạng</div>
                <div class="rank-desc">
                  {{ selectedUser.totalPoints }} pts trong sprint này
                </div>
              </div>
              
              <div class="divider-line"></div>
              
              <!-- LEVEL -->
              <div class="hero-level-section">
                <span class="level-label">CẤP ĐỘ</span>
                <div class="level-val">{{ selectedUserCareer.title || 'Contributor' }}</div>
              </div>
              
              <!-- LEVEL & PROGRESS -->
              <div class="progress-bar-section">
                <div class="bar-info">
                  <span class="bar-level-remaining">{{ selectedUserCareer.pointsToNext }} pts đến cấp tiếp theo</span>
                  <span class="bar-percentage">{{ selectedUserCareer.progressPercent }}%</span>
                </div>
                <div class="bar-track">
                  <div class="bar-fill" :style="{ width: `${selectedUserCareer.progressPercent}%` }"></div>
                </div>
              </div>
            </div>
          </div>

          <!-- Privacy lock panel -->
          <div class="panel privacy-panel">
            <div class="privacy-icon">
              <i class="fa-solid fa-lock"></i>
            </div>
            <h4>Hồ sơ được bảo mật</h4>
            <p>
              Các chỉ số chi tiết của Sprint, danh sách công việc và lịch sử điểm số của thành viên khác được bảo mật để đảm bảo quyền riêng tư.
            </p>
          </div>
        </template>
      </div>
    </div>
  </section>
</template>

<script setup>
import { computed, onMounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18nStore } from '@/store/useI18nStore'
import { useAuthStore } from '@/store/useAuthStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import axiosClient from '@/api/axiosClient'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { getScopedCurrentProjectId } from '@/utils/projectContext'
import { validateRewardForm, validateRewardSeasonForm } from '@/utils/rewardUi'

const { t } = useI18nStore()
const authStore = useAuthStore()
const peopleStore = usePeopleStore()
const currentUser = authStore.user || {}

const loading = ref(false)
const wallet = ref({
  userId: currentUser.id || currentUser.userId || '',
  userName: currentUser.fullName || currentUser.userName || currentUser.email || 'Bạn',
  avatarColor: currentUser.avatarColor || currentUser.AvatarColor || '',
  totalPoints: 0,
  level: 1,
  nextLevelAt: 1000
})
const career = ref({ level: 1, title: 'Contributor', progressPercent: 0, nextThreshold: 1000 })
const formula = ref({
  expression: 'Base effort x Efficiency x Quality x Contribution share',
  actualHoursRule: '',
  policy: {},
  sample: { difficulty: 0, duration: 0, share: 0, total: 0, bonus: 0, penalty: 0, note: '' }
})
const summary = ref({ completedTasks: 0, earlyBonuses: 0, basePoints: 0, bonusPoints: 0, penaltyPoints: 0, contributionPercent: 0, rollbackPoints: 0, estimatedHours: 0, actualHours: 0, loggedHours: 0 })
const spotlightTasks = ref([])
const recentAchievements = ref([])
const transactions = ref([])
const leaderboard = ref([])
const seasonDashboard = ref({ currentSeason: null, careerXp: 0, careerLevel: 1, mySeasonPoints: 0, myRank: 0, myOnTimeRate: 0, leaderboard: [], pendingEvents: [], openRewards: [], rewardHistory: [], availableRewards: [], rewardProgress: [], canManage: false })
const managerSeasons = ref([])
const managerBusy = ref(false)
const seasonForm = ref({ name: '', type: 'Sprint', startAt: '', endAt: '', timeZone: '' })
const rewardForm = ref({ seasonId: '', name: '', description: '', rewardType: 'Gift', condition: 'TopN', threshold: 100, rankTo: 1, requireActiveMember: true })
const rewardTypes = ['Cash', 'Voucher', 'Gift', 'Privilege', 'Custom']
const rewardConditions = [
  { key: 'TopN', label: 'Top N' },
  { key: 'SeasonPoints', label: 'Season Points ≥ X' },
  { key: 'OnTimeRate', label: 'On-time rate ≥ X%' },
  { key: 'ApprovedTasks', label: 'Approved tasks ≥ X' },
  { key: 'TeamOnTimeRate', label: 'Team on-time rate ≥ X%' }
]

// New interactive state variables
const activeTab = ref('tasks')
const selectedUser = ref({
  userId: wallet.value.userId,
  userName: wallet.value.userName,
  totalPoints: 0,
  isMe: true,
  avatarColor: wallet.value.avatarColor
})

const formatDate = (value) => (value ? new Date(value).toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' }) : '')
const seasonTimeRemaining = computed(() => {
  const end = seasonDashboard.value.currentSeason?.endAt
  if (!end) return 'No end date'
  const remaining = new Date(end).getTime() - Date.now()
  if (remaining <= 0) return 'Expired — close when ready'
  const days = Math.floor(remaining / 86400000)
  const hours = Math.floor((remaining % 86400000) / 3600000)
  return `${days}d ${hours}h remaining`
})
const managedGrants = computed(() => seasonDashboard.value.openRewards || [])
const pointsToNext = computed(() => Math.max(0, Number(career.value?.nextThreshold || 0) - Number(wallet.value?.totalPoints || 0)))

const top1 = computed(() => leaderboard.value[0] || null)
const top2 = computed(() => leaderboard.value[1] || null)
const top3 = computed(() => leaderboard.value[2] || null)
const restLeaders = computed(() => leaderboard.value.slice(3))

const myRankIndex = computed(() => {
  if (!leaderboard.value || !wallet.value?.userId) return -1
  return leaderboard.value.findIndex(u => String(u.userId || u.Id || u.id) === String(wallet.value.userId))
})
const myRankDisplay = computed(() => {
  if (myRankIndex.value === -1) return '--'
  const rank = myRankIndex.value + 1
  return rank < 10 ? `#0${rank}` : `#${rank}`
})
const calculateClientCareer = (points) => {
  let level = 1
  let currentThreshold = 0
  const pointsForLevel = (lvl) => 250 * lvl * (lvl + 1)
  
  let nextLvl = level + 1
  let nextThresh = pointsForLevel(nextLvl)
  while (points >= nextThresh) {
    level++
    currentThreshold = nextThresh
    nextLvl = level + 1
    nextThresh = pointsForLevel(nextLvl)
  }
  
  const span = Math.max(1, nextThresh - currentThreshold)
  const pointsIntoLevel = Math.max(0, points - currentThreshold)
  const progressPercent = Math.min(100, Math.round((pointsIntoLevel / span) * 100))
  
  const getCareerTitle = (lvl) => {
    if (lvl <= 1) return 'Contributor'
    if (lvl === 2) return 'Specialist'
    if (lvl === 3) return 'Senior Specialist'
    if (lvl === 4) return 'Lead'
    if (lvl === 5) return 'Principal'
    return 'Director'
  }
  
  return {
    level,
    title: getCareerTitle(level),
    nextThreshold: nextThresh,
    progressPercent,
    pointsToNext: Math.max(0, nextThresh - points)
  }
}

const selectedUserCareer = computed(() => {
  if (!selectedUser.value) return { level: 1, title: 'Contributor', progressPercent: 0, pointsToNext: 1000 }
  if (selectedUser.value.isMe) {
    return {
      level: career.value.level,
      title: career.value.title,
      progressPercent: career.value.progressPercent,
      pointsToNext: pointsToNext.value
    }
  }
  return calculateClientCareer(selectedUser.value.totalPoints)
})

const selectUser = (user) => {
  if (!user) return
  selectedUser.value = {
    userId: user.userId,
    userName: user.userName,
    totalPoints: user.totalPoints,
    isMe: user.userId === wallet.value.userId,
    avatarColor: user.avatarColor || user.AvatarColor
  }
}

const resetToMe = () => {
  if (wallet.value) {
    selectedUser.value = {
      userId: wallet.value.userId,
      userName: wallet.value.userName || 'Bạn',
      totalPoints: wallet.value.totalPoints,
      isMe: true,
      avatarColor: wallet.value.avatarColor || wallet.value.AvatarColor
    }
  }
}

const loadRewards = async () => {
  loading.value = true
  try {
    const promises = [
      axiosClient.get('/gamification/me'),
      axiosClient.get('/gamification/leaderboard')
    ]
    if (!peopleStore.users || peopleStore.users.length === 0) {
      promises.push(peopleStore.fetchPeople('', 1, 100))
    }

    const projectId = getScopedCurrentProjectId()
    if (projectId) promises.push(axiosClient.get(`/projects/${projectId}/rewards/dashboard`).catch(() => null))

    const results = await Promise.all(promises)
    const mine = results[0]
    const leaders = results[1]

    const data = mine.data?.data || {}
    const nextWallet = data.wallet || {}
    const nextCareer = data.career || {}

    wallet.value = {
      ...wallet.value,
      ...nextWallet,
      totalPoints: Number(nextWallet.totalPoints ?? wallet.value.totalPoints ?? 0),
      level: Number(nextWallet.level ?? wallet.value.level ?? 1),
      nextLevelAt: Number(nextWallet.nextLevelAt ?? wallet.value.nextLevelAt ?? 1000),
      userId: nextWallet.userId || wallet.value.userId,
      userName: nextWallet.userName || nextWallet.email || wallet.value.userName,
      avatarColor: nextWallet.avatarColor || nextWallet.AvatarColor || wallet.value.avatarColor
    }
    career.value = {
      ...career.value,
      ...nextCareer,
      level: Number(nextCareer.level ?? wallet.value.level ?? career.value.level ?? 1),
      title: nextCareer.title || wallet.value.rankTitle || career.value.title || 'Contributor',
      nextThreshold: Number(nextCareer.nextThreshold ?? wallet.value.nextLevelAt ?? career.value.nextThreshold ?? 1000),
      progressPercent: Math.max(0, Math.min(100, Number(nextCareer.progressPercent ?? career.value.progressPercent ?? 0)))
    }
    formula.value = {
      ...formula.value,
      ...(data.formula || {}),
      sample: {
        ...formula.value.sample,
        ...(data.formula?.sample || {})
      },
      policy: {
        ...formula.value.policy,
        ...(data.formula?.policy || {})
      }
    }
    summary.value = {
      ...summary.value,
      ...(data.summary || {})
    }
    spotlightTasks.value = data.spotlightTasks || []
    recentAchievements.value = data.recentAchievements || []
    transactions.value = data.transactions || []
    leaderboard.value = leaders.data?.data || []
    const seasonResponse = projectId ? results[results.length - 1] : null
    if (seasonResponse?.data) seasonDashboard.value = { ...seasonDashboard.value, ...(seasonResponse.data?.data || seasonResponse.data) }
    if (projectId && seasonDashboard.value.canManage) {
      const seasonsResponse = await axiosClient.get(`/projects/${projectId}/rewards/seasons`)
      managerSeasons.value = seasonsResponse.data?.data || seasonsResponse.data || []
    } else if (seasonDashboard.value.currentSeason) {
      managerSeasons.value = [seasonDashboard.value.currentSeason]
    }

    // Initialize selectedUser
    if (selectedUser.value) {
      if (selectedUser.value.isMe) {
        resetToMe()
      } else {
        const found = leaderboard.value.find(u => u.userId === selectedUser.value.userId)
        if (found) {
          selectUser(found)
        } else {
          resetToMe()
        }
      }
    } else {
      resetToMe()
    }
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to load rewards.')
  } finally {
    loading.value = false
  }
}

const showValidationErrors = (errors) => {
  if (errors.length) ElMessage.warning(errors[0])
  return errors.length === 0
}

const createSeason = async () => {
  if (!showValidationErrors(validateRewardSeasonForm(seasonForm.value))) return
  const projectId = getScopedCurrentProjectId()
  if (!projectId) return
  managerBusy.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/rewards/seasons`, {
      name: seasonForm.value.name.trim(), type: seasonForm.value.type, startAt: `${seasonForm.value.startAt}T00:00:00+00:00`,
      endAt: seasonForm.value.type === 'Custom' && seasonForm.value.endAt ? `${seasonForm.value.endAt}T23:59:59.9999999+00:00` : null,
      timeZone: seasonForm.value.timeZone.trim() || null
    })
    seasonForm.value = { name: '', type: 'Sprint', startAt: '', endAt: '', timeZone: '' }
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to create season.')
  } finally {
    managerBusy.value = false
  }
}

const createReward = async () => {
  if (!showValidationErrors(validateRewardForm(rewardForm.value))) return
  const projectId = getScopedCurrentProjectId()
  if (!projectId) return
  const topN = rewardForm.value.condition === 'TopN'
  managerBusy.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/rewards/seasons/${rewardForm.value.seasonId}/definitions`, {
      name: rewardForm.value.name.trim(), description: rewardForm.value.description.trim() || null, rewardType: rewardForm.value.rewardType,
      displayValue: null, currency: null, conditionType: topN ? 'Ranking' : rewardForm.value.condition === 'TeamOnTimeRate' ? 'TeamGoal' : 'PersonalMilestone',
      conditionMetric: topN ? 'SeasonPoints' : rewardForm.value.condition === 'ApprovedTasks' ? 'FinalizedTaskCount' : rewardForm.value.condition.includes('OnTimeRate') ? 'OnTimeRate' : 'SeasonPoints',
      threshold: topN ? 0 : Number(rewardForm.value.threshold), rankFrom: topN ? 1 : null, rankTo: topN ? Number(rewardForm.value.rankTo) : null,
      requireActiveMemberAtSettlement: rewardForm.value.requireActiveMember
    })
    rewardForm.value = { seasonId: rewardForm.value.seasonId, name: '', description: '', rewardType: 'Gift', condition: 'TopN', threshold: 100, rankTo: 1, requireActiveMember: true }
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to create reward.')
  } finally {
    managerBusy.value = false
  }
}

const activateSeason = async (season) => {
  const projectId = getScopedCurrentProjectId()
  if (!projectId || !season?.id) return
  managerBusy.value = true
  try { await axiosClient.post(`/projects/${projectId}/rewards/seasons/${season.id}/activate`); await loadRewards() } catch (error) { ElMessage.error(error.response?.data?.message || 'Unable to activate season.') } finally { managerBusy.value = false }
}

const closeSeason = async (season) => {
  const projectId = getScopedCurrentProjectId()
  if (!projectId || !season?.id) return
  managerBusy.value = true
  try { await axiosClient.post(`/projects/${projectId}/rewards/seasons/${season.id}/close`); await loadRewards() } catch (error) { ElMessage.error(error.response?.data?.message || 'Unable to close season.') } finally { managerBusy.value = false }
}

const resolveGrant = async (grant, award) => {
  const projectId = getScopedCurrentProjectId()
  if (!projectId || !grant?.id) return
  try { await axiosClient.post(`/projects/${projectId}/rewards/grants/${grant.id}/resolve`, { award, note: 'Resolved by manager.' }); await loadRewards() } catch (error) { ElMessage.error(error.response?.data?.message || 'Unable to resolve reward tie.') }
}

const fulfillGrant = async (grant) => {
  const projectId = getScopedCurrentProjectId()
  if (!projectId || !grant?.id) return
  try { await axiosClient.post(`/projects/${projectId}/rewards/grants/${grant.id}/fulfill`); await loadRewards() } catch (error) { ElMessage.error(error.response?.data?.message || 'Unable to fulfill reward.') }
}

const reviewSeasonEvent = async (event, approve) => {
  const projectId = getScopedCurrentProjectId()
  if (!projectId || !event?.id) return
  try {
    await axiosClient.post(`/projects/${projectId}/rewards/events/${event.id}/review`, { approve })
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to review reward event.')
  }
}

onMounted(loadRewards)
</script>

<style scoped>
.rewards-page {
  --sa-page-x: 18px;
  min-height: 100%;
  width: 100%;
  background: var(--reward-bg, #F7F9FC);
  color: var(--reward-text, #172033);
  padding: 0;
  margin: 0;
  font-family: 'Inter', sans-serif;

  /* Corporate Palette */
  --reward-bg: #F7F9FC;
  --reward-surface: #FFFFFF;
  --reward-text: #172033;
  --reward-muted: #667085;
  --reward-border: #E4E9F0;
  
  --reward-accent: var(--color-accent, #0f62fe);
  --reward-accent-strong: var(--color-accent-hover, #0043ce);
  
  --reward-gold: #C9A227; /* Muted executive gold */
  --reward-silver: #8A99AD; /* Muted cool silver gray */
  --reward-bronze: #B87333; /* Muted warm bronze */
}

.season-v1-panel {
  margin: 0 var(--sa-page-x, 24px) 4px;
  padding: 18px 20px;
  border: 1px solid color-mix(in srgb, var(--reward-accent, #6366f1) 24%, transparent);
  background: linear-gradient(135deg, color-mix(in srgb, var(--reward-accent, #6366f1) 8%, var(--reward-surface, #fff)), var(--reward-surface, #fff));
}
.season-v1-heading, .season-v1-row, .season-v1-actions { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.season-v1-heading h2 { margin: 3px 0; color: var(--reward-text, #111827); }
.season-v1-heading p, .season-v1-eyebrow { margin: 0; color: var(--reward-muted, #64748b); font-size: 12px; }
.season-v1-eyebrow { color: var(--reward-accent, #6366f1); font-weight: 700; text-transform: uppercase; letter-spacing: .08em; }
.season-v1-xp { display: grid; justify-items: end; color: var(--reward-muted, #64748b); }
.season-v1-xp strong { color: var(--reward-accent, #6366f1); font-size: 26px; }
.season-v1-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 22px; margin-top: 16px; }
.season-v1-grid h3 { margin: 0 0 8px; font-size: 13px; color: var(--reward-text, #111827); }
.season-v1-row { min-height: 34px; border-top: 1px solid color-mix(in srgb, var(--reward-muted, #64748b) 14%, transparent); color: var(--reward-text, #111827); font-size: 13px; }
.season-v1-progress { margin-top: 16px; }
.season-v1-progress h3 { margin: 0 0 8px; font-size: 13px; color: var(--reward-text, #111827); }
.season-v1-progress-row { display: grid; grid-template-columns: minmax(0, 1fr) auto minmax(90px, 18%); align-items: center; gap: 12px; min-height: 30px; color: var(--reward-text, #111827); font-size: 12px; }
.season-v1-progress-track { height: 6px; overflow: hidden; border-radius: 999px; background: color-mix(in srgb, var(--reward-muted, #64748b) 18%, transparent); }
.season-v1-progress-fill { height: 100%; border-radius: inherit; background: var(--reward-accent, #6366f1); }
.season-v1-stats { grid-column: 1 / -1; }
.season-v1-stat-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: 10px; }
.season-v1-stat-grid span { display: grid; gap: 3px; padding: 10px; border: 1px solid var(--reward-border); border-radius: 7px; color: var(--reward-muted); font-size: 11px; }
.season-v1-stat-grid strong { color: var(--reward-text); font-size: 14px; }
.season-v1-history { margin-top: 16px; }
.season-v1-history h3 { margin: 0 0 8px; font-size: 13px; }
.season-v1-actions button { border: 0; border-radius: 6px; padding: 4px 8px; color: #fff; background: var(--reward-accent, #6366f1); cursor: pointer; font-size: 11px; }
.season-v1-actions button.reject { background: #b91c1c; }
.reward-manager-panel { margin: 18px var(--sa-page-x, 24px) 20px; padding: 18px 20px; }
.manager-panel-heading, .manager-season-row, .manager-grant-row { display: flex; align-items: center; justify-content: space-between; gap: 14px; }
.manager-panel-heading h2 { margin: 3px 0 0; }
.manager-note, .manager-season-row small, .manager-grant-row small { display: block; color: var(--reward-muted); font-size: 11px; }
.manager-columns { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 24px; margin-top: 18px; }
.manager-columns h3 { margin: 0 0 8px; font-size: 13px; }
.manager-season-list, .manager-grants { display: grid; gap: 8px; }
.manager-season-row, .manager-grant-row { padding: 10px 0; border-top: 1px solid var(--reward-border); font-size: 12px; }
.manager-row-actions { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; justify-content: flex-end; }
.status-pill { padding: 3px 7px; border-radius: 999px; background: var(--reward-bg); color: var(--reward-muted); }
.manager-row-actions button, .manager-form button { border: 0; border-radius: 6px; padding: 6px 9px; color: #fff; background: var(--reward-accent); cursor: pointer; font-size: 11px; }
.manager-row-actions button.danger { background: #b91c1c; }
.manager-form { display: grid; gap: 8px; margin-top: 16px; padding: 14px; border: 1px solid var(--reward-border); border-radius: 8px; }
.manager-form h4, .manager-grants h4 { margin: 0 0 2px; font-size: 12px; }
.manager-form input, .manager-form select, .manager-form textarea { box-sizing: border-box; width: 100%; border: 1px solid var(--reward-border); border-radius: 6px; padding: 8px; background: var(--reward-surface); color: var(--reward-text); font: inherit; font-size: 12px; }
.manager-checkbox { display: flex; align-items: center; gap: 7px; color: var(--reward-muted); font-size: 11px; }
.manager-checkbox input { width: auto; }
@media (max-width: 760px) { .season-v1-grid, .season-v1-progress-row { grid-template-columns: 1fr; } }
@media (max-width: 760px) { .season-v1-stat-grid, .manager-columns { grid-template-columns: 1fr 1fr; } .manager-panel-heading { align-items: flex-start; flex-direction: column; } }

:global(.dark) .rewards-page,
:global([data-theme="dark"]) .rewards-page {
  --reward-bg: #0f172a;
  --reward-surface: #1e293b;
  --reward-text: #f1f5f9;
  --reward-muted: #94a3b8;
  --reward-border: #334155;
  --reward-gold: #C9A227;
  --reward-silver: #A8B2C1;
  --reward-bronze: #B87333;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 16px var(--sa-page-x, 24px) 12px;
  background: var(--reward-surface);
  border-bottom: 1px solid var(--reward-border);
}

.title-with-filter {
  display: flex;
  align-items: center;
  gap: 12px;
}

.title-with-filter h1 {
  font-size: 26px; /* Title size: 24-26px */
  font-weight: 700;
  color: var(--reward-text);
  margin: 0;
  letter-spacing: -0.01em;
}

.sprint-filter-badge {
  background: var(--reward-surface);
  border: 1px solid var(--reward-border);
  color: var(--reward-text);
  padding: 6px 12px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
  transition: all 120ms ease;
}

.sprint-filter-badge:hover {
  background: var(--reward-bg);
  border-color: var(--reward-accent);
}

.rewards-dashboard-container {
  display: flex;
  gap: 20px;
  padding: 20px var(--sa-page-x, 24px);
  align-items: start;
}

@media (max-width: 1024px) {
  .rewards-dashboard-container {
    flex-direction: column;
  }
  .profile-details-sidebar {
    width: 100% !important;
  }
}

/* Left main Area */
.leaderboard-main-area {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

.panel {
  background: var(--reward-surface);
  border: 1px solid var(--reward-border);
  border-radius: 8px;
  padding: 20px;
}

.leaderboard-card {
  display: flex;
  flex-direction: column;
}

.leaderboard-header {
  margin-bottom: 12px;
}

.leaderboard-header h2 {
  font-size: 24px; /* Title size: 24-26px */
  font-weight: 700;
  color: var(--reward-text);
  margin: 0 0 4px 0;
  letter-spacing: -0.01em;
}

.leaderboard-header .subtitle {
  font-size: 13px; /* Secondary text: 12-14px */
  font-weight: 400;
  color: var(--reward-muted);
}

/* Editorial Top 3 Columns - outside the card panel */
.top-three-section {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 24px;
  padding: 4px;
}

@media (max-width: 640px) {
  .top-three-section {
    grid-template-columns: 1fr;
    gap: 24px;
  }
}

.top-three-col {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  cursor: pointer;
  padding: 16px 8px;
  min-height: 160px;
  justify-content: center;
  background: transparent;
  border: none;
  box-shadow: none;
}

.top-three-col.active .card-avatar {
  outline: 3px solid var(--reward-accent);
  outline-offset: 2px;
}

.top-three-col.gold-col {
  z-index: 2;
}

/* Crown & Badge Avatar Wrapper styling */
.avatar-wrapper {
  position: relative;
  margin-bottom: 24px;
  display: inline-flex;
  flex-direction: column;
  align-items: center;
}

.card-avatar {
  border-radius: 50%;
  box-sizing: border-box;
  position: relative;
  z-index: 5;
}

.gold-col .card-avatar {
  border: 3.5px solid #FFD700 !important;
  box-shadow: 0 0 24px rgba(255, 215, 0, 0.45);
}

.silver-col .card-avatar {
  border: 3.5px solid #94A3B8 !important;
  box-shadow: 0 0 18px rgba(148, 163, 184, 0.45);
}

.bronze-col .card-avatar {
  border: 3.5px solid #CD7F32 !important;
  box-shadow: 0 0 16px rgba(205, 127, 50, 0.35);
}

.crown-badge {
  position: absolute;
  left: 50%;
  transform: translateX(-50%) rotate(-5deg);
  z-index: 1;
  filter: drop-shadow(0 4px 6px rgba(0,0,0,0.25));
}

.gold-crown {
  font-size: 32px;
  color: #FFD700;
  top: -28px;
  text-shadow: 0 2px 0 #b38600, 0 4px 0 #806000;
}

.silver-crown {
  font-size: 26px;
  color: #A1B0C4;
  top: -24px;
  text-shadow: 0 2px 0 #475569, 0 4px 0 #334155;
}

.bronze-crown {
  font-size: 24px;
  color: #CD7F32;
  top: -20px;
  text-shadow: 0 2px 0 #a0522d, 0 4px 0 #8b4513;
}

.rank-badge {
  position: absolute;
  left: 50%;
  transform: translateX(-50%);
  z-index: 10;
  display: flex;
  align-items: center;
  justify-content: center;
}

.gold-badge {
  width: 108px;
  height: 38px;
  bottom: -16px;
}

.silver-badge {
  width: 92px;
  height: 33px;
  bottom: -14px;
}

.bronze-badge {
  width: 78px;
  height: 28px;
  bottom: -12px;
}

.ribbon-svg {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  filter: drop-shadow(0 3px 5px rgba(0,0,0,0.25));
}

.ribbon-shadow {
  fill: #000;
  opacity: 0.35;
}

.rank-text {
  position: relative;
  z-index: 11;
  font-weight: 900;
  color: #ffffff;
  letter-spacing: 0.05em;
  text-shadow: 0 1px 2px rgba(0,0,0,0.8), 0 0 3px rgba(0,0,0,0.8);
  margin-top: -3px;
}

.gold-badge .rank-text {
  font-size: 14px;
}

.silver-badge .rank-text {
  font-size: 12px;
}

.bronze-badge .rank-text {
  font-size: 10.5px;
}

.card-name {
  font-size: 17px; /* Member name: 17-18px */
  font-weight: 600;
  color: var(--reward-text);
  margin-bottom: 2px;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  width: 100%;
}

.gold-col .card-name {
  font-weight: 700;
  font-size: 18px;
}

.card-points {
  font-size: 28px; /* Points: 28-32px */
  font-weight: 700;
  color: var(--reward-text);
  margin-bottom: 2px;
}

.gold-col .card-points {
  font-size: 32px;
  font-weight: 700;
}

.card-title {
  font-size: 11px; /* Level: 11-12px */
  color: var(--reward-muted);
  font-weight: 500;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}

.empty-col .card-avatar.avatar-empty {
  border-radius: 50%;
  background: var(--reward-bg);
  display: flex;
  align-items: center;
  justify-content: center;
  color: var(--reward-muted);
  border: 2px dashed var(--reward-border);
  margin-bottom: 8px;
  box-sizing: border-box;
  aspect-ratio: 1 / 1;
  flex: 0 0 auto;
}

.silver-col.empty-col .card-avatar.avatar-empty {
  width: 68px;
  height: 68px;
  font-size: 20px;
}

.bronze-col.empty-col .card-avatar.avatar-empty {
  width: 56px;
  height: 56px;
  font-size: 16px;
}

.gold-col.empty-col .card-avatar.avatar-empty {
  width: 80px;
  height: 80px;
  font-size: 24px;
}

/* Rankings Table */
.rankings-table-container {
  overflow-x: auto;
  margin-top: 8px;
}

.rankings-table {
  width: 100%;
  border-collapse: collapse;
}

.rankings-table th {
  padding: 12px 14px; /* clean whitespace */
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  color: var(--reward-muted);
  border-bottom: 1px solid var(--reward-border); /* thin divider */
  letter-spacing: 0.05em;
  text-align: left;
}

.rankings-table th.col-points {
  text-align: right;
}

.ranking-row {
  cursor: pointer;
  transition: background 120ms ease;
  border-bottom: 1px solid var(--reward-border);
}

.ranking-row:hover {
  background: rgba(0, 0, 0, 0.015) !important;
}

:global(.dark) .ranking-row:hover,
:global([data-theme="dark"]) .ranking-row:hover {
  background: rgba(255, 255, 255, 0.015) !important;
}

.ranking-row.active {
  background: rgba(15, 98, 254, 0.03) !important;
}

.ranking-row.is-me {
  background: rgba(15, 98, 254, 0.015) !important;
  border-left: 2px solid var(--reward-accent); /* very light accent indicator */
}

.rankings-table td {
  padding: 12px 14px;
  font-size: 13px;
  color: var(--reward-text);
  vertical-align: middle;
}

.rankings-table td.col-points {
  text-align: right;
}

.col-rank {
  width: 80px;
}

.rank-number {
  font-size: 12px;
  font-weight: 600;
  color: var(--reward-muted); /* muted rank color */
}

.user-cell {
  display: flex;
  align-items: center;
  gap: 10px;
}

.user-name {
  font-size: 14px;
  font-weight: 600;
  color: var(--reward-text); /* visual anchor */
}

.me-tag {
  background: rgba(15, 98, 254, 0.08);
  color: var(--reward-accent);
  font-size: 9px;
  font-weight: 600;
  padding: 1px 4px;
  border-radius: 4px;
  margin-left: 6px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.level-text {
  font-size: 12px;
  color: var(--reward-muted); /* secondary information */
}

.points-text {
  font-weight: 700;
  color: var(--reward-text);
}

/* Right Sidebar area */
.profile-details-sidebar {
  width: 380px;
  flex-shrink: 0;
  display: flex;
  flex-direction: column;
  gap: 20px;
}

/* Panel Design System */
.profile-details-sidebar .panel {
  background: var(--reward-surface);
  border: 1px solid var(--reward-border);
  border-radius: 8px;
  padding: 20px;
}

/* YOUR PROGRESS / MEMBER PROFILE = main card */
.profile-details-sidebar .panel.progress-card {
  border: 1px solid var(--reward-border);
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.02); /* extremely subtle shadow */
}

/* SPRINT SUMMARY = compact card */
.profile-details-sidebar .panel.summary-card {
  padding: 14px 16px;
}

/* ACTIVITIES = compact content section */
.profile-details-sidebar .panel.activities-card {
  border: none;
  background: transparent;
  padding: 0;
  box-shadow: none;
}

.card-header {
  margin-bottom: 12px;
}

.card-header h3 {
  font-size: 12px;
  font-weight: 700;
  color: var(--reward-muted);
  margin: 0 0 2px 0;
  text-transform: uppercase;
  letter-spacing: 0.03em;
}

.card-header .sub-label {
  font-size: 11px;
  color: var(--reward-muted);
  opacity: 0.8;
}

/* Structured layout for progress rank */
.progress-hero-vertical {
  display: flex;
  flex-direction: column;
  gap: 12px;
  margin-bottom: 8px;
}

.hero-primary-rank {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hero-primary-rank .rank-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--reward-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.hero-primary-rank .rank-val {
  font-size: 28px;
  font-weight: 700;
  color: var(--reward-text);
  line-height: 1.1;
}

.hero-primary-rank .rank-val.not-ranked {
  color: var(--reward-muted);
  font-weight: 600;
}

.hero-primary-rank .rank-status {
  font-size: 13px;
  font-weight: 600;
  color: var(--reward-text);
}

.hero-primary-rank .rank-status.not-ranked {
  color: var(--reward-muted);
  font-weight: 500;
}

.hero-primary-rank .rank-desc {
  font-size: 12px;
  color: var(--reward-muted);
  font-weight: 400;
}

.divider-line {
  height: 1px;
  background: var(--reward-border);
  margin: 4px 0;
}

.hero-level-section {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.hero-level-section .level-label {
  font-size: 11px;
  font-weight: 600;
  color: var(--reward-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.hero-level-section .level-val {
  font-size: 15px;
  font-weight: 700;
  color: var(--reward-text);
}

.progress-bar-section {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin-top: 4px;
}

.bar-info {
  display: flex;
  justify-content: space-between;
  font-size: 11px;
  font-weight: 600;
  color: var(--reward-muted);
}

.bar-level-title {
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--reward-muted);
}

.bar-level-remaining {
  color: var(--reward-muted);
}

.bar-track {
  height: 6px;
  background: var(--reward-bg);
  border-radius: 99px;
  overflow: hidden;
  border: 1px solid var(--reward-border);
}

.bar-fill {
  height: 100%;
  background: var(--reward-accent);
  border-radius: 99px;
  transition: width 350ms ease;
}

.bar-percentage {
  font-size: 11px;
  color: var(--reward-muted);
  text-align: right;
  font-weight: 500;
}

/* SPRINT SUMMARY Card */
.summary-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-top: 8px;
}

.summary-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.summary-item .label {
  font-size: 10px;
  font-weight: 600;
  color: var(--reward-muted);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}

.summary-item .value {
  font-size: 20px;
  font-weight: 700;
  color: var(--reward-text);
  line-height: 1;
}

/* Member Profile Sidebar */
.profile-hero-section {
  display: flex;
  flex-direction: column;
  align-items: center;
  text-align: center;
  padding-bottom: 12px;
  border-bottom: 1px solid var(--reward-border);
}

.profile-name {
  font-size: 16px;
  font-weight: 600;
  color: var(--reward-text);
  margin: 0;
}

.profile-level-badge {
  background: var(--reward-bg);
  border: 1px solid var(--reward-border);
  color: var(--reward-muted);
  font-size: 11px;
  font-weight: 600;
  padding: 3px 8px;
  border-radius: 4px;
  margin-top: 4px;
}

.flex-between {
  display: flex;
  justify-content: space-between;
}

/* Custom Tabs styling */
.custom-tabs-header {
  display: flex;
  gap: 16px;
  border-bottom: 1px solid var(--reward-border);
  margin-bottom: 12px;
  margin-top: 8px;
}

.tab-btn {
  background: none;
  border: none;
  padding: 8px 0;
  font-size: 13px;
  font-weight: 600;
  color: var(--reward-muted);
  cursor: pointer;
  position: relative;
  transition: all 150ms ease;
}

.tab-btn::after {
  content: '';
  position: absolute;
  bottom: -1px;
  left: 0;
  right: 0;
  height: 2px;
  background: transparent;
  transition: background 150ms ease;
}

.tab-btn.active {
  color: var(--reward-accent);
}

.tab-btn.active::after {
  background: var(--reward-accent);
}

.custom-tabs-content {
  min-height: 120px;
}

/* Lists in tabs */
.empty-list-small {
  font-size: 13px;
  color: var(--reward-muted);
  text-align: center;
  padding: 32px 16px;
  border: 1px dashed var(--reward-border);
  border-radius: 6px;
  background: rgba(0, 0, 0, 0.01);
}

.mini-task-list,
.mini-tx-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
  max-height: 200px;
  overflow-y: auto;
}

.mini-task-item,
.mini-tx-item {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 10px 12px;
  background: var(--reward-surface);
  border: 1px solid var(--reward-border);
  border-radius: 6px;
  gap: 12px;
  transition: border-color 120ms ease;
}

.mini-task-item:hover,
.mini-tx-item:hover {
  border-color: var(--reward-accent);
}

.mini-task-item .task-info,
.mini-tx-item .tx-info {
  display: flex;
  flex-direction: column;
  min-width: 0;
}

.mini-task-item .task-info strong {
  font-size: 11px;
  color: var(--reward-muted);
  font-weight: 600;
}

.mini-task-item .task-info .task-title,
.mini-tx-item .tx-title {
  font-size: 12px;
  font-weight: 550;
  color: var(--reward-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.pts-badge {
  background: rgba(15, 98, 254, 0.08);
  color: var(--reward-accent);
  font-size: 11px;
  font-weight: 600;
  padding: 2px 6px;
  border-radius: 4px;
  flex-shrink: 0;
}

.mini-tx-item time {
  font-size: 10px;
  color: var(--reward-muted);
}

.tx-pts {
  font-size: 12px;
  font-weight: 600;
  color: #10b981;
  flex-shrink: 0;
}

.tx-pts.negative {
  color: #ef4444;
}

/* Privacy note styling */
.privacy-panel {
  text-align: center;
}

.privacy-icon {
  font-size: 24px;
  color: var(--reward-muted);
  margin-bottom: 10px;
}

.privacy-panel h4 {
  font-size: 14px;
  font-weight: 600;
  color: var(--reward-text);
  margin: 0 0 4px 0;
}

.privacy-panel p {
  font-size: 12px;
  color: var(--reward-muted);
  line-height: 1.5;
  margin: 0;
}

/* Back to me */
.back-to-me-btn {
  background: var(--reward-bg);
  border: 1px solid var(--reward-border);
  color: var(--reward-muted);
  font-size: 11px;
  font-weight: 600;
  padding: 4px 8px;
  border-radius: 4px;
  cursor: pointer;
  transition: all 150ms ease;
  display: inline-flex;
  align-items: center;
}

.back-to-me-btn:hover {
  color: var(--reward-text);
  border-color: var(--reward-accent);
}

/* Utility buttons */
.refresh-btn {
  background: var(--reward-surface);
  border: 1px solid var(--reward-border);
  color: var(--reward-text);
  padding: 8px 14px;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 500;
  cursor: pointer;
  transition: all 150ms ease;
  display: inline-flex;
  align-items: center;
  gap: 8px;
}

.refresh-btn:hover:not(:disabled) {
  background: var(--reward-bg);
  border-color: var(--reward-accent);
}

.refresh-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.empty {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  padding: 40px 20px;
  text-align: center;
  color: var(--reward-muted);
  font-size: 13px;
}
</style>

<style>
/* Hide the global AI mascot on the Rewards page to maintain premium SaaS aesthetics */
.dashboard-layout:has(.rewards-page) .ai-floating-btn.ai-pet {
  display: none !important;
}
</style>
