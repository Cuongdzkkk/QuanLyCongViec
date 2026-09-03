<template>
  <section class="rewards-page">
    <header class="page-header app-shell-page-header">
      <div class="title-with-filter" style="display: flex; align-items: center; gap: 16px; flex: 1;">
        <h1 style="white-space: nowrap; margin-right: 8px;">{{ t('rewards.title') }}</h1>
        
        <!-- Active Season Banner (Marquee) -->
        <div v-if="seasonDashboard.currentSeason" class="active-season-banner" style="flex: 1; padding: 6px 12px; background: linear-gradient(to right, #f0f9ff, #e0f2fe); border: 1px solid #bae6fd; border-radius: 8px; display: flex; align-items: center; gap: 8px; overflow: hidden; white-space: nowrap; box-sizing: border-box; min-width: 0;">
          <div style="width: 24px; height: 24px; background: #38bdf8; border-radius: 6px; display: flex; align-items: center; justify-content: center; color: white; font-size: 11px; flex-shrink: 0; z-index: 2;">
            <i class="fa-solid fa-bullhorn"></i>
          </div>
          <div class="marquee-container" style="flex: 1; margin: 0; padding: 0; overflow: hidden; display: flex; align-items: center; white-space: nowrap;">
            <div class="marquee-content" style="display: inline-block;">
              <span style="color: #0369a1; font-size: 13px; font-weight: 700; margin-right: 24px;">Mùa giải đang hoạt động: {{ seasonDashboard.currentSeason.name }}</span>
              <span style="color: #0284c7; font-size: 12px;">
                <i class="fa-regular fa-clock" style="margin-right: 4px;"></i> {{ formatDate(seasonDashboard.currentSeason.startAt) }} — {{ seasonDashboard.currentSeason.endAt ? formatDate(seasonDashboard.currentSeason.endAt) : 'Không giới hạn' }}
              </span>
            </div>
          </div>
        </div>
      </div>
      <div style="display: flex; gap: 8px;">
        <button class="primary-btn" type="button" @click="openShopModal = true">
          <i class="fa-solid fa-shop"></i> Reward Shop
        </button>
        <button class="refresh-btn" type="button" :disabled="loading" @click="loadRewards">
          <i class="fa-solid fa-rotate" :class="{ 'fa-spin': loading }"></i> {{ loading ? t('rewards.refreshing') : t('rewards.refresh') }}
        </button>
      </div>
    </header>




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
              <div class="user-level-badge" style="position: absolute; top: -5px; right: -10px; background: #0f172a; color: #38bdf8; padding: 2px 6px; border-radius: 8px; font-size: 11px; font-weight: 900; border: 2px solid #38bdf8; z-index: 10; box-shadow: 0 2px 8px rgba(0,0,0,0.3);">
                Lv.{{ top2.level || 1 }}
              </div>
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
              <div class="user-level-badge" style="position: absolute; top: 0; right: -12px; background: #0f172a; color: #f59e0b; padding: 3px 8px; border-radius: 8px; font-size: 13px; font-weight: 900; border: 2px solid #f59e0b; z-index: 10; box-shadow: 0 2px 8px rgba(0,0,0,0.3);">
                Lv.{{ top1.level || 1 }}
              </div>
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
              <div class="user-level-badge" style="position: absolute; top: -5px; right: -10px; background: #0f172a; color: #f97316; padding: 2px 6px; border-radius: 8px; font-size: 11px; font-weight: 900; border: 2px solid #f97316; z-index: 10; box-shadow: 0 2px 8px rgba(0,0,0,0.3);">
                Lv.{{ top3.level || 1 }}
              </div>
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
            <div v-if="!seasonDashboard.currentSeason" class="empty-spaces-flat" style="flex: 1; background: transparent; box-shadow: none;">
              <div class="empty-spaces-icon" aria-hidden="true">
                <i class="fa-solid fa-trophy"></i>
              </div>
              <div class="empty-spaces-copy">
                <h3>Chưa có mùa giải nào</h3>
                <p>Bắt đầu một mùa giải để xếp hạng thành viên.</p>
              </div>
              <button v-if="seasonDashboard.canManage" class="empty-spaces-btn" style="margin-top: 16px;" @click="openSettingsModal = true">
                Cấu hình ngay
              </button>
            </div>
            <div v-else-if="restLeaders.length === 0" class="empty-spaces-flat" style="flex: 1; background: transparent; box-shadow: none;">
              <div class="empty-spaces-icon" aria-hidden="true">
                <i class="fa-solid fa-trophy"></i>
              </div>
              <div class="empty-spaces-copy">
                <h3>Chưa có thành viên xếp hạng</h3>
                <p>Hãy hoàn thành công việc để tích lũy điểm số.</p>
              </div>
              <router-link :to="`/space/${$route.params.spaceId}/tasks`" class="empty-spaces-btn" style="margin-top: 16px; display: inline-flex; align-items: center; justify-content: center; text-decoration: none;">
                Đi tới Công việc
              </router-link>
            </div>
            <table v-else class="rankings-table">
              <tbody>
                <tr v-for="(item, index) in restLeaders" :key="item.userId" class="ranking-row" :class="{ active: selectedUser?.userId === item.userId, 'is-me': item.userId === wallet.userId }" @click="selectUser(item)">
                  <td class="col-rank">
                    <span class="rank-number">#{{ index + 4 < 10 ? '0' + (index + 4) : index + 4 }}</span>
                  </td>
                  <td class="col-user">
                    <div class="user-cell" style="position: relative; display: inline-flex; align-items: center;">
                      <div style="position: relative; display: inline-block;">
                        <UserAvatar :user="{ ...item, fullName: item.userName, id: item.userId }" :size="28" :fontSize="10" />
                        <div class="user-level-badge" style="position: absolute; top: -6px; right: -6px; background: #1e293b; color: white; padding: 1px 4px; border-radius: 4px; font-size: 8px; font-weight: 800; border: 1px solid #94a3b8; z-index: 10;">
                          Lv.{{ item.level || 1 }}
                        </div>
                      </div>
                      <span class="user-name" style="margin-left: 12px;">{{ item.userName }}</span>
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
          <div class="gamification-sidebar" style="display: flex; flex-direction: column; gap: 20px;">
            <!-- Premium User Rank Card -->
            <div class="premium-card cyber-rank-card" style="padding: 20px;">
              <div style="margin-bottom: 20px; display: flex; align-items: baseline; gap: 12px;">
                <div style="font-size: 24px; font-weight: 900; color: #0f172a; letter-spacing: -0.5px;">{{ myRankIndex >= 0 ? '#' + (myRankIndex + 1) : 'Chưa xếp hạng' }}</div>
                <div style="font-size: 15px; font-weight: 700; color: #10b981;" :title="'Ví của bạn: ' + (wallet.totalPoints || 0) + ' pts'">
                  {{ seasonDashboard.currentSeason ? seasonDashboard.mySeasonPoints : wallet.totalPoints || 0 }} pts
                </div>
              </div>
              
              <div style="height: 1px; background: #e2e8f0; margin: 0 -20px 20px; border-bottom: 1px dashed #cbd5e1;"></div>
              
              <div>
                <h4 style="margin: 0 0 8px; font-size: 11px; font-weight: 800; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px;">Cấp độ hiện tại</h4>
                <div style="font-size: 18px; font-weight: 800; color: #0f172a; display: flex; align-items: center; justify-content: space-between; margin-bottom: 12px;">
                  <span><i class="fa-solid fa-meteor" style="color: #f59e0b; margin-right: 6px;"></i>{{ career.title || 'Contributor' }}</span>
                  <span style="font-size: 12px; color: #64748b; background: #f1f5f9; padding: 2px 8px; border-radius: 12px; font-weight: 800;">Lv. {{ career.level || 1 }}</span>
                </div>
                <!-- Premium XP Bar with text inside -->
                <div style="height: 24px; background: #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: inset 0 1px 3px rgba(0,0,0,0.1); position: relative; margin-top: 10px;">
                  <div :style="{ width: `${career.progressPercent || 0}%` }" style="height: 100%; background: linear-gradient(90deg, #3b82f6, #8b5cf6, #ec4899); border-radius: 12px; transition: width 0.5s ease; box-shadow: 0 0 10px rgba(139,92,246,0.5);"></div>
                  <div style="position: absolute; top: 0; left: 0; right: 0; bottom: 0; display: flex; align-items: center; justify-content: center; font-size: 12px; font-weight: 800; color: #0f172a; text-shadow: 0 0 4px rgba(255,255,255,0.8); pointer-events: none;">
                    {{ wallet.totalPoints || 0 }} / {{ (wallet.totalPoints || 0) + pointsToNext }}
                  </div>
                </div>
              </div>
            </div>

            <!-- Premium Sprint Summary -->
            <div class="premium-card cyber-summary-card" style="padding: 20px;">
              <h4 style="margin: 0 0 16px; font-size: 11px; font-weight: 800; color: #94a3b8; text-transform: uppercase; letter-spacing: 1px;">Tóm tắt Sprint</h4>
              
              <div style="display: grid; grid-template-columns: 1fr 1fr 1fr; gap: 12px;">
                <div style="text-align: center; padding: 12px; background: #f8fafc; border-radius: 10px; border: 1px solid #f1f5f9;">
                  <div style="font-size: 10px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">Hoàn thành</div>
                  <div style="font-size: 20px; font-weight: 900; color: #10b981;">{{ seasonDashboard.currentSeason ? (leaderboard.find(u => String(u.userId) === String(wallet.userId))?.completedTasks || 0) : (summary.completedTasks || 0) }}</div>
                </div>
                <div style="text-align: center; padding: 12px; background: #f8fafc; border-radius: 10px; border: 1px solid #f1f5f9;">
                  <div style="font-size: 10px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">Thưởng mốc</div>
                  <div style="font-size: 20px; font-weight: 900; color: #8b5cf6;">{{ seasonDashboard.currentSeason ? 0 : (summary.earlyBonuses || 0) }}</div>
                </div>
                <div style="text-align: center; padding: 12px; background: #f8fafc; border-radius: 10px; border: 1px solid #f1f5f9;">
                  <div style="font-size: 10px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">Tổng điểm</div>
                  <div style="font-size: 20px; font-weight: 900; color: #f59e0b;">{{ seasonDashboard.currentSeason ? (seasonDashboard.mySeasonPoints || 0) : (summary.basePoints + summary.bonusPoints || 0) }}</div>
                </div>
              </div>
            </div>
          </div>

          <!-- YOUR ACTIVITIES Card -->
          <div class="panel activities-card">
            <div class="custom-tabs-header">
              <button class="tab-btn" :class="{ active: activeTab === 'history' }" @click="activeTab = 'history'">Lịch sử điểm</button>
              <button class="tab-btn" :class="{ active: activeTab === 'inventory' }" @click="activeTab = 'inventory'">Túi đồ</button>
            </div>
            
            <div class="custom-tabs-content">
              <div v-if="activeTab === 'history'" class="tab-pane">
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

              <div v-else-if="activeTab === 'inventory'" class="tab-pane">
                <div v-if="myGrants.length === 0" class="empty-list-small">Chưa có phần thưởng nào.</div>
                <div class="mini-tx-list" v-else>
                  <div v-for="grant in myGrants" :key="grant.id" class="mini-tx-item">
                    <div class="tx-info">
                      <div class="tx-title">{{ grant.rewardName }}</div>
                      <time>{{ formatDate(grant.earnedAt) }}</time>
                    </div>
                    <strong class="tx-pts" style="color: #f59e0b;">
                      {{ grant.quantity }}x {{ grant.rewardType }}
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

    <!-- Modals -->
    <el-dialog v-model="openShopModal" width="85%" top="5vh" class="reward-shop-modal">
      <template #header>
        <div style="display: flex; justify-content: space-between; align-items: center; padding-right: 24px;">
          <span style="font-size: 18px; font-weight: 700; color: #0f172a;">Reward Shop</span>
          <div style="font-size: 14px; font-weight: 700; background: #fef08a; padding: 6px 16px; border-radius: 20px; color: #a16207; display: flex; align-items: center; gap: 8px;">
            <i class="fa-solid fa-coins"></i> {{ wallet.totalPoints }} pts
          </div>
        </div>
      </template>

      <el-tabs v-model="shopActiveTab" class="reward-settings-tabs" style="padding: 0 24px 24px;">
        <el-tab-pane label="Cửa hàng" name="store">
          <div class="shop-grid">
            <div v-if="shopItems.length === 0" class="empty-spaces-flat" style="flex: 1; background: transparent; box-shadow: none; grid-column: 1 / -1; min-height: 295px; margin: auto; display: flex; flex-direction: column; align-items: center; justify-content: center;">
              <div class="empty-spaces-icon" aria-hidden="true">
                <i class="fa-solid fa-gift" style="color: var(--color-icon); margin-bottom: 12px;"></i>
              </div>
              <div class="empty-spaces-copy" style="text-align: center;">
                <h3 style="font-size: 16px; font-weight: 700; margin: 0 0 8px;">Chưa có phần thưởng</h3>
                <p style="font-size: 13px; color: var(--color-text-muted); margin: 0;">Shop hiện tại chưa có món quà nào để đổi.</p>
              </div>
            </div>
            <div v-for="item in shopItems" :key="item.id" class="premium-card cyber-reward-card" style="display: flex; flex-direction: column; height: 295px; padding: 0; box-sizing: border-box; overflow: hidden; position: relative; border-radius: 12px; background: white; border: 1px solid #cbd5e1;">
              <div class="cyber-image-area" style="height: 140px; border-radius: 8px 8px 0 0; background: #f8fafc; display: flex; align-items: center; justify-content: center; position: relative; overflow: hidden; border-bottom: 1px solid #e2e8f0; flex-shrink: 0;">
                <img v-if="getRewardImage(item.id)" :src="getRewardImage(item.id)" style="width: 100%; height: 100%; object-fit: cover; position: absolute; inset: 0;" />
                <div v-else style="display: flex; flex-direction: column; align-items: center; justify-content: center; color: #cbd5e1; height: 100%; width: 100%;">
                  <i class="fa-solid fa-image" style="font-size: 32px; margin-bottom: 8px;"></i>
                  <span style="font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">Chưa có ảnh</span>
                </div>
              </div>
              <div style="padding: 12px; display: flex; flex-direction: column; flex: 1;">
                <strong style="font-size: 15px; color: #0f172a; display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 800; letter-spacing: -0.3px;">{{ item.name }}</strong>
                <p v-if="getRewardConfig(item).text" style="margin: 6px 0 12px; color: #475569; font-size: 12.5px; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; line-height: 1.5; flex: 1;">{{ getRewardConfig(item).text }}</p>
                <div style="margin-top: auto; padding-top: 12px; border-top: 1px solid #f1f5f9; display: flex; align-items: center; justify-content: center;">
                  <button class="reward-redeem-btn" :disabled="wallet.totalPoints < (getRewardConfig(item).pointCost ?? item.pointCost) || shopBusy" @click="redeemReward(item)">
                    <i class="fa-solid fa-spinner fa-spin" v-if="shopBusy"></i>
                    <template v-else>
                      <i class="fa-solid fa-cart-shopping" style="font-size: 14px;"></i>
                      <span>{{ getRewardConfig(item).pointCost ?? item.pointCost }} pts</span>
                    </template>
                  </button>
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>
        <el-tab-pane label="Giỏ hàng của tôi" name="cart">
          <div v-if="myGrants.length === 0" class="empty-list-small" style="text-align: center; padding: 40px; color: #64748b;">Chưa có phần thưởng nào.</div>
          <div class="shop-grid" v-else>
            <div v-for="grant in myGrants" :key="grant.id" class="premium-card cyber-reward-card" style="display: flex; flex-direction: column; height: 295px; padding: 0; box-sizing: border-box; overflow: hidden; position: relative; border-radius: 12px; background: white; border: 1px solid #cbd5e1;">
              <div class="cyber-image-area" style="height: 140px; border-radius: 8px 8px 0 0; background: #f8fafc; display: flex; align-items: center; justify-content: center; position: relative; overflow: hidden; border-bottom: 1px solid #e2e8f0; flex-shrink: 0;">
                <img v-if="getRewardImage(grant.rewardDefinitionId)" :src="getRewardImage(grant.rewardDefinitionId)" style="width: 100%; height: 100%; object-fit: cover; position: absolute; inset: 0;" />
                <div v-else style="display: flex; flex-direction: column; align-items: center; justify-content: center; color: #cbd5e1; height: 100%; width: 100%;">
                  <i class="fa-solid fa-image" style="font-size: 32px; margin-bottom: 8px;"></i>
                  <span style="font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">Chưa có ảnh</span>
                </div>
                <div style="position: absolute; top: 8px; right: 8px; background: #ef4444; color: white; padding: 4px 10px; border-radius: 12px; font-size: 13px; font-weight: 900; box-shadow: 0 4px 6px rgba(0,0,0,0.1); border: 2px solid white; z-index: 10;">
                  x{{ grant.quantity }}
                </div>
              </div>
              <div style="padding: 12px; display: flex; flex-direction: column; flex: 1;">
                <strong style="font-size: 15px; color: #0f172a; display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 800; letter-spacing: -0.3px;">{{ grant.rewardName }}</strong>
                <p style="margin: 6px 0 12px; color: #475569; font-size: 12.5px; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; line-height: 1.5; flex: 1;">
                  {{ getRewardConfig(getRewardDefById(grant.rewardDefinitionId))?.text || '' }}
                </p>
                <div style="margin-top: auto; padding-top: 12px; border-top: 1px solid #f1f5f9; display: flex; align-items: center; justify-content: space-between;">
                  <span style="font-size: 11px; font-weight: 600; color: #94a3b8;">{{ formatDate(grant.earnedAt) }}</span>
                  <div style="background: #fef08a; padding: 4px 10px; border-radius: 20px; color: #a16207; font-size: 12px; font-weight: 700; display: flex; align-items: center; gap: 4px;">
                    <i class="fa-solid fa-coins"></i> 
                    <span>{{ getRewardConfig(getRewardDefById(grant.rewardDefinitionId))?.pointCost ?? 0 }} pts</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-dialog>

    <el-drawer v-model="openSettingsModal" :title="t('Gamification.RewardConfiguration', 'Cấu hình phần thưởng')" size="800px" custom-class="reward-settings-drawer" destroy-on-close direction="rtl">
      <el-tabs v-model="settingsActiveTab" class="reward-settings-tabs">
        <el-tab-pane :label="t('Gamification.CyclesSeasons', 'Mùa giải')" name="seasons">
          <div class="manager-columns" style="display: block; padding: 4px 0;">
            <div style="margin-bottom: 24px;">
              <h3 style="font-size: 14px; font-weight: 600; margin-bottom: 12px; color: var(--reward-text);">Danh sách mùa giải</h3>
              <div class="manager-season-list" style="display: flex; flex-direction: column; gap: 12px;">
                <!-- Placeholder / Form Add Season -->
                <div v-if="!isCreatingSeason" @click="isCreatingSeason = true" class="premium-form-wrapper" style="margin-top: 12px; margin-bottom: 12px; display: flex; flex-direction: column; cursor: pointer;">
                  <div style="margin-left: 24px; margin-bottom: -2px; position: relative; z-index: 2; width: fit-content; background: #e2e8f0; color: #94a3b8; padding: 6px 36px 8px; font-size: 12px; font-weight: 800; display: flex; align-items: center; gap: 8px; clip-path: polygon(16px 0, calc(100% - 16px) 0, 100% 100%, 0 100%); letter-spacing: 0.5px; text-transform: uppercase;">
                    <i class="fa-solid fa-store" style="font-size: 14px;"></i> Shop mùa giải
                  </div>
                  <div class="premium-form-body" style="padding: 20px 16px 16px; border: 2px dashed #cbd5e1; border-radius: 12px; display: flex; align-items: center; justify-content: center; background: #f8fafc; position: relative; z-index: 1; min-height: 72px; transition: all 0.2s;">
                    <span style="color: #64748b; font-weight: 600; font-size: 14px;"><i class="fa-solid fa-plus" style="margin-right: 8px;"></i> Tạo mùa giải mới</span>
                  </div>
                </div>
                <div v-else class="premium-form-wrapper" style="margin-top: 12px; margin-bottom: 12px; display: flex; flex-direction: column;">
                  <el-popover placement="bottom-start" :width="540" trigger="click" popper-class="shop-popover">
                    <template #reference>
                      <div style="margin-left: 24px; margin-bottom: -2px; position: relative; z-index: 2; width: fit-content; background: #16a34a; color: white; padding: 6px 36px 8px; font-size: 12px; font-weight: 800; display: flex; align-items: center; gap: 8px; clip-path: polygon(16px 0, calc(100% - 16px) 0, 100% 100%, 0 100%); letter-spacing: 0.5px; text-transform: uppercase; cursor: pointer;">
                        <i class="fa-solid fa-store" style="color: #4ade80; font-size: 14px; text-shadow: 0 0 8px rgba(74, 222, 128, 0.5);"></i> Shop mùa giải
                      </div>
                    </template>
                    <div class="shop-popover-content" style="padding: 4px;">
                      <el-input v-model="shopSearch" placeholder="Tìm kiếm phần thưởng..." clearable style="width: 100%; margin-bottom: 16px;">
                        <template #prefix>
                          <i class="fa-solid fa-magnifying-glass"></i>
                        </template>
                      </el-input>
                      <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; max-height: 310px; overflow-y: auto; padding-right: 4px;">
                        <div v-for="reward in filteredShopRewards" :key="reward.id" class="shop-reward-card" style="border: 1px solid #cbd5e1; border-radius: 8px; overflow: hidden; cursor: pointer; display: flex; flex-direction: column; align-items: center; padding-bottom: 8px; position: relative;" @click="toggleRewardInSeason('new', reward.id)">
                          <div style="width: 100%; height: 110px; background: #f1f5f9; display: flex; align-items: center; justify-content: center; position: relative;">
                            <img v-if="reward.imageUrl" :src="reward.imageUrl" style="width: 100%; height: 100%; object-fit: cover;" />
                            <i v-else class="fa-solid fa-gift" style="font-size: 24px; color: #94a3b8;"></i>
                            <div v-if="isRewardInSeason('new', reward.id)" style="position: absolute; inset: 0; background: rgba(34, 197, 94, 0.2); border: 2px solid #22c55e;">
                              <i class="fa-solid fa-circle-check" style="position: absolute; top: 4px; right: 4px; color: #22c55e; font-size: 16px; background: white; border-radius: 50%;"></i>
                            </div>
                          </div>
                          <span style="font-size: 11px; font-weight: 600; text-align: center; margin-top: 8px; padding: 0 4px; line-height: 1.2; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; color: #0f172a;">{{ reward.name }}</span>
                        </div>
                      </div>
                    </div>
                  </el-popover>
                  <div class="premium-form-body" style="padding: 20px 16px 16px; border: 2px solid #16a34a; border-radius: 12px; display: flex; align-items: center; background: #ffffff; position: relative; z-index: 1; box-shadow: 0 8px 20px -4px rgba(22, 163, 74, 0.15);">
                    <form @submit.prevent="createSeason" style="display: flex; gap: 12px; width: 100%; align-items: center;">
                      <div style="flex: 2; position: relative;">
                        <i class="fa-solid fa-trophy" style="position: absolute; left: 12px; top: 50%; transform: translateY(-50%); color: var(--color-text-muted); font-size: 13px;"></i>
                        <input v-model="seasonForm.name" placeholder="Tên mùa giải..." required class="reward-nexus-input reward-nexus-search" />
                      </div>
                      <input v-model="seasonForm.startAt" type="date" title="Ngày bắt đầu" class="reward-nexus-input" style="flex: 1;" />
                      <input v-model="seasonForm.endAt" type="date" title="Ngày kết thúc" class="reward-nexus-input" style="flex: 1;" />
                      <div style="display: flex; gap: 8px;">
                        <button type="button" @click="isCreatingSeason = false" class="secondary-btn" style="height: 32px; padding: 0 14px; background: #fff; border: 1px solid #e2e8f0; border-radius: 6px; color: #475569; font-weight: 600; font-size: 13px; box-sizing: border-box;">Hủy</button>
                        <button type="submit" :disabled="managerBusy" class="primary-btn" style="height: 32px; padding: 0 16px; border-radius: 6px; font-weight: 600; font-size: 13px; box-shadow: 0 2px 4px rgba(37,99,235,0.2); box-sizing: border-box;">Lưu</button>
                      </div>
                    </form>
                  </div>
                </div>

                <!-- Existing Seasons -->
                <div v-for="season in managerSeasons" :key="season.id" class="premium-form-wrapper" style="margin-top: 12px; margin-bottom: 12px; display: flex; flex-direction: column;">
                  <el-popover placement="bottom-start" :width="540" trigger="click" popper-class="shop-popover">
                    <template #reference>
                      <div style="margin-left: 24px; margin-bottom: -2px; position: relative; z-index: 2; width: fit-content; background: #16a34a; color: white; padding: 6px 36px 8px; font-size: 12px; font-weight: 800; display: flex; align-items: center; gap: 8px; clip-path: polygon(16px 0, calc(100% - 16px) 0, 100% 100%, 0 100%); letter-spacing: 0.5px; text-transform: uppercase; cursor: pointer;">
                        <i class="fa-solid fa-store" style="color: #4ade80; font-size: 14px; text-shadow: 0 0 8px rgba(74, 222, 128, 0.5);"></i> Shop mùa giải
                      </div>
                    </template>
                    <div class="shop-popover-content" style="padding: 4px;">
                      <el-input v-model="shopSearch" placeholder="Tìm kiếm phần thưởng..." clearable style="width: 100%; margin-bottom: 16px;">
                        <template #prefix>
                          <i class="fa-solid fa-magnifying-glass"></i>
                        </template>
                      </el-input>
                      <div style="display: grid; grid-template-columns: repeat(3, 1fr); gap: 12px; max-height: 310px; overflow-y: auto; padding-right: 4px;">
                        <div v-for="reward in filteredShopRewards" :key="reward.id" class="shop-reward-card" style="border: 1px solid #cbd5e1; border-radius: 8px; overflow: hidden; cursor: pointer; display: flex; flex-direction: column; align-items: center; padding-bottom: 8px; position: relative;" @click="toggleRewardInSeason(season.id, reward.id)">
                          <div style="width: 100%; height: 110px; background: #f8fafc; border: 1px solid #e2e8f0; display: flex; align-items: center; justify-content: center; position: relative; border-radius: 6px; overflow: hidden;">
                            <img v-if="getRewardImage(reward.id)" :src="getRewardImage(reward.id)" style="width: 100%; height: 100%; object-fit: cover;" />
                            <div v-else style="display: flex; flex-direction: column; align-items: center; justify-content: center; color: #cbd5e1; height: 100%; width: 100%;">
                              <i class="fa-solid fa-image" style="font-size: 20px;"></i>
                            </div>
                            <div v-if="isRewardInSeason(season.id, reward.id)" style="position: absolute; inset: 0; background: rgba(34, 197, 94, 0.2); border: 2px solid #22c55e;">
                              <i class="fa-solid fa-circle-check" style="position: absolute; top: 4px; right: 4px; color: #22c55e; font-size: 16px; background: white; border-radius: 50%;"></i>
                            </div>
                          </div>
                          <span style="font-size: 11px; font-weight: 600; text-align: center; margin-top: 8px; padding: 0 4px; line-height: 1.2; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; color: #0f172a;">{{ reward.name }}</span>
                        </div>
                      </div>
                    </div>
                  </el-popover>
                  <div class="premium-card manager-season-row" style="display: flex; justify-content: space-between; align-items: center; padding: 16px; position: relative; z-index: 1; box-shadow: 0 4px 6px -1px rgba(0, 0, 0, 0.1); border: 2px solid #16a34a; border-radius: 12px; background: #ffffff;">
                    <div style="display: flex; align-items: center; gap: 16px;">
                      <div class="season-icon-wrapper" style="width: 44px; height: 44px; border-radius: 12px; background: linear-gradient(135deg, #f0fdf4, #dcfce7); color: #16a34a; display: flex; align-items: center; justify-content: center; font-size: 20px;">
                        <i class="fa-solid fa-flag-checkered"></i>
                      </div>
                      <div>
                        <strong style="font-size: 15px; color: #0f172a; font-weight: 600;">{{ season.name }}</strong>
                        <small style="display: block; color: #64748b; font-size: 12.5px; margin-top: 4px; font-weight: 500;">
                          <i class="fa-regular fa-calendar" style="margin-right: 4px;"></i>
                          {{ formatDate(season.startAt) }} {{ season.endAt ? '— ' + formatDate(season.endAt) : '— Không giới hạn' }}
                        </small>
                      </div>
                    </div>
                    <div class="manager-row-actions" style="display: flex; gap: 10px; align-items: center;">
                      <button v-if="season.status !== 'Active' && season.status !== 'Paused'" type="button" class="empty-spaces-btn" @click="activateSeason(season)">Bắt đầu</button>
                      <button v-if="season.status === 'Paused'" type="button" class="empty-spaces-btn" @click="activateSeason(season)">Tiếp tục</button>
                      <button v-if="season.status === 'Active'" type="button" class="empty-spaces-btn" @click="pauseSeason(season)">Tạm dừng</button>
                      <el-popconfirm v-if="season.status === 'Active' || season.status === 'Paused'" title="Chắc chắn kết thúc mùa giải?" confirm-button-text="Đồng ý" cancel-button-text="Hủy" @confirm="closeSeason(season)">
                        <template #reference>
                          <button type="button" class="empty-spaces-btn" style="color: #ef4444; border-color: #ef4444;">Kết thúc</button>
                        </template>
                      </el-popconfirm>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>

        <el-tab-pane :label="t('Gamification.RewardCatalog', 'Danh mục phần thưởng')" name="catalog">
          <div class="manager-columns" style="display: block; padding: 4px 0;">
            <div style="margin-bottom: 24px;">
              <h3 style="font-size: 14px; font-weight: 600; margin-bottom: 12px; color: var(--reward-text);">Danh mục phần thưởng đã tạo</h3>
              <div class="manager-grants" style="margin-bottom: 16px; display: grid; grid-template-columns: repeat(3, 1fr); gap: 16px; align-items: start;">
                <!-- Placeholder Add Reward -->
                <div v-if="!isCreatingReward" @click="isCreatingReward = true" class="premium-placeholder cyber-reward-card" style="display: flex; flex-direction: column; align-items: center; justify-content: center; height: 295px; padding: 10px; box-sizing: border-box; border: 2px dashed #cbd5e1; border-radius: 12px; background: transparent; cursor: pointer;">
                  <div class="cyber-icon-wrapper" style="background: transparent; color: #94a3b8; font-size: 28px; margin-bottom: 12px;">
                    <i class="fa-solid fa-plus-circle"></i>
                  </div>
                  <span style="font-size: 14px; font-weight: 600; color: #94a3b8;">Thêm phần thưởng</span>
                  <input type="file" ref="quickImageInputRef" @change="handleQuickImageSelect" style="display: none" accept="image/*" />
                </div>

                <!-- Form Add Reward (Compact, Fits 1 cell) -->
                <div v-else class="premium-form cyber-reward-card" style="display: flex; flex-direction: column; overflow: hidden; height: 295px; box-sizing: border-box; border: 1px solid #cbd5e1; border-radius: 12px; background: white;">
                  <form @submit.prevent="createReward" style="display: flex; flex-direction: column; height: 100%;">
                    <!-- Select Image Area -->
                    <input type="file" ref="rewardImageInput" @change="handleRewardImageSelect" style="display: none" accept="image/*" />
                    <div class="cyber-image-area" @click="!rewardForm.imagePreview && triggerImageSelect()" @mousedown="startDrag" @mousemove="onDrag" @mouseup="stopDrag" @mouseleave="stopDrag" style="height: 140px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; flex-direction: column; align-items: center; justify-content: center; position: relative; overflow: hidden; flex-shrink: 0; user-select: none;">
                      <img v-if="rewardForm.imagePreview" ref="imageRef" :src="rewardForm.imagePreview" :style="{ transform: `translateY(${cropState.offsetY}px)`, cursor: cropState.isDragging ? 'grabbing' : 'grab' }" style="width: 100%; height: auto; position: absolute; top: 0; left: 0;" draggable="false" />
                      
                      <div v-if="!rewardForm.imagePreview" style="display: flex; flex-direction: column; align-items: center; color: #94a3b8; z-index: 1; cursor: pointer;">
                        <i class="fa-solid fa-gift" style="font-size: 24px; margin-bottom: 4px;"></i>
                        <span style="font-size: 11px; font-weight: 600;">Tải ảnh bìa</span>
                      </div>
                      <div v-else style="position: absolute; top: 8px; left: 8px; z-index: 10;" @click.stop="triggerImageSelect" title="Đổi ảnh">
                        <div style="width: 28px; height: 28px; border-radius: 6px; background: rgba(15, 23, 42, 0.7); display: flex; align-items: center; justify-content: center; cursor: pointer; border: 1px solid rgba(255, 255, 255, 0.2); color: white;">
                          <i class="fa-solid fa-image" style="font-size: 13px;"></i>
                        </div>
                      </div>

                      <!-- Settings Gear (Create Mode) -->
                      <div style="position: absolute; top: 8px; right: 8px; z-index: 10;" @click.stop>
                        <el-popover placement="left-start" :width="280" trigger="click">
                          <template #reference>
                            <div style="width: 28px; height: 28px; border-radius: 6px; background: rgba(15, 23, 42, 0.7); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; cursor: pointer; border: 1px solid rgba(255, 255, 255, 0.2); color: white; transition: all 0.2s;">
                              <i class="fa-solid fa-gear" style="font-size: 13px;"></i>
                            </div>
                          </template>
                          <div style="padding: 4px;">
                            <h4 style="font-size: 13px; font-weight: 600; margin-bottom: 12px; color: #0f172a; border-bottom: 1px solid #e2e8f0; padding-bottom: 8px;">Cài đặt phần thưởng</h4>
                            <div style="display: flex; flex-direction: column;">
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="rewardForm.usePoints" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-coins" style="color: #eab308; width: 16px;"></i> Đổi bằng điểm
                              </label>
                              <input v-if="rewardForm.usePoints" v-model="rewardForm.pointCost" type="number" placeholder="Số điểm..." class="reward-nexus-input" style="width: calc(100% - 20px); height: 28px !important; margin-bottom: 12px; margin-left: 20px;" />
                              
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="rewardForm.useLevel" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-arrow-up-right-dots" style="color: #3b82f6; width: 16px;"></i> Đạt mốc Cấp độ
                              </label>
                              <input v-if="rewardForm.useLevel" v-model="rewardForm.levelRequired" type="number" placeholder="Cấp độ tối thiểu..." class="reward-nexus-input" style="width: calc(100% - 20px); height: 28px !important; margin-bottom: 12px; margin-left: 20px;" />
                              
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="rewardForm.useTop" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-crown" style="color: #f59e0b; width: 16px;"></i> Đạt Top xếp hạng
                              </label>
                              <input v-if="rewardForm.useTop" v-model="rewardForm.topRequired" type="number" placeholder="Top N (vd: 3)..." class="reward-nexus-input" style="width: calc(100% - 20px); height: 28px !important; margin-left: 20px; margin-bottom: 12px;" />
                              
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="rewardForm.deductLeaderboard" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-chart-line" style="color: #ef4444; width: 16px;"></i> Trừ điểm Bảng xếp hạng
                              </label>
                            </div>
                          </div>
                        </el-popover>
                      </div>

                      <div class="cyber-overlay-hover"></div>
                    </div>
                    
                    <div style="padding: 12px; display: flex; flex-direction: column; flex: 1; box-sizing: border-box;">
                      <input v-model="rewardForm.name" placeholder="Tên quà (vd: Discord Nitro)" required class="reward-nexus-input" style="margin-bottom: 6px; font-weight: 600;" />
                      <textarea v-model="rewardForm.description" placeholder="Mô tả..." rows="1" class="reward-nexus-input" style="margin-bottom: 8px; flex: 1;"></textarea>
                      
                      <div style="display: flex; justify-content: space-between; gap: 8px; margin-top: auto;">
                        <button type="button" @click="isCreatingReward = false" class="secondary-btn" style="flex: 1; padding: 4px 0; background: transparent; color: #ef4444; border: 1px solid #ef4444; border-radius: 6px; font-weight: 600; font-size: 11px; cursor: pointer;">Hủy</button>
                        <button type="submit" :disabled="managerBusy" class="secondary-btn" style="flex: 1; padding: 4px 0; background: transparent; color: #3b82f6; border: 1px solid #3b82f6; border-radius: 6px; font-weight: 600; font-size: 11px; cursor: pointer;">Lưu</button>
                      </div>
                    </div>
                  </form>
                </div>

                <!-- Existing Rewards -->
                <div v-for="reward in seasonDashboard.availableRewards" :key="reward.id">
                  
                  <!-- EDIT MODE -->
                  <div v-if="editingRewardId === reward.id" class="premium-form cyber-reward-card" style="display: flex; flex-direction: column; overflow: hidden; height: 295px; box-sizing: border-box; border: 1px solid #cbd5e1; border-radius: 12px; background: white;">
                    <form @submit.prevent="saveEditReward(reward)" style="display: flex; flex-direction: column; height: 100%;">
                      <div class="cyber-image-area" @click="triggerImageUpdate(reward)" style="height: 140px; background: #f8fafc; border-bottom: 1px solid #e2e8f0; display: flex; flex-direction: column; align-items: center; justify-content: center; position: relative; overflow: hidden; flex-shrink: 0; cursor: pointer;">
                        <img v-if="getRewardImage(reward.id)" :src="getRewardImage(reward.id)" style="width: 100%; height: 100%; object-fit: cover; position: absolute; inset: 0;" />
                        <div v-else style="display: flex; flex-direction: column; align-items: center; color: #cbd5e1; z-index: 1;">
                          <i class="fa-solid fa-image" style="font-size: 32px; margin-bottom: 8px;"></i>
                          <span style="font-size: 11px; font-weight: 600; text-transform: uppercase;">Đổi ảnh</span>
                        </div>
                      </div>
                      <div style="padding: 12px; display: flex; flex-direction: column; flex: 1; box-sizing: border-box;">
                        <input v-model="editRewardForm.name" placeholder="Tên quà (vd: Discord Nitro)" required class="reward-nexus-input" style="margin-bottom: 6px; font-weight: 600;" />
                        <textarea v-model="editRewardForm.description" placeholder="Mô tả..." rows="1" class="reward-nexus-input" style="margin-bottom: 8px; flex: 1;"></textarea>
                        <div style="display: flex; justify-content: space-between; gap: 8px; margin-top: auto;">
                          <button type="button" @click="cancelEditReward" class="secondary-btn" style="flex: 1; padding: 4px 0; background: transparent; color: #ef4444; border: 1px solid #ef4444; border-radius: 6px; font-weight: 600; font-size: 11px; cursor: pointer;">Hủy</button>
                          <button type="submit" class="secondary-btn" style="flex: 1; padding: 4px 0; background: transparent; color: #3b82f6; border: 1px solid #3b82f6; border-radius: 6px; font-weight: 600; font-size: 11px; cursor: pointer;">Lưu</button>
                        </div>
                      </div>
                    </form>
                  </div>

                  <!-- DISPLAY MODE -->
                  <div v-else class="premium-card cyber-reward-card" style="display: flex; flex-direction: column; height: 295px; padding: 0; box-sizing: border-box; overflow: hidden; position: relative; border-radius: 12px; border: 1px solid #cbd5e1;">
                    <!-- Top Image Area -->
                    <div class="cyber-image-area" @click="triggerImageUpdate(reward)" style="height: 140px; border-radius: 8px 8px 0 0; background: #f8fafc; display: flex; align-items: center; justify-content: center; position: relative; overflow: hidden; border-bottom: 1px solid #e2e8f0; flex-shrink: 0; cursor: pointer;">
                      <img v-if="getRewardImage(reward.id)" :src="getRewardImage(reward.id)" style="width: 100%; height: 100%; object-fit: cover; position: absolute; inset: 0;" />
                      <div v-else style="display: flex; flex-direction: column; align-items: center; justify-content: center; color: #cbd5e1; height: 100%; width: 100%;">
                        <i class="fa-solid fa-image" style="font-size: 32px; margin-bottom: 8px;"></i>
                        <span style="font-size: 11px; font-weight: 600; text-transform: uppercase; letter-spacing: 0.5px;">Chưa có ảnh</span>
                      </div>

                      <!-- Settings Gear -->
                      <div style="position: absolute; top: 8px; right: 8px; z-index: 10;" @click.stop>
                        <el-popover placement="left-start" :width="280" trigger="click">
                          <template #reference>
                            <div style="width: 28px; height: 28px; border-radius: 6px; background: rgba(15, 23, 42, 0.7); backdrop-filter: blur(4px); display: flex; align-items: center; justify-content: center; cursor: pointer; border: 1px solid rgba(255, 255, 255, 0.2); color: white; transition: all 0.2s;">
                              <i class="fa-solid fa-gear" style="font-size: 13px;"></i>
                            </div>
                          </template>
                          <div style="padding: 4px;">
                            <h4 style="font-size: 13px; font-weight: 600; margin-bottom: 12px; color: #0f172a; border-bottom: 1px solid #e2e8f0; padding-bottom: 8px;">Cài đặt phần thưởng</h4>
                            <div style="display: flex; flex-direction: column;">
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="getRewardConfig(reward).usePoints" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-coins" style="color: #eab308; width: 16px;"></i> Đổi bằng điểm
                              </label>
                              <input v-if="getRewardConfig(reward).usePoints" v-model="getRewardConfig(reward).pointCost" type="number" placeholder="Số điểm..." class="reward-nexus-input" style="width: calc(100% - 20px); height: 28px !important; margin-bottom: 12px; margin-left: 20px;" />
                              
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="getRewardConfig(reward).useLevel" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-arrow-up-right-dots" style="color: #3b82f6; width: 16px;"></i> Đạt mốc Cấp độ
                              </label>
                              <input v-if="getRewardConfig(reward).useLevel" v-model="getRewardConfig(reward).levelRequired" type="number" placeholder="Cấp độ tối thiểu..." class="reward-nexus-input" style="width: calc(100% - 20px); height: 28px !important; margin-bottom: 12px; margin-left: 20px;" />
                              
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="getRewardConfig(reward).useTop" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-crown" style="color: #f59e0b; width: 16px;"></i> Đạt Top xếp hạng
                              </label>
                              <input v-if="getRewardConfig(reward).useTop" v-model="getRewardConfig(reward).topRequired" type="number" placeholder="Top N (vd: 3)..." class="reward-nexus-input" style="width: calc(100% - 20px); height: 28px !important; margin-left: 20px; margin-bottom: 12px;" />
                              
                              <label style="display: flex; align-items: center; gap: 8px; font-size: 13px; margin-bottom: 8px; cursor: pointer;">
                                <input type="checkbox" v-model="getRewardConfig(reward).deductLeaderboard" style="accent-color: var(--color-accent);" /> <i class="fa-solid fa-chart-line" style="color: #ef4444; width: 16px;"></i> Trừ điểm Bảng xếp hạng
                              </label>
                              <button type="button" class="reward-nexus-btn btn-primary" style="width: 100%; margin-top: 12px; height: 32px !important; font-size: 13px;" @click="saveEditReward(reward)">Lưu cài đặt</button>
                            </div>
                          </div>
                        </el-popover>
                      </div>
                    </div>
                    <!-- Bottom Text Area -->
                    <div style="padding: 12px; display: flex; flex-direction: column; flex: 1; cursor: pointer;" @click="startEditReward(reward)">
                      <strong style="font-size: 15px; color: #0f172a; display: block; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 800; letter-spacing: -0.3px; margin-bottom: 6px;" title="Sửa thông tin">{{ reward.name }}</strong>
                      <p v-if="parseDescription(reward.description).text" style="margin: 0 0 12px; color: #475569; font-size: 12.5px; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; line-height: 1.5; flex: 1;" title="Sửa thông tin">{{ parseDescription(reward.description).text }}</p>
                      <div style="margin-top: auto; padding-top: 12px; border-top: 1px solid #f1f5f9; display: flex; align-items: center; justify-content: space-between;" @click.stop>
                        <span style="font-size: 10px; font-weight: 800; color: #16a34a; background: #f0fdf4; padding: 4px 8px; border-radius: 4px; text-transform: uppercase; letter-spacing: 0.5px; border: 1px solid #dcfce7;">{{ reward.rewardType || 'GIFT' }}</span>
                        <span style="font-size: 11px; font-weight: 600; color: #94a3b8;"><i class="fa-regular fa-clock" style="margin-right: 4px;"></i>{{ formatDate(reward.startAt || Date.now()) }}</span>
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </el-tab-pane>

        <el-tab-pane :label="t('Gamification.PointRules', 'Quy tắc điểm')" name="rules">
          <div class="manager-columns" style="display: block; padding: 4px 0;">
            <div style="margin-bottom: 24px;">
              <el-tabs type="card" class="premium-tabs">
                <!-- ================= TAB CẤU HÌNH ĐIỂM (POINTS) ================= -->
                <el-tab-pane label="Cấu Hình Điểm (Points)">
                  <div style="background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 16px; margin-bottom: 20px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                      <strong style="color: #0f172a;">Công thức cốt lõi (Points):</strong>
                      <el-button :type="pointFormulaEditMode ? 'primary' : 'default'" size="small" @click="pointFormulaEditMode = !pointFormulaEditMode">
                        <i class="fa-solid fa-pen-to-square" style="margin-right: 6px;"></i>
                        {{ pointFormulaEditMode ? 'Lưu Công Thức' : 'Chỉnh Sửa' }}
                      </el-button>
                    </div>
                    
                    <div class="formula-builder" :class="{ 'edit-mode': pointFormulaEditMode }" style="display: flex; gap: 8px; flex-wrap: wrap; align-items: center; font-size: 14px; font-weight: 600;">
                      <div class="formula-prefix">Tổng Điểm =</div>
                      
                      <draggable 
                        v-model="pointRules.PointConfig.sequence" 
                        class="formula-sequence" 
                        style="display: flex; gap: 8px; align-items: center; flex-wrap: wrap;" 
                        :disabled="!pointFormulaEditMode" 
                        draggable=".formula-block" 
                        animation="200"
                        @end="cleanupSequence(pointRules.PointConfig)"
                      >
                        <template #item="{ element, index }">
                          <div v-if="['+', '*', '-'].includes(element)" class="formula-op" :style="pointFormulaEditMode ? 'cursor: pointer; padding: 4px 8px; font-weight: bold; color: #3b82f6;' : 'padding: 4px 8px;'" @click="pointFormulaEditMode && toggleOperator(pointRules.PointConfig, index)">
                            {{ element === '*' ? 'x' : element }}
                          </div>
                          <div v-else class="formula-block" :style="pointFormulaEditMode ? 'cursor: grab; box-shadow: 0 2px 4px rgba(0,0,0,0.1);' : ''" style="background: #dbeafe; color: #1e40af; padding: 6px 12px; border-radius: 6px; border: 1px solid #bfdbfe; display: flex; align-items: center; gap: 6px;">
                            <i v-if="pointFormulaEditMode" class="fa-solid fa-grip-vertical" style="opacity: 0.5;"></i>
                            {{ getBlockName(element) }}
                          </div>
                        </template>
                      </draggable>
                      
                      <div v-if="pointRules.PointConfig.sequence.length === 0" style="color: #94a3b8; font-style: italic; font-weight: normal; margin-left: 8px;">(Chưa có thành phần nào)</div>
                    </div>
                    <div v-if="pointFormulaEditMode" style="margin-top: 12px; font-size: 12px; color: #ef4444; font-weight: 500;">* Chế độ chỉnh sửa: Kéo thả các khối để thay đổi vị trí. Click vào các dấu toán học (+, -, x) để thay đổi.</div>
                  </div>

                  <div class="premium-card" style="padding: 20px;">
                    <div style="display: flex; flex-direction: column; gap: 16px;">
                      
                      <!-- Phần 1: Độ khó -->
                      <div style="margin-bottom: 8px;">
                        <div style="font-size: 11px; font-weight: 800; color: #64748b; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;">1. Điểm Độ Khó</div>
                        
                        <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                          <div>
                            <div style="display: flex; align-items: center; gap: 8px;">
                              <el-switch v-model="pointRules.PointConfig.enableStoryPoints" size="small" @change="v => toggleBlock(pointRules.PointConfig, 'storyPoints', v)" />
                              <strong style="color: #0f172a; font-size: 14px; font-weight: 600;">Hệ số Story Points</strong>
                            </div>
                            <div style="color: #64748b; font-size: 12px; margin-top: 4px;">Nếu Task CÓ Story Point, điểm = (Số SP) x (Hệ số này).</div>
                          </div>
                          <div style="width: 120px;" v-if="pointRules.PointConfig.enableStoryPoints">
                            <el-input-number v-model="pointRules.storyPointMultiplier" :min="0" :step="5" size="large" style="width: 100%" />
                          </div>
                        </div>
                        
                        <div style="display: flex; align-items: center; justify-content: space-between;">
                          <div>
                            <div style="display: flex; align-items: center; gap: 8px;">
                              <el-switch v-model="pointRules.PointConfig.enableBase" size="small" @change="v => toggleBlock(pointRules.PointConfig, 'base', v)" />
                              <strong style="color: #0f172a; font-size: 14px; font-weight: 600;">Điểm Cơ Bản (Base)</strong>
                            </div>
                            <div style="color: #64748b; font-size: 12px; margin-top: 4px;">Nếu Task KHÔNG CÓ SP, sẽ nhận mức điểm cố định này.</div>
                          </div>
                          <div style="width: 120px;" v-if="pointRules.PointConfig.enableBase">
                            <el-input-number v-model="pointRules.basePoints" :min="0" :step="10" size="large" style="width: 100%" />
                          </div>
                        </div>
                      </div>
                      
                      <el-divider style="margin: 4px 0;" />
                      
                      <!-- Phần 2: Thưởng Ưu Tiên -->
                      <div style="margin-bottom: 8px;">
                        <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
                          <el-switch v-model="pointRules.PointConfig.enablePriorityBonus" size="small" @change="v => toggleBlock(pointRules.PointConfig, 'priorityBonus', v)" />
                          <div style="font-size: 11px; font-weight: 800; color: #64748b; text-transform: uppercase; letter-spacing: 1px;">2. Thưởng Mức độ Ưu tiên</div>
                        </div>
                        
                        <div v-if="pointRules.PointConfig.enablePriorityBonus" style="display: flex; flex-direction: column; gap: 12px;">
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Thấp (Low)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.priorityBonus.Low" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Trung bình (Medium)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.priorityBonus.Normal" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Cao (High)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.priorityBonus.High" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Khẩn cấp (Urgent)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.priorityBonus.Urgent" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                        </div>
                      </div>

                      <el-divider style="margin: 4px 0;" />
                      
                      <!-- Phần 3: Thưởng Hoàn Thành Sớm -->
                      <div style="margin-bottom: 8px;">
                        <div style="display: flex; align-items: center; justify-content: space-between;">
                          <div>
                            <div style="display: flex; align-items: center; gap: 8px;">
                              <el-switch v-model="pointRules.PointConfig.enableEarlyBonus" size="small" @change="v => toggleBlock(pointRules.PointConfig, 'earlyBonus', v)" />
                              <strong style="color: #0f172a; font-size: 14px; font-weight: 600;">Thưởng Hoàn Thành Sớm (%)</strong>
                            </div>
                            <div style="color: #64748b; font-size: 12px; margin-top: 4px;">Cộng thêm % tổng điểm nếu hoàn thành trước Deadline 24h.</div>
                          </div>
                          <div style="width: 120px;" v-if="pointRules.PointConfig.enableEarlyBonus">
                            <el-input-number v-model="pointRules.earlyBonusPercent" :min="0" :max="100" :step="5" size="large" style="width: 100%" />
                          </div>
                        </div>
                      </div>

                    </div>
                  </div>
                </el-tab-pane>

                <!-- ================= TAB CẤU HÌNH KINH NGHIỆM (EXP) ================= -->
                <el-tab-pane label="Cấu Hình Kinh Nghiệm (EXP)">
                  <div style="background: #fdf4ff; border: 1px solid #fbcfe8; border-radius: 8px; padding: 16px; margin-bottom: 20px;">
                    <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                      <strong style="color: #9f1239;">Công thức cốt lõi (EXP):</strong>
                      <el-button :type="expFormulaEditMode ? 'danger' : 'default'" size="small" @click="expFormulaEditMode = !expFormulaEditMode">
                        <i class="fa-solid fa-pen-to-square" style="margin-right: 6px;"></i>
                        {{ expFormulaEditMode ? 'Lưu Công Thức' : 'Chỉnh Sửa' }}
                      </el-button>
                    </div>
                    
                    <div class="formula-builder exp-builder" :class="{ 'edit-mode': expFormulaEditMode }" style="display: flex; gap: 8px; flex-wrap: wrap; align-items: center; font-size: 14px; font-weight: 600;">
                      <div class="formula-prefix" style="color: #9f1239;">Tổng EXP =</div>
                      
                      <draggable 
                        v-model="pointRules.ExpConfig.sequence" 
                        class="formula-sequence" 
                        style="display: flex; gap: 8px; align-items: center; flex-wrap: wrap;" 
                        :disabled="!expFormulaEditMode" 
                        draggable=".formula-block" 
                        animation="200"
                        @end="cleanupSequence(pointRules.ExpConfig)"
                      >
                        <template #item="{ element, index }">
                          <div v-if="['+', '*', '-'].includes(element)" class="formula-op op-exp" :style="expFormulaEditMode ? 'cursor: pointer; padding: 4px 8px; font-weight: bold; color: #e11d48;' : 'padding: 4px 8px; color: #be185d;'" @click="expFormulaEditMode && toggleOperator(pointRules.ExpConfig, index)">
                            {{ element === '*' ? 'x' : element }}
                          </div>
                          <div v-else class="formula-block block-exp" :style="expFormulaEditMode ? 'cursor: grab; box-shadow: 0 2px 4px rgba(0,0,0,0.1);' : ''" style="background: #fce7f3; color: #9f1239; padding: 6px 12px; border-radius: 6px; border: 1px solid #fbcfe8; display: flex; align-items: center; gap: 6px;">
                            <i v-if="expFormulaEditMode" class="fa-solid fa-grip-vertical" style="opacity: 0.5;"></i>
                            {{ getBlockName(element) }}
                          </div>
                        </template>
                      </draggable>
                      
                      <div v-if="pointRules.ExpConfig.sequence.length === 0" style="color: #f43f5e; font-style: italic; font-weight: normal; margin-left: 8px;">(Chưa có thành phần nào)</div>
                    </div>
                    <div v-if="expFormulaEditMode" style="margin-top: 12px; font-size: 12px; color: #ef4444; font-weight: 500;">* Chế độ chỉnh sửa: Kéo thả các khối để thay đổi vị trí. Click vào các dấu toán học (+, -, x) để thay đổi.</div>
                  </div>

                  <div class="premium-card" style="padding: 20px; border-top: 4px solid #fecdd3;">
                    <div style="display: flex; flex-direction: column; gap: 16px;">
                      
                      <!-- Phần 1: EXP Độ Khó -->
                      <div style="margin-bottom: 8px;">
                        <div style="font-size: 11px; font-weight: 800; color: #9f1239; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;">1. EXP Độ Khó</div>
                        
                        <div style="display: flex; align-items: center; justify-content: space-between; margin-bottom: 16px;">
                          <div>
                            <div style="display: flex; align-items: center; gap: 8px;">
                              <el-switch v-model="pointRules.ExpConfig.enableStoryPoints" size="small" @change="v => toggleBlock(pointRules.ExpConfig, 'storyPoints', v)" />
                              <strong style="color: #0f172a; font-size: 14px; font-weight: 600;">Hệ số Story Points (EXP)</strong>
                            </div>
                            <div style="color: #64748b; font-size: 12px; margin-top: 4px;">Nếu Task CÓ Story Point, EXP = (Số SP) x (Hệ số này).</div>
                          </div>
                          <div style="width: 120px;" v-if="pointRules.ExpConfig.enableStoryPoints">
                            <el-input-number v-model="pointRules.storyExpMultiplier" :min="0" :step="5" size="large" style="width: 100%" />
                          </div>
                        </div>
                        
                        <div style="display: flex; align-items: center; justify-content: space-between;">
                          <div>
                            <div style="display: flex; align-items: center; gap: 8px;">
                              <el-switch v-model="pointRules.ExpConfig.enableBase" size="small" @change="v => toggleBlock(pointRules.ExpConfig, 'base', v)" />
                              <strong style="color: #0f172a; font-size: 14px; font-weight: 600;">Kinh Nghiệm Cơ Bản (Base EXP)</strong>
                            </div>
                            <div style="color: #64748b; font-size: 12px; margin-top: 4px;">Nếu Task KHÔNG CÓ SP, sẽ nhận mức EXP cố định này.</div>
                          </div>
                          <div style="width: 120px;" v-if="pointRules.ExpConfig.enableBase">
                            <el-input-number v-model="pointRules.baseExp" :min="0" :step="5" size="large" style="width: 100%" />
                          </div>
                        </div>
                      </div>
                      
                      <el-divider style="margin: 4px 0;" />
                      
                      <!-- Phần 2: Thưởng Ưu Tiên EXP -->
                      <div style="margin-bottom: 8px;">
                        <div style="display: flex; align-items: center; gap: 8px; margin-bottom: 12px;">
                          <el-switch v-model="pointRules.ExpConfig.enablePriorityBonus" size="small" @change="v => toggleBlock(pointRules.ExpConfig, 'priorityBonus', v)" />
                          <div style="font-size: 11px; font-weight: 800; color: #9f1239; text-transform: uppercase; letter-spacing: 1px;">2. Thưởng Mức độ Ưu tiên (EXP)</div>
                        </div>
                        
                        <div v-if="pointRules.ExpConfig.enablePriorityBonus" style="display: flex; flex-direction: column; gap: 12px;">
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Thấp (Low)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.expPriorityBonus.Low" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Trung bình (Medium)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.expPriorityBonus.Normal" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Cao (High)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.expPriorityBonus.High" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                          <div style="display: flex; align-items: center; justify-content: space-between;">
                            <div style="color: #0f172a; font-size: 14px; font-weight: 500;">Khẩn cấp (Urgent)</div>
                            <div style="width: 120px;"><el-input-number v-model="pointRules.expPriorityBonus.Urgent" :min="0" :step="5" size="small" style="width: 100%" /></div>
                          </div>
                        </div>
                      </div>

                      <el-divider style="margin: 4px 0;" />
                      
                      <!-- Phần 3: Thưởng Hoàn Thành Sớm EXP -->
                      <div style="margin-bottom: 8px;">
                        <div style="display: flex; align-items: center; justify-content: space-between;">
                          <div>
                            <div style="display: flex; align-items: center; gap: 8px;">
                              <el-switch v-model="pointRules.ExpConfig.enableEarlyBonus" size="small" @change="v => toggleBlock(pointRules.ExpConfig, 'earlyBonus', v)" />
                              <strong style="color: #0f172a; font-size: 14px; font-weight: 600;">Thưởng Hoàn Thành Sớm (% EXP)</strong>
                            </div>
                            <div style="color: #64748b; font-size: 12px; margin-top: 4px;">Cộng thêm % tổng EXP nếu hoàn thành trước Deadline 24h.</div>
                          </div>
                          <div style="width: 120px;" v-if="pointRules.ExpConfig.enableEarlyBonus">
                            <el-input-number v-model="pointRules.expEarlyBonusPercent" :min="0" :max="100" :step="5" size="large" style="width: 100%" />
                          </div>
                        </div>
                      </div>

                    </div>
                  </div>
                </el-tab-pane>
              </el-tabs>

              <div style="margin-top: 24px; text-align: right;">
                <button type="button" @click="savePointRules" class="primary-btn" style="padding: 10px 24px; border-radius: 8px; font-weight: 600; box-shadow: 0 4px 6px rgba(37,99,235,0.2);">
                  <i class="fa-solid fa-save" style="margin-right: 8px;"></i> Lưu Cấu Hình
                </button>
              </div>

            </div>
          </div>
        </el-tab-pane>

        <!-- NEW: Cấp độ & Kinh nghiệm Tab -->
        <el-tab-pane label="Cấp độ & Kinh nghiệm" name="levels">
          <div class="manager-columns" style="display: block; padding: 4px 0;">
            <div style="margin-bottom: 24px;">
              <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 12px;">
                <h3 style="font-size: 14px; font-weight: 600; color: var(--reward-text); margin: 0;">Tiến trình thăng cấp (Levels)</h3>
              </div>
              <p style="font-size: 13px; color: #64748b; margin-bottom: 16px;">Thiết lập các mốc điểm kinh nghiệm (XP) để người dùng thăng cấp và nhận quà tặng đặc biệt khi đạt cấp độ mới.</p>
              
              <div class="level-config-list" style="display: flex; flex-direction: column; gap: 12px;">
                <!-- Placeholder / Form Add Level -->
                <div v-if="!isCreatingLevel" @click="isCreatingLevel = true" class="premium-placeholder" style="padding: 12px; min-height: 72px;">
                  <i class="fa-solid fa-plus" style="margin-right: 8px;"></i> Thêm mốc Cấp độ mới
                </div>
                <div v-else class="premium-form" style="padding: 12px; min-height: 72px; display: flex; align-items: center; border: 1px solid #3b82f6; border-radius: 12px; background: #eff6ff;">
                  <form @submit.prevent="createLevelConfig" style="display: flex; gap: 12px; width: 100%; align-items: center;">
                    <div class="level-badge" style="width: 44px; height: 44px; border-radius: 50%; background: linear-gradient(135deg, #1e293b, #0f172a); color: #38bdf8; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 10px rgba(0,0,0,0.15); border: 2px solid #38bdf8; flex-shrink: 0; font-weight: 900; font-size: 16px;">
                      {{ levelForm.level || '?' }}
                    </div>
                    <div style="flex: 1; display: grid; grid-template-columns: 0.8fr 1.5fr 1fr; gap: 12px; align-items: center;">
                      <div>
                        <input v-model.number="levelForm.level" type="number" placeholder="Mốc cấp độ (VD: 60)" required class="sa-input" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; font-weight: 600;" />
                      </div>
                      <div>
                        <input v-model="levelForm.title" placeholder="Danh hiệu (VD: Hạng mới)" required class="sa-input" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; font-weight: 600;" />
                      </div>
                      <div>
                        <input v-model.number="levelForm.requiredXpPerLevel" type="number" placeholder="XP (VD: 1000)" required class="sa-input" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; font-weight: 600;" />
                      </div>
                    </div>
                    <div style="display: flex; gap: 8px;">
                      <button type="button" @click="isCreatingLevel = false" class="secondary-btn" style="padding: 6px 12px; background: transparent; color: #ef4444; border: 1px solid #ef4444; border-radius: 6px; font-weight: 600; font-size: 12px;">Hủy</button>
                      <button type="submit" class="secondary-btn" style="padding: 6px 12px; background: transparent; color: #3b82f6; border: 1px solid #3b82f6; border-radius: 6px; font-weight: 600; font-size: 12px;">Thêm</button>
                    </div>
                  </form>
                </div>

                <div v-for="(lv, index) in levelConfigs" :key="index" class="premium-card level-row" style="padding: 12px 16px; display: flex; align-items: center; gap: 16px;">
                  <!-- Level Badge -->
                  <div class="level-badge" style="width: 54px; height: 54px; border-radius: 50%; background: linear-gradient(135deg, #1e293b, #0f172a); color: #38bdf8; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 10px rgba(0,0,0,0.15); border: 2px solid #38bdf8; flex-shrink: 0; font-weight: 900; font-size: 20px;">
                    {{ lv.level }}
                  </div>
                  
                  <!-- Config Inputs -->
                  <div style="flex: 1; display: grid; grid-template-columns: 0.8fr 1.5fr 1fr 2fr; gap: 12px; align-items: center;">
                    <div>
                      <label style="display: block; font-size: 11px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">Mốc cấp độ</label>
                      <input v-model.number="lv.level" type="number" class="sa-input" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; font-weight: 600; color: #0f172a;" />
                    </div>
                    <div>
                      <label style="display: block; font-size: 11px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">Huy hiệu / Danh hiệu</label>
                      <input v-model="lv.title" class="sa-input" placeholder="VD: Intern" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; font-weight: 600; color: #0f172a;" />
                    </div>
                    <div>
                      <label style="display: block; font-size: 11px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">XP / Cấp</label>
                      <input v-model.number="lv.requiredXpPerLevel" type="number" class="sa-input" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; font-weight: 600; color: #2563eb;" />
                    </div>
                    <div>
                      <label style="display: block; font-size: 11px; font-weight: 700; color: #64748b; margin-bottom: 4px; text-transform: uppercase;">Phần thưởng thăng hạng</label>
                      <select v-model="lv.rewardId" class="sa-input" style="width: 100%; padding: 6px 10px; border: 1px solid #cbd5e1; border-radius: 6px; font-size: 13px; color: #475569; outline: none; background: #f8fafc;">
                        <option value="">-- Không có quà --</option>
                        <option v-for="r in seasonDashboard.availableRewards" :key="r.id" :value="r.id">{{ r.name }}</option>
                      </select>
                    </div>
                  </div>
                  
                  <div style="flex-shrink: 0; padding-left: 8px; border-left: 1px dashed #e2e8f0;">
                    <i class="fa-solid fa-trash-can" style="color: #ef4444; font-size: 16px; cursor: pointer; padding: 8px; transition: transform 0.2s;" @click="removeLevelConfig(index)" title="Xóa nhóm cấp độ này"></i>
                  </div>
                </div>
              </div>

              <div style="margin-top: 16px; display: flex; justify-content: flex-end;">
                <button type="button" class="primary-btn" style="padding: 8px 24px; border-radius: 8px; font-weight: 600; font-size: 13px;" @click="saveLevelConfig">
                  Lưu cấu hình
                </button>
              </div>
            </div>
          </div>
        </el-tab-pane>
      </el-tabs>
    </el-drawer>
  </section>
</template>

<script setup>
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { ElMessage } from 'element-plus'
import { useI18nStore } from '@/store/useI18nStore'
import { useAuthStore } from '@/store/useAuthStore'
import { usePeopleStore } from '@/store/usePeopleStore'
import { useSiteStore } from '@/store/useSiteStore'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import axiosClient from '@/api/axiosClient'
import UserAvatar from '@/components/common/UserAvatar.vue'
import { getScopedCurrentProjectId } from '@/utils/projectContext'
import { validateRewardForm, validateRewardSeasonForm } from '@/utils/rewardUi'
import draggable from 'vuedraggable'

const { t } = useI18nStore()
const authStore = useAuthStore()
const peopleStore = usePeopleStore()
const siteStore = useSiteStore()
const homeProjectStore = useHomeProjectStore()
const currentUser = authStore.user || {}

const getFirstActiveProjectId = () => {
  let pid = getScopedCurrentProjectId()
  if (pid) return pid
  
  const activeProjects = homeProjectStore.projects?.filter(p => !p.isArchived) || []
  if (activeProjects.length > 0) {
    return activeProjects[0].id
  }
  return null
}

const ensureActiveProjects = async () => {
  if (!homeProjectStore.projects || homeProjectStore.projects.length === 0) {
    await homeProjectStore.fetchProjects()
  }
  
  let activeProjects = []
  const ctxProj = getScopedCurrentProjectId()
  if (ctxProj) activeProjects.push({ id: ctxProj })
  else activeProjects = homeProjectStore.projects?.filter(p => !p.isArchived) || []

  return activeProjects
}

const isOwner = computed(() => siteStore.activeSite?.workspaceRole === 'Owner' || siteStore.activeSite?.WorkspaceRole === 'Owner')

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
const shopBusy = ref(false)
const seasonForm = ref({ name: '', type: 'Custom', startAt: '', endAt: '', timeZone: '' })
const rewardForm = ref({ seasonId: '', name: '', description: '', rewardType: 'Gift', condition: 'PersonalMilestone', threshold: 100, rankTo: 1, requireActiveMember: true, method: 'Redeem', pointCost: 0, quantity: null, claimLimit: null, imageFile: null, imagePreview: null, usePoints: false, useLevel: false, useTop: false, levelRequired: 1, topRequired: 3 })
const rewardTypes = ['Cash', 'Voucher', 'Gift', 'Privilege', 'Custom']
const rewardConditions = [
  { key: 'TopN', label: 'Top N' },
  { key: 'SeasonPoints', label: 'Season Points ≥ X' },
  { key: 'OnTimeRate', label: 'On-time rate ≥ X%' },
  { key: 'ApprovedTasks', label: 'Approved tasks ≥ X' },
  { key: 'TeamOnTimeRate', label: 'Team on-time rate ≥ X%' }
]

// New interactive state variables
const activeTab = ref('history')
const openShopModal = ref(false)
const shopActiveTab = ref('store')
const openSettingsModal = ref(false)
const settingsActiveTab = ref('seasons')
const isCreatingSeason = ref(false)
const isCreatingReward = ref(false)

const parseDescription = (desc) => {
  if (!desc) return { text: '', usePoints: true, pointCost: 0, useLevel: false, useTop: false, deductLeaderboard: true }
  try {
    const obj = JSON.parse(desc)
    if (obj.text !== undefined) {
      if (obj.deductLeaderboard === undefined) obj.deductLeaderboard = true
      return obj
    }
    return { text: desc, usePoints: true, pointCost: 0, useLevel: false, useTop: false, deductLeaderboard: true }
  } catch(e) {
    return { text: desc, usePoints: true, pointCost: 0, useLevel: false, useTop: false, deductLeaderboard: true }
  }
}

const imageRefreshKey = ref(0)
const quickImageInputRef = ref(null)
const quickImageRewardId = ref(null)

const editingRewardId = ref(null)
const editRewardForm = ref({ name: '', description: '' })

const startEditReward = (reward) => {
  editingRewardId.value = reward.id
  editRewardForm.value = {
    name: reward.name,
    description: parseDescription(reward.description).text
  }
}

const cancelEditReward = () => {
  editingRewardId.value = null
}

const saveEditReward = async (reward) => {
  const r = seasonDashboard.value.availableRewards.find(x => x.id === reward.id)
  if (r) {
    const pid = getFirstActiveProjectId()
    if (!pid || !seasonDashboard.value.currentSeason) return
    
    managerBusy.value = true
    try {
      const currentConfig = getRewardConfig(r)
      
      // If we are saving from the Edit Form, update the text and name
      if (editRewardForm.value && editRewardForm.value.name) {
        currentConfig.text = editRewardForm.value.description
      }
      
      const payloadName = (editRewardForm.value && editRewardForm.value.name) ? editRewardForm.value.name : r.name
      
      await axiosClient.put(`/projects/${pid}/rewards/seasons/${seasonDashboard.value.currentSeason.id}/definitions/${r.id}`, {
        name: payloadName,
        description: JSON.stringify(currentConfig),
        rewardType: r.rewardType || 'Gift',
        conditionType: r.conditionType || 'PersonalMilestone',
        conditionMetric: r.conditionMetric || 'SeasonPoints',
        threshold: r.threshold || 0,
        rankTo: r.rankTo || 1,
        method: currentConfig.usePoints ? 'Redeem' : 'Gift',
        pointCost: currentConfig.pointCost || 0,
        quantity: r.quantity || null,
        claimLimit: r.claimLimit || null,
        requireActiveMemberAtSettlement: r.requireActiveMemberAtSettlement ?? false
      })
      
      r.name = payloadName
      r.description = JSON.stringify(currentConfig)
      ElMessage.success('Đã lưu cấu hình phần thưởng vào CSDL thành công!')
    } catch (err) {
      ElMessage.error('Không thể lưu phần thưởng.')
    } finally {
      managerBusy.value = false
      editingRewardId.value = null
    }
  } else {
    editingRewardId.value = null
  }
}

const triggerImageUpdate = (reward) => {
  quickImageRewardId.value = reward.id
  if (quickImageInputRef.value) quickImageInputRef.value.click()
}

const handleQuickImageSelect = (event) => {
  const file = event.target.files[0]
  if (file && quickImageRewardId.value) {
    const reader = new FileReader()
    reader.onload = (e) => {
      const img = new Image()
      img.onload = () => {
        const canvas = document.createElement('canvas')
        const ctx = canvas.getContext('2d')
        const width = 250
        const height = 140
        canvas.width = width
        canvas.height = height
        const scale = width / img.width
        const scaledHeight = img.height * scale
        ctx.fillStyle = '#f1f5f9'
        ctx.fillRect(0, 0, width, height)
        ctx.drawImage(img, 0, (height - scaledHeight) / 2, width, scaledHeight)
        const base64 = canvas.toDataURL('image/jpeg', 0.85)
        
        const targetReward = seasonDashboard.value.availableRewards.find(r => r.id === quickImageRewardId.value)
        if (targetReward) {
          const pid = getFirstActiveProjectId()
          if (pid && seasonDashboard.value.currentSeason) {
            managerBusy.value = true
            axiosClient.put(`/projects/${pid}/rewards/seasons/${seasonDashboard.value.currentSeason.id}/definitions/${targetReward.id}`, {
              displayValue: base64
            }).then(() => {
              targetReward.displayValue = base64
              ElMessage.success('Đã cập nhật ảnh thành công!')
            }).catch(() => {
              ElMessage.error('Lỗi khi lưu ảnh lên máy chủ.')
            }).finally(() => {
              managerBusy.value = false
            })
          }
        }
        quickImageRewardId.value = null
        if (quickImageInputRef.value) quickImageInputRef.value.value = ''
      }
      img.src = e.target.result
    }
    reader.readAsDataURL(file)
  }
}

const getRewardImage = (id) => {
  const r = seasonDashboard.value.availableRewards.find(x => x.id === id)
  if (r) {
    if (r.displayValue && r.displayValue.startsWith('data:image')) return r.displayValue
    if (r.imageUrl && r.imageUrl.startsWith('data:image')) return r.imageUrl
  }
  return ''
}

const isCreatingLevel = ref(false)
const levelForm = ref({ level: null, title: '', requiredXpPerLevel: 100, rewardId: '' })

// Shop Rewards UI state
const localRewardConfigs = ref({})
const getRewardConfig = (reward) => {
  if (!localRewardConfigs.value[reward.id]) {
    localRewardConfigs.value[reward.id] = parseDescription(reward.description)
  }
  return localRewardConfigs.value[reward.id]
}

const getRewardDefById = (id) => {
  return seasonDashboard.value.availableRewards?.find(r => r.id === id) || { id, name: '' }
}

const shopSearch = ref('')
const newSeasonRewards = ref([])

const filteredShopRewards = computed(() => {
  const query = shopSearch.value.trim().toLowerCase()
  const rewards = seasonDashboard.value.availableRewards || []
  const pointRewards = rewards.filter(r => getRewardConfig(r).usePoints === true)
  if (!query) return pointRewards
  return pointRewards.filter(r => (r.name || '').toLowerCase().includes(query))
})

const isRewardInSeason = (seasonId, rewardId) => {
  if (seasonId === 'new') {
    return newSeasonRewards.value.some(id => id == rewardId)
  }
  const season = managerSeasons.value.find(s => s.id == seasonId)
  return season?.rewards?.some(id => id == rewardId) || false
}

const toggleRewardInSeason = (seasonId, rewardId) => {
  if (seasonId === 'new') {
    const idx = newSeasonRewards.value.indexOf(rewardId)
    if (idx > -1) newSeasonRewards.value.splice(idx, 1)
    else newSeasonRewards.value.push(rewardId)
    return
  }
  const season = managerSeasons.value.find(s => s.id == seasonId)
  if (!season) return
  if (!season.rewards) season.rewards = []
  const idx = season.rewards.findIndex(id => id == rewardId)
  if (idx > -1) season.rewards.splice(idx, 1)
  else season.rewards.push(rewardId)

  // Persist to localStorage
  localStorage.setItem(`season_rewards_${seasonId}`, JSON.stringify(season.rewards))
  shopRefreshKey.value++
}

// Level Config State (Mock)
const levelConfigs = ref([
  { level: 1, title: 'Intern', requiredXpPerLevel: 100, rewardId: '' },
  { level: 16, title: 'Junior', requiredXpPerLevel: 250, rewardId: '' },
  { level: 31, title: 'Senior', requiredXpPerLevel: 600, rewardId: '' },
  { level: 51, title: 'Master', requiredXpPerLevel: 1500, rewardId: '' }
])
const createLevelConfig = () => {
  if (!levelForm.value.level || !levelForm.value.title) return
  
  if (levelConfigs.value.some(lv => lv.level === levelForm.value.level)) {
    ElMessage.error(`Mốc cấp độ ${levelForm.value.level} đã tồn tại!`)
    return
  }

  levelConfigs.value.push({ ...levelForm.value })
  levelConfigs.value.sort((a, b) => a.level - b.level)
  isCreatingLevel.value = false
  levelForm.value = { level: null, title: '', requiredXpPerLevel: 100, rewardId: '' }
}
const removeLevelConfig = (index) => {
  levelConfigs.value.splice(index, 1)
}

const saveLevelConfig = async () => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  managerBusy.value = true
  try {
    const payload = {
      configs: levelConfigs.value.map(lv => ({
        level: lv.level,
        title: lv.title,
        requiredXpPerLevel: lv.requiredXpPerLevel,
        rewardId: lv.rewardId || '00000000-0000-0000-0000-000000000000'
      }))
    }
    await axiosClient.put(`/projects/${pid}/rewards/levels`, payload)
    ElMessage.success('Đã lưu cấu hình cấp độ thành công!')
  } catch (error) {
    ElMessage.error('Không thể lưu cấu hình cấp độ.')
  } finally {
    managerBusy.value = false
  }
}

const loadLevelConfigs = async () => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  managerBusy.value = true
  try {
    const res = await axiosClient.get(`/projects/${pid}/rewards/levels`)
    if (res.data && res.data.data) {
      levelConfigs.value = res.data.data.map(item => ({
        level: item.level,
        title: item.title,
        requiredXpPerLevel: item.requiredXpPerLevel,
        rewardId: item.rewardId
      }))
      levelConfigs.value.sort((a, b) => a.level - b.level)
    }
  } catch (error) {
    console.error('Lỗi khi tải cấu hình level:', error)
  } finally {
    managerBusy.value = false
  }
}

const activateSeason = async (season) => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  managerBusy.value = true
  try {
    await axiosClient.post(`/projects/${pid}/rewards/seasons/${season.id}/activate`)
    ElMessage.success(`Mùa giải ${season.name} đã bắt đầu.`)
    await loadRewards()
  } catch (error) {
    ElMessage.error('Không thể bắt đầu mùa giải.')
  } finally {
    managerBusy.value = false
  }
}

const pauseSeason = async (season) => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  managerBusy.value = true
  try {
    await axiosClient.post(`/projects/${pid}/rewards/seasons/${season.id}/pause`)
    ElMessage.success(`Mùa giải ${season.name} đã tạm dừng.`)
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể tạm dừng mùa giải.')
  } finally {
    managerBusy.value = false
  }
}

const closeSeason = async (season) => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  managerBusy.value = true
  try {
    await axiosClient.post(`/projects/${pid}/rewards/seasons/${season.id}/close`)
    ElMessage.success(`Mùa giải ${season.name} đã kết thúc.`)
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể kết thúc mùa giải.')
  } finally {
    managerBusy.value = false
  }
}

const rewardImageInput = ref(null)

const triggerImageSelect = () => {
  if (rewardImageInput.value) {
    rewardImageInput.value.click()
  }
}

const handleRewardImageSelect = (event) => {
  const file = event.target.files[0]
  if (file) {
    rewardForm.value.imageFile = file
    rewardForm.value.imagePreview = URL.createObjectURL(file)
    cropState.value.offsetY = 0
  }
}

const imageRef = ref(null)
const cropState = ref({
  isDragging: false,
  startY: 0,
  offsetY: 0
})

const startDrag = (e) => {
  if (!rewardForm.value.imagePreview) return
  cropState.value.isDragging = true
  cropState.value.startY = e.clientY - cropState.value.offsetY
}

const onDrag = (e) => {
  if (!cropState.value.isDragging) return
  const y = e.clientY - cropState.value.startY
  const containerHeight = 100
  const imgElement = imageRef.value
  if (!imgElement) return
  const imgHeight = imgElement.clientHeight || 100
  const minY = Math.min(0, containerHeight - imgHeight)
  cropState.value.offsetY = Math.max(minY, Math.min(0, y))
}

const stopDrag = () => {
  cropState.value.isDragging = false
}

const generateCroppedBase64 = async () => {
  if (!rewardForm.value.imagePreview || !imageRef.value) return null
  return new Promise((resolve) => {
    const canvas = document.createElement('canvas')
    const ctx = canvas.getContext('2d')
    const width = 250 // standard card width
    const height = 140
    canvas.width = width
    canvas.height = height
    
    const img = new Image()
    img.onload = () => {
      const scale = width / img.width
      const scaledHeight = img.height * scale
      
      const containerWidth = imageRef.value.parentElement.clientWidth || 250
      const ratio = width / containerWidth
      const dy = cropState.value.offsetY * ratio
      
      ctx.fillStyle = '#f1f5f9'
      ctx.fillRect(0, 0, width, height)
      ctx.drawImage(img, 0, dy, width, scaledHeight)
      resolve(canvas.toDataURL('image/jpeg', 0.85))
    }
    img.onerror = () => resolve(null)
    img.src = rewardForm.value.imagePreview
  })
}

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
const shopRefreshKey = ref(0)
const managedGrants = computed(() => seasonDashboard.value.openRewards || [])
const myGrants = computed(() => {
  const history = seasonDashboard.value.rewardHistory || []
  const map = {}
  history.forEach(grant => {
    if (!map[grant.rewardDefinitionId]) {
      map[grant.rewardDefinitionId] = {
        ...grant,
        quantity: 1
      }
    } else {
      map[grant.rewardDefinitionId].quantity += 1
      if (new Date(grant.earnedAt) > new Date(map[grant.rewardDefinitionId].earnedAt)) {
        map[grant.rewardDefinitionId].earnedAt = grant.earnedAt
      }
    }
  })
  return Object.values(map)
})
const shopItems = computed(() => {
  const trigger = shopRefreshKey.value // Reactivity trigger
  const currentSeason = seasonDashboard.value.currentSeason
  if (!currentSeason) return []

  const managerSeason = managerSeasons.value.find(s => s.id == currentSeason.id)
  let activeRewardsIds = managerSeason ? managerSeason.rewards : null
  
  if (!activeRewardsIds) {
    const cached = localStorage.getItem(`season_rewards_${currentSeason.id}`)
    activeRewardsIds = []
    if (cached) {
      try { activeRewardsIds = JSON.parse(cached) } catch(e) {}
    }
  }

  return (seasonDashboard.value.availableRewards || []).filter(item => {
    return (activeRewardsIds || []).some(id => id == item.id)
  })
})

// Point Rules Settings
const pointRules = ref({
  PointConfig: {
    sequence: ["base", "+", "storyPoints", "+", "priorityBonus"],
    enableBase: true,
    enableStoryPoints: true,
    enablePriorityBonus: true,
    enableEarlyBonus: false
  },
  ExpConfig: {
    sequence: ["base", "+", "storyPoints", "+", "priorityBonus"],
    enableBase: true,
    enableStoryPoints: true,
    enablePriorityBonus: true,
    enableEarlyBonus: false
  },
  basePoints: 10,
  storyPointMultiplier: 5,
  priorityBonus: {
    Low: 0,
    Normal: 0,
    High: 5,
    Urgent: 10
  },
  earlyBonusPercent: 10,
  baseExp: 15,
  storyExpMultiplier: 5,
  expPriorityBonus: {
    Low: 0,
    Normal: 0,
    High: 5,
    Urgent: 10
  },
  expEarlyBonusPercent: 10
})

const pointFormulaEditMode = ref(false)
const expFormulaEditMode = ref(false)

const getBlockName = (node) => {
  if (node === 'base') return 'Điểm Cơ Bản'
  if (node === 'storyPoints') return 'Story Points'
  if (node === 'priorityBonus') return 'Thưởng Ưu Tiên'
  if (node === 'earlyBonus') return 'Thưởng Sớm (%)'
  return node
}

const toggleOperator = (config, index) => {
  if (config.sequence[index] === '+') config.sequence[index] = '*'
  else if (config.sequence[index] === '*') config.sequence[index] = '-'
  else if (config.sequence[index] === '-') config.sequence[index] = '+'
}

const cleanupSequence = (config) => {
  const blocks = config.sequence.filter(x => !['+', '-', '*'].includes(x))
  const ops = config.sequence.filter(x => ['+', '-', '*'].includes(x))
  const newSeq = []
  for (let i = 0; i < blocks.length; i++) {
    newSeq.push(blocks[i])
    if (i < blocks.length - 1) {
      newSeq.push(ops[i] || '+')
    }
  }
  config.sequence = newSeq
}

const toggleBlock = (config, blockKey, enabled) => {
  if (enabled) {
    if (config.sequence.length > 0) config.sequence.push('+')
    config.sequence.push(blockKey)
  } else {
    const idx = config.sequence.indexOf(blockKey)
    if (idx > -1) {
      if (idx > 0 && ['+', '*', '-'].includes(config.sequence[idx - 1])) {
        config.sequence.splice(idx - 1, 2)
      } else if (idx < config.sequence.length - 1 && ['+', '*', '-'].includes(config.sequence[idx + 1])) {
        config.sequence.splice(idx, 2)
      } else {
        config.sequence.splice(idx, 1)
      }
    }
  }
}

const savePointRules = async () => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  managerBusy.value = true
  try {
    const rulesConfig = JSON.stringify(pointRules.value)
    await axiosClient.put(`/settings/GamificationRules:${pid}`, {
      settings: {
        'Rules': rulesConfig
      }
    })
    ElMessage.success('Lưu cấu hình điểm thành công!')
  } catch (error) {
    ElMessage.error('Không thể lưu cấu hình điểm.')
  } finally {
    managerBusy.value = false
  }
}

const loadPointRules = async () => {
  const pid = getFirstActiveProjectId()
  if (!pid) return
  try {
    const res = await axiosClient.get(`/settings/GamificationRules:${pid}`)
    if (res.data?.data?.Rules) {
      const savedRules = JSON.parse(res.data.data.Rules)
      pointRules.value = { ...pointRules.value, ...savedRules }
    }
  } catch (e) {
    // ignore
  }
}

// Load point rules on mount
onMounted(() => {
  loadPointRules()
  loadLevelConfigs()
})

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
const userCanManage = computed(() => true)

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

    const projectId = getFirstActiveProjectId()
    if (projectId) promises.push(axiosClient.get(`/projects/${projectId}/rewards/dashboard?t=${new Date().getTime()}`).catch(() => null))

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
    
    const seasonResponse = projectId ? results[results.length - 1] : null
    if (seasonResponse?.data) {
      seasonDashboard.value = { ...seasonDashboard.value, ...(seasonResponse.data?.data || seasonResponse.data) }
      // Load Base64 Image from localStorage
      if (seasonDashboard.value.availableRewards) {
        seasonDashboard.value.availableRewards.forEach(r => {
          const cachedImg = localStorage.getItem(`reward_img_${r.id}`)
          if (cachedImg) r.imageUrl = cachedImg
        })
      }
    } else {
      seasonDashboard.value = { ...seasonDashboard.value, currentSeason: null, leaderboard: [], openRewards: [], availableRewards: [] }
    }

    if (seasonDashboard.value.currentSeason && seasonDashboard.value.currentSeason.status === 'Active') {
      const rawBoard = seasonDashboard.value.leaderboard || leaders.data?.data || []
      const activeBoard = rawBoard.filter(u => (u.seasonPoints || u.totalPoints || u.points || 0) >= 1)
      leaderboard.value = activeBoard.map(u => ({
        userId: u.userId,
        userName: u.userName || u.name,
        totalPoints: u.seasonPoints || u.totalPoints || u.points || 0,
        completedTasks: u.finalizedTasks ?? u.completedTasks ?? u.tasksCompleted ?? u.tasks ?? 0,
        rank: u.rank
      }))
    } else {
      leaderboard.value = []
    }

    // Diagnostic toast to see how many rewards are fetched
    if (activeTab.value === 'rewards' && !isCreatingReward.value) {
      ElMessage.info(`[Debug] Đã tải ${seasonDashboard.value.availableRewards?.length || 0} phần thưởng từ hệ thống`)
    }

    if (projectId && userCanManage.value) {
      const seasonsResponse = await axiosClient.get(`/projects/${projectId}/rewards/seasons`).catch(() => null)
      managerSeasons.value = seasonsResponse?.data?.data || seasonsResponse?.data || []
    } else if (seasonDashboard.value.currentSeason) {
      managerSeasons.value = [seasonDashboard.value.currentSeason]
    }
    
    // Load persisted season-reward mappings
    managerSeasons.value.forEach(s => {
      const cached = localStorage.getItem(`season_rewards_${s.id}`)
      if (cached) {
        try { s.rewards = JSON.parse(cached) } catch(e) { s.rewards = [] }
      } else {
        s.rewards = []
      }
    })

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
  if (!seasonForm.value.name?.trim()) {
    ElMessage.warning('Vui lòng nhập tên mùa giải.')
    return
  }
  
  if (!seasonForm.value.startAt) {
    seasonForm.value.startAt = new Date().toISOString().split('T')[0]
  }
  if (!seasonForm.value.type) {
    seasonForm.value.type = 'Custom'
  }
  
  const activeProjects = await ensureActiveProjects()
  if (activeProjects.length === 0) {
    ElMessage.error(t('Gamification.RequireActiveProject', 'Vui lòng tạo ít nhất 1 Dự án trước khi tạo mùa giải.'))
    return
  }

  const projectId = activeProjects[0].id

  managerBusy.value = true
  try {
    await axiosClient.post(`/projects/${projectId}/rewards/seasons`, {
      name: seasonForm.value.name.trim(),
      type: seasonForm.value.type,
      startAt: `${seasonForm.value.startAt}T00:00:00+00:00`,
      endAt: seasonForm.value.endAt ? `${seasonForm.value.endAt}T23:59:59.9999999+00:00` : null,
      timeZone: seasonForm.value.timeZone?.trim() || null
    })

    seasonForm.value = { name: '', type: 'Custom', startAt: '', endAt: '', timeZone: '' }
    isCreatingSeason.value = false
    ElMessage.success('Tạo mùa giải thành công!')
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Unable to create season.')
  } finally {
    managerBusy.value = false
  }
}

const createReward = async () => {
  if (!rewardForm.value.name?.trim()) {
    ElMessage.warning('Vui lòng nhập tên phần thưởng.')
    return
  }

  try {
    managerBusy.value = true
    const activeProjects = await ensureActiveProjects()
    if (activeProjects.length === 0) {
      ElMessage.error(t('Gamification.RequireActiveProject', 'Vui lòng tạo ít nhất 1 Dự án trước khi tạo phần thưởng.'))
      return
    }

    const projectId = activeProjects[0].id

    let seasonId = rewardForm.value.seasonId
    if (!seasonId && seasonDashboard.value.currentSeason) {
      seasonId = seasonDashboard.value.currentSeason.id
    }
    if (!seasonId && managerSeasons.value.length > 0) {
      seasonId = managerSeasons.value[0].id
    }
    if (!seasonId) {
      const uniqueName = `Mùa Giải ${new Date().getTime().toString().slice(-4)}`
      const newSeason = await axiosClient.post(`/projects/${projectId}/rewards/seasons`, {
        name: uniqueName,
        type: 'Sprint',
        startAt: `${new Date().toISOString().split('T')[0]}T00:00:00+00:00`
      })
      const sData = newSeason.data?.data || newSeason.data
      seasonId = sData?.id
    }

    const croppedImage = await generateCroppedBase64()

    const config = {
      text: rewardForm.value.description || '',
      usePoints: rewardForm.value.usePoints,
      pointCost: rewardForm.value.pointCost || 0,
      useLevel: rewardForm.value.useLevel,
      levelRequired: rewardForm.value.levelRequired || 1,
      useTop: rewardForm.value.useTop,
      topRequired: rewardForm.value.topRequired || 3,
      deductLeaderboard: rewardForm.value.deductLeaderboard ?? true
    }
    const descriptionJson = JSON.stringify(config)

    const res = await axiosClient.post(`/projects/${projectId}/rewards/seasons/${seasonId}/definitions`, {
      name: rewardForm.value.name.trim(),
      description: descriptionJson,
      rewardType: rewardForm.value.rewardType || 'Gift',
      conditionType: rewardForm.value.condition || 'PersonalMilestone',
      conditionMetric: 'SeasonPoints',
      threshold: rewardForm.value.threshold || 0,
      rankTo: rewardForm.value.rankTo || 1,
      method: rewardForm.value.usePoints ? 'Redeem' : 'Gift',
      pointCost: rewardForm.value.pointCost || 0,
      quantity: rewardForm.value.quantity || null,
      claimLimit: rewardForm.value.claimLimit || null,
      requireActiveMemberAtSettlement: false
    })

    const responseData = res.data?.data || res.data
    if (croppedImage && responseData?.id) {
      localStorage.setItem(`reward_img_${responseData.id}`, croppedImage)
      imageRefreshKey.value++
    }

    rewardForm.value = { seasonId: '', name: '', description: '', rewardType: 'Gift', condition: 'PersonalMilestone', threshold: 100, rankTo: 1, requireActiveMember: false, method: 'Redeem', pointCost: 0, quantity: null, claimLimit: null, imageFile: null, imagePreview: null }
    isCreatingReward.value = false
    ElMessage.success('Tạo phần thưởng thành công!')
    await loadRewards()
  } catch (error) {
    console.error("Create reward error:", error.response || error)
    const errorMsg = error.response?.data?.message || error.response?.data?.title || error.message || 'Unable to create reward.'
    ElMessage.error(`Lỗi tạo phần thưởng: ${errorMsg}`)
  } finally {
    managerBusy.value = false
  }
}

const redeemReward = async (item) => {
  if (!item || shopBusy.value) return
  try {
    shopBusy.value = true
    const projectId = getFirstActiveProjectId()
    if (!projectId) return
    const res = await axiosClient.post(`/projects/${projectId}/rewards/redeem`, {
      rewardDefinitionId: item.id
    })
    
    ElMessage.success(res.data?.message || 'Đổi quà thành công!')
    if (res.data?.data) {
      wallet.value.totalPoints = res.data.data.remainingPoints
      item.quantity = res.data.data.remainingQuantity
    }
    await loadRewards()
  } catch (error) {
    ElMessage.error(error.response?.data?.message || 'Không thể đổi quà.')
  } finally {
    shopBusy.value = false
  }
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

onMounted(async () => {
  if (!homeProjectStore.projects || homeProjectStore.projects.length === 0) {
    await homeProjectStore.fetchProjects()
  }
  loadRewards()
  window.addEventListener('open-reward-settings', () => {
    openSettingsModal.value = true
  })
})

onUnmounted(() => {
  window.removeEventListener('open-reward-settings', () => {
    openSettingsModal.value = true
  })
})
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
  flex: 1;
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
  display: flex;
  flex-direction: column;
  flex: 1;
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

.inline-creation-row {
  display: flex;
  gap: 8px;
  align-items: center;
  background: var(--color-surface-hover, #FAFBFC);
  padding: 12px;
  border: 1px dashed var(--color-border, #DFE1E6);
  border-radius: 6px;
  transition: all 0.2s ease;
}
.inline-creation-row:focus-within {
  border-color: var(--sa-primary, #0052cc);
  background: var(--color-background-soft, #FFFFFF);
}
.inline-creation-row input, .inline-creation-row select {
  padding: 8px 12px;
  border: 1px solid var(--color-border, #DFE1E6);
  border-radius: 4px;
  font-size: 14px;
  outline: none;
  background: var(--color-background, #fff);
  color: var(--color-text, #172B4D);
}
.inline-creation-row input:focus, .inline-creation-row select:focus {
  border-color: var(--sa-primary, #0052cc);
}

.add-placeholder-btn {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 12px 16px;
  border: 1px dashed var(--color-border, #DFE1E6);
  border-radius: 6px;
  color: var(--color-text-secondary, #5E6C84);
  cursor: pointer;
  background: transparent;
  transition: all 0.2s;
  font-weight: 500;
}
.add-placeholder-btn:hover {
  background: var(--color-background-soft, #F4F5F7);
  color: var(--color-text, #172B4D);
  border-color: #C1C7D0;
}

.reward-creation-card {
  border: 1px solid var(--color-border, #DFE1E6);
  border-radius: 8px;
  overflow: hidden;
  background: var(--color-background, #FFFFFF);
  width: 280px;
  box-shadow: 0 4px 12px rgba(9, 30, 66, 0.1);
  margin-bottom: 16px;
}
.reward-card-header {
  height: 100px;
  background: linear-gradient(135deg, #E5F0FF, #B3D4FF);
  display: flex;
  align-items: center;
  justify-content: center;
}
.reward-card-image-placeholder {
  width: 48px;
  height: 48px;
  background: #fff;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 20px;
  color: #0052cc;
  box-shadow: 0 2px 4px rgba(0,0,0,0.1);
}
.reward-card-body {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.reward-card-input-name {
  border: none;
  border-bottom: 1px solid var(--color-border, #DFE1E6);
  font-size: 16px;
  font-weight: 600;
  padding: 4px 0;
  outline: none;
  background: transparent;
}
.reward-card-input-name:focus {
  border-bottom-color: var(--sa-primary, #0052cc);
}
.reward-card-cost-row {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #f5cd47;
}
.reward-card-cost-row input {
  flex: 1;
  border: 1px solid var(--color-border, #DFE1E6);
  border-radius: 4px;
  padding: 6px 10px;
  outline: none;
  font-size: 14px;
}
.reward-card-select-season {
  border: 1px solid var(--color-border, #DFE1E6);
  border-radius: 4px;
  padding: 6px 10px;
  outline: none;
  font-size: 13px;
  width: 100%;
}
.reward-card-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  margin-top: 4px;
}

.input-with-icon {
  display: flex;
  align-items: center;
  border: 1px solid var(--color-border, #DFE1E6);
  border-radius: 4px;
  overflow: hidden;
  background: var(--color-background, #fff);
  transition: border-color 0.2s;
}
.input-with-icon:focus-within {
  border-color: var(--sa-primary, #0052cc);
}
.input-with-icon .icon-left {
  padding: 0 10px;
  color: #f5cd47;
  font-size: 16px;
}
.input-with-icon input {
  flex: 1;
  border: none;
  padding: 8px 10px 8px 0;
  outline: none;
  font-size: 14px;
  background: transparent;
}
.reward-preview-img {
  width: 100%;
  height: 100%;
  object-fit: cover;
  border-radius: 50%;
}
</style>

<style>
/* Shop Modal Height adjustment to make top and bottom margins exactly 5vh */
.reward-shop-modal {
  height: 90vh !important;
  display: flex !important;
  flex-direction: column !important;
  margin-bottom: 0 !important;
}

.reward-shop-modal .el-dialog__body {
  flex: 1 !important;
  overflow-y: auto !important;
  padding: 24px;
}
/* Hide the global AI mascot on the Rewards page to maintain premium SaaS aesthetics */
.dashboard-layout:has(.rewards-page) .ai-floating-btn.ai-pet {
  display: none !important;
}

/* Modals */
.shop-header {
  text-align: center;
  padding: 24px;
  background: var(--color-surface-hover);
  border-radius: 12px;
  margin-bottom: 24px;
}
.shop-balance {
  font-size: 32px;
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  margin-bottom: 8px;
}
.shop-balance span {
  font-size: 16px;
  color: var(--color-text-secondary);
  font-weight: 500;
}
.shop-subtitle {
  color: var(--color-text-secondary);
  font-size: 14px;
}
.shop-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: 16px;
  min-height: 50vh;
}
.shop-item-card {
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 12px;
  border: 1px solid var(--color-border);
  border-radius: 12px;
  background: var(--color-background-soft);
}
.shop-item-icon {
  width: 48px;
  height: 48px;
  border-radius: 12px;
  background: color-mix(in srgb, var(--sa-primary) 10%, transparent);
  color: var(--sa-primary);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
}
.shop-item-info h4 {
  margin: 0 0 4px;
  font-size: 16px;
}
.shop-item-info p {
  margin: 0 0 12px;
  font-size: 13px;
  color: var(--color-text-secondary);
  line-height: 1.4;
}
.shop-item-meta {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
  font-size: 13px;
  font-weight: 600;
}
.shop-item-meta .price {
  color: #f5cd47;
  display: flex;
  align-items: center;
  gap: 4px;
}
.shop-item-meta .stock {
  color: var(--color-text-muted);
}
.reward-redeem-btn {
  width: 100%;
  height: 36px;
  padding: 0 16px;
  font-size: 13px;
  display: flex;
  align-items: center;
  justify-content: flex-start;
  gap: 8px;
  border-radius: 8px;
  background: #fef08a;
  color: #a16207;
  border: 1px solid #fde047;
  cursor: pointer;
  transition: all 0.2s ease;
  font-weight: 700;
}
.reward-redeem-btn:hover:not(:disabled) {
  background: #fde047;
  border-color: #facc15;
}
.reward-redeem-btn:active:not(:disabled) {
  background: #facc15;
}
.reward-redeem-btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  filter: grayscale(1);
}

/* Premium Styles for Gamification Admin UI */
.premium-card {
  background: linear-gradient(145deg, #ffffff, #f8fafc);
  border: 1px solid rgba(226, 232, 240, 0.8);
  border-radius: 12px;
  box-shadow: 0 2px 8px -2px rgba(15, 23, 42, 0.05), inset 0 1px 0 rgba(255,255,255,0.6);
  transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
  position: relative;
  overflow: hidden;
}
.premium-card:hover {
  transform: translateY(-3px) scale(1.01);
  box-shadow: 0 10px 20px -5px rgba(15, 23, 42, 0.08), 0 4px 6px -4px rgba(15, 23, 42, 0.04);
  border-color: rgba(37, 99, 235, 0.3);
}
.premium-card::before {
  content: '';
  position: absolute;
  top: 0; left: 0; right: 0; height: 3px;
  background: linear-gradient(90deg, var(--sa-primary, #2563eb), #60a5fa);
  opacity: 0;
  transition: opacity 0.3s;
}
.premium-card:hover::before {
  opacity: 1;
}

.premium-placeholder {
  border: 2px dashed rgba(226, 232, 240, 0.9);
  border-radius: 12px;
  background: rgba(248, 250, 252, 0.5);
  color: #64748b;
  display: flex;
  align-items: center;
  justify-content: center;
  cursor: pointer;
  transition: all 0.2s ease;
  font-weight: 500;
}
.premium-placeholder:hover {
  background: #f1f5f9;
  border-color: var(--sa-primary, #2563eb);
  color: var(--sa-primary, #2563eb);
  transform: translateY(-1px);
}

.premium-form {
  background: #ffffff;
  border: 1px solid var(--sa-primary, #2563eb);
  border-radius: 12px;
  box-shadow: 0 8px 20px -4px rgba(37, 99, 235, 0.15);
  animation: slideDown 0.2s ease-out;
}
@keyframes slideDown {
  from { opacity: 0; transform: translateY(-5px) scale(0.98); }
  to { opacity: 1; transform: translateY(0) scale(1); }
}

.cyber-reward-card {
  border-radius: 12px;
}
.cyber-image-area {
  position: relative;
}
.cyber-grid-bg {
  position: absolute;
  inset: 0;
  background-image: 
    linear-gradient(rgba(255, 255, 255, 0.05) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.05) 1px, transparent 1px);
  background-size: 20px 20px;
  background-position: center;
  opacity: 0.5;
}
.cyber-overlay-hover {
  position: absolute;
  inset: 0;
  background: rgba(37, 99, 235, 0.05);
  opacity: 0;
  transition: opacity 0.2s;
}
.cyber-image-area:hover .cyber-overlay-hover {
  opacity: 1;
}
.sa-input:focus {
  border-color: #3b82f6 !important;
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.1);
}


.add-placeholder:hover {
  background-color: var(--color-background-soft, #f8fafc) !important;
  border-color: var(--sa-primary, #2563eb) !important;
  color: var(--sa-primary, #2563eb) !important;
}

/* Standardized Input from Work Items */
.reward-nexus-input,
input.reward-nexus-input,
.el-popover input.reward-nexus-input,
.el-popover input.reward-nexus-input:not(.el-range-input) {
  width: 100%;
  height: 34px !important;
  padding: 0 12px !important;
  border-radius: 9px !important;
  border: 1px solid #e2e8f0 !important;
  background-color: #ffffff !important;
  color: #334155 !important;
  font-size: 13.5px !important;
  transition: border-color 0.2s, box-shadow 0.2s !important;
  box-sizing: border-box !important;
  outline: none !important;
}
.reward-nexus-input:focus,
input.reward-nexus-input:focus,
.el-popover input.reward-nexus-input:focus,
.el-popover input.reward-nexus-input:not(.el-range-input):focus {
  border-color: var(--color-accent) !important;
  box-shadow: 0 0 0 2px rgba(14, 165, 233, 0.15) !important;
}
.reward-nexus-input::placeholder,
input.reward-nexus-input::placeholder,
.el-popover input.reward-nexus-input::placeholder {
  color: var(--color-text-muted) !important;
}
textarea.reward-nexus-input {
  height: auto !important;
  min-height: 34px !important;
  padding-top: 8px !important;
  padding-bottom: 8px !important;
  resize: none;
}
.reward-nexus-search {
  padding-left: 36px !important;
}

@keyframes scroll-left {
  0% {
    transform: translateX(100%);
  }
  100% {
    transform: translateX(-100%);
  }
}
.marquee-content {
  display: inline-block;
  padding-left: 100%;
  animation: scroll-left 15s linear infinite;
}

.filter-search-field {
  position: relative;
  display: flex;
  align-items: center;
  background: white;
  border: 1px solid var(--border-color, #e2e8f0);
  border-radius: 6px;
  overflow: hidden;
  height: 32px;
  transition: all 0.2s ease;
}
.filter-search-field:focus-within {
  border-color: var(--primary-color, #3b82f6);
  box-shadow: 0 0 0 2px rgba(59, 130, 246, 0.1);
}
.filter-search-icon {
  position: absolute;
  left: 10px;
  color: #94a3b8;
  font-size: 13px;
}
.filter-search-input {
  width: 100%;
  height: 100%;
  border: none;
  padding: 0 12px 0 32px;
  font-size: 13px;
  outline: none;
  background: transparent;
  color: #0f172a;
}
.filter-search-input::placeholder {
  color: #94a3b8;
}.empty-spaces-btn {
  background: white;
  border: 1px solid #e2e8f0;
  color: #475569;
  font-weight: 600;
  font-size: 13px;
  padding: 8px 16px;
  border-radius: 8px;
  box-shadow: 0 1px 2px rgba(0, 0, 0, 0.05);
  transition: all 0.2s ease;
  cursor: pointer;
}
.empty-spaces-btn:hover {
  background: #f8fafc;
  color: #0f172a;
  border-color: #cbd5e1;
}

[data-theme='dark'] .empty-spaces-btn {
  background: rgba(30, 41, 59, 0.5);
  border-color: #334155;
  color: #cbd5e1;
}
[data-theme='dark'] .empty-spaces-btn:hover {
  background: #1e293b;
  color: #f8fafc;
  border-color: #475569;
}
</style>
