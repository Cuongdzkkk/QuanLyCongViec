<template>
  <div class="team-kudos-container">
    <section class="kudos-section teams-content-panel">
      <div class="section-header">
        <h2>Lời khen</h2>
      </div>

      <ProjectPageToolbar
        v-model:searchQuery="kudosSearch"
        show-search
        search-placeholder="Tìm kiếm lời khen"
      >
        <template #filters>
          <ToolbarFilterMenu
            label="Filters"
            clear-label="Xóa lọc"
            clear-all-label="Xóa tất cả"
            empty-label="Chưa áp dụng filter"
            :count="activeFilterCount"
            :active-items="activeFilterItems"
            @clear="clearFilters"
            @remove="removeFilter"
          >
            <template #default="{ search }">
              <DropdownFilter v-if="matchesFilterSearch('Người gửi', search)" label="Người gửi" :options="senderOptions" v-model="filters.sender" :searchable="false" />
            </template>
          </ToolbarFilterMenu>
        </template>

        <template #actions>
          <button class="primary-btn" @click="isGiveKudosOpen = true">Gửi lời khen</button>
        </template>
      </ProjectPageToolbar>

      <div v-if="false" class="kudos-summary">{{ filteredKudos.length }} lời khen</div>

      <div class="goals-empty-state" v-if="filteredKudos.length === 0">
        <div class="empty-spaces-icon" aria-hidden="true">
          <i class="fa-solid fa-medal"></i>
        </div>
        <div class="empty-spaces-copy">
          <h3>No praise found</h3>
          <p>Use praise to thank a teammate, celebrate a small win, or recognize excellent work.</p>
        </div>
      </div>

      <div class="kudos-feed" v-else>
        <div v-for="kudo in filteredKudos" :key="kudo.id" class="kudo-card">
          <div class="kudo-card-header">
            <UserAvatar :user="{ fullName: kudo.senderName || kudo.sender, email: kudo.senderEmail, avatarUrl: kudo.senderAvatarUrl }" :size="32" :fontSize="14" class="kudo-avatar" />
            <div>
              <div class="kudo-sender">{{ kudo.senderName || kudo.sender }}</div>
              <div class="kudo-date">{{ new Date(kudo.createdAt).toLocaleDateString('vi-VN') }}</div>
            </div>
          </div>
          <div class="kudo-message" v-html="sanitizeHtml(kudo.message)"></div>
          <div class="kudo-icon">
            <i v-if="isIconClass(kudo.icon)" :class="kudo.icon"></i>
            <span v-else>{{ kudo.icon }}</span>
          </div>
        </div>
      </div>
    </section>

    <div class="kudos-empty-state legacy-kudos-block" v-if="false">
      <div class="kudos-illustration">
        <div class="star-medal-illustration">
          <i class="fa-solid fa-star"></i>
          <div class="ribbon ribbon-left"></div>
          <div class="ribbon ribbon-right"></div>
        </div>
      </div>
      <h2>Không có lời khen ngợi nào gần đây</h2>
      <p>Gửi lời khen ngợi để cảm ơn một thành viên trong nhóm, ăn mừng một chiến thắng nhỏ hoặc ghi nhận một công việc được hoàn thành xuất sắc.</p>
      <button class="primary-btn" @click="isGiveKudosOpen = true">Gửi lời khen ngợi</button>
    </div>

    <!-- Kudos Feed -->
    <div class="kudos-feed legacy-kudos-block" v-if="false">
      <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 24px;">
        <h2 style="font-size: 20px; font-weight: 500; color: #172B4D; margin: 0;">Khen ngợi gần đây</h2>
        <button class="primary-btn" @click="isGiveKudosOpen = true">Gửi lời khen ngợi</button>
      </div>
      <div v-for="kudo in teamStore.kudos" :key="kudo.id" class="kudo-card" style="background: white; border: 1px solid #DFE1E6; border-radius: 8px; padding: 16px; margin-bottom: 16px;">
        <div style="display: flex; align-items: center; gap: 12px; margin-bottom: 12px;">
          <UserAvatar :user="{ fullName: kudo.senderName || kudo.sender, email: kudo.senderEmail, avatarUrl: kudo.senderAvatarUrl }" :size="32" :fontSize="14" class="kudo-avatar" />
          <div>
             <div style="font-weight: 500; color: #172B4D;">{{ kudo.senderName || kudo.sender }}</div>
            <div style="font-size: 12px; color: #6B778C;">{{ new Date(kudo.createdAt).toLocaleDateString('vi-VN') }}</div>
          </div>
        </div>
        <div style="font-size: 14px; color: #172B4D; line-height: 1.5; margin-bottom: 12px;" v-html="sanitizeHtml(kudo.message)"></div>
        <div style="font-size: 24px;">{{ kudo.icon }}</div>
      </div>
    </div>

    <!-- Give Kudos Full Screen Overlay -->
    <div class="give-kudos-overlay" v-if="isGiveKudosOpen" style="position: fixed; top: 0; left: 0; right: 0; bottom: 0; background: #FFF4F8; z-index: 9999; overflow-y: auto; display: flex; flex-direction: column;">
       
       <!-- Content -->
       <div style="flex: 1; display: flex; justify-content: center; padding-top: 40px;" @click="isKudosLinkDropdownOpen = false; isKudosTargetDropdownOpen = false; isKudosEmojiDropdownOpen = false">
          <div style="width: 100%; max-width: 600px; display: flex; flex-direction: column; gap: 24px; position: relative;">
             <!-- Top Actions -->
             <div style="display: flex; justify-content: flex-end; margin-bottom: -16px;">
                <button class="icon-btn" @click="isGiveKudosOpen = false" style="background: transparent; border: none; font-size: 14px; font-weight: 500; cursor: pointer; color: #42526E; display: flex; align-items: center; gap: 8px; padding: 4px 8px; border-radius: 4px; transition: background 0.1s;" onmouseover="this.style.background='#EBECF0'" onmouseout="this.style.background='transparent'">Quay lại <i class="fa-solid fa-arrow-right"></i></button>
             </div>
             <div style="position: relative;">
                 <div style="display: flex; align-items: center; gap: 8px; font-weight: 500; font-size: 14px; color: #0052CC; cursor: pointer; padding: 8px 12px; border: 1px solid #4C9AFF; border-radius: 4px; display: inline-flex;" @click.stop="isKudosTargetDropdownOpen = !isKudosTargetDropdownOpen">
                    <UserAvatar v-if="kudosTargetType === 'user'" :user="kudosTargetData || {}" :size="24" :fontSize="10" />
                     <div v-else class="member-avatar-micro" style="background-color: #36B37E; color: white; width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 11px;">{{ kudosTargetAvatar }}</div>
                    Khen ngợi {{ kudosTargetName }}
                 </div>
                 
                 <!-- Target Dropdown -->
                 <div v-if="isKudosTargetDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 40px; left: 0; z-index: 10; width: 340px; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 8px 0; display: flex; flex-direction: column; max-height: 300px; overflow-y: auto;">
                    <div style="padding: 4px 12px; font-size: 11px; font-weight: 700; color: #5E6C84; text-transform: uppercase;">Mọi người</div>
                    <div v-for="user in kudosTargetUsers" :key="user.id" @click="selectKudosTarget('user', user)" style="display: flex; align-items: center; gap: 8px; padding: 8px 16px; cursor: pointer; transition: background 0.1s;" onmouseover="this.style.background='#FAFBFC'" onmouseout="this.style.background='transparent'">
                       <UserAvatar :user="{ ...user, fullName: user.fullName || user.name, avatarColor: getAvatarColor(user.email || user.id) }" :size="24" :fontSize="10" />
                       <span style="font-size: 14px; color: #172B4D;">{{ user.fullName || user.name }}</span>
                    </div>
                    <div style="padding: 4px 12px; font-size: 11px; font-weight: 700; color: #5E6C84; text-transform: uppercase; margin-top: 8px; border-top: 1px solid #DFE1E6; padding-top: 8px;">Đội ngũ</div>
                    <div v-for="t in teamStore.allTeams" :key="t.id" @click="selectKudosTarget('team', t)" style="display: flex; align-items: center; gap: 8px; padding: 8px 16px; cursor: pointer; transition: background 0.1s; background: #E6FCFF;" onmouseover="this.style.background='#B3F5FF'" onmouseout="this.style.background='#E6FCFF'">
                       <div class="member-avatar-micro" style="background-color: #36B37E; color: white; width: 24px; height: 24px; border-radius: 4px; display: flex; align-items: center; justify-content: center; font-size: 11px;">{{ t.name ? t.name.substring(0, 2).toUpperCase() : 'T' }}</div>
                       <div style="display: flex; flex-direction: column;">
                         <span style="font-size: 14px; color: #0052CC;">{{ t.name }} <i class="fa-solid fa-circle-check" style="font-size: 10px;"></i></span>
                         <span style="font-size: 11px; color: #6B778C;">Đội ngũ chính thức • {{ t.memberCount || 0 }} thành viên, kể cả bạn</span>
                       </div>
                    </div>
                 </div>
             </div>
             
             <!-- Text input that renders HTML or handles link replacement -->
             <div style="position: relative;">
                 <div 
                   ref="kudosEditorRef"
                   class="kudos-editor"
                   contenteditable="true"
                   @input="e => kudosText = e.target.innerHTML"
                   style="width: 100%; min-height: 60px; font-size: 20px; color: #172B4D; outline: none; border: none; background: transparent; line-height: 1.5; padding: 8px 0; font-weight: 400; cursor: text;"
                   :data-placeholder="'Hãy cho ' + kudosTargetName + ' biết lý do bạn gửi lời khen ngợi này'"
                 ></div>
             </div>

             <!-- Icons toolbar -->
             <div style="display: flex; gap: 16px; color: #6B778C; font-size: 18px; align-items: center;">
               <div style="position: relative;">
                 <i class="fa-regular fa-face-smile" style="cursor: pointer;" @click.stop="isKudosEmojiDropdownOpen = !isKudosEmojiDropdownOpen"></i>
                 
                 <!-- Emoji Dropdown -->
                 <div v-if="isKudosEmojiDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 28px; left: 0; z-index: 10; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 12px; width: 340px; display: flex; flex-direction: column; gap: 8px;">
                    <input type="text" placeholder="Tìm kiếm icon..." v-model="kudosEmojiSearch" style="width: 100%; padding: 6px; border: 1px solid #DFE1E6; border-radius: 3px; outline: none; font-size: 13px;" />
                    <div style="display: grid; grid-template-columns: repeat(8, 1fr); gap: 6px; max-height: 200px; overflow-y: auto;">
                       <div v-for="emoji in filteredKudosEmojis" :key="emoji" @click="insertEmoji(emoji)" style="cursor: pointer; font-size: 20px; text-align: center; padding: 4px; border-radius: 4px; transition: background 0.1s;" onmouseover="this.style.background='#F4F5F7'" onmouseout="this.style.background='transparent'">
                          {{ emoji }}
                       </div>
                    </div>
                 </div>
               </div>
               
               <div style="position: relative;">
                 <i class="fa-solid fa-link" style="cursor: pointer;" @click.stop="isKudosLinkDropdownOpen = !isKudosLinkDropdownOpen"></i>
                 
                 <!-- Link Dropdown -->
                 <div v-if="isKudosLinkDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 24px; left: 0; z-index: 10; width: 340px; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 12px; display: flex; flex-direction: column; gap: 12px;">
                    <div>
                      <label style="font-size: 11px; font-weight: 600; color: #6B778C;">Tìm kiếm hoặc dán liên kết *</label>
                      <input type="text" placeholder="Tìm các liên kết gần đây hoặc dán một liên kết" style="width: 100%; margin-top: 4px; padding: 8px; border: 2px solid #4C9AFF; border-radius: 3px; outline: none;" v-model="kudosLinkSearch" />
                    </div>
                    <div>
                      <label style="font-size: 11px; font-weight: 600; color: #6B778C;">Văn bản hiển thị (không bắt buộc)</label>
                      <input type="text" placeholder="Văn bản cần hiển thị" style="width: 100%; margin-top: 4px; padding: 8px; border: 1px solid #DFE1E6; border-radius: 3px; outline: none;" v-model="kudosLinkDisplay" />
                      <div style="font-size: 11px; color: #6B778C; margin-top: 4px;">Cung cấp tiêu đề hoặc mô tả cho liên kết này</div>
                    </div>
                    
                    <div style="display: flex; gap: 16px; border-bottom: 1px solid #DFE1E6; padding-bottom: 8px;">
                      <span @click="kudosLinkTab = 'Home'" :style="{ fontSize: '13px', fontWeight: kudosLinkTab === 'Home' ? '600' : '500', color: kudosLinkTab === 'Home' ? '#0052CC' : '#6B778C', borderBottom: kudosLinkTab === 'Home' ? '2px solid #0052CC' : 'none', paddingBottom: '8px', cursor: 'pointer', marginBottom: '-9px' }">Home</span>
                      <span @click="kudosLinkTab = 'SprintA'" :style="{ fontSize: '13px', fontWeight: kudosLinkTab === 'SprintA' ? '600' : '500', color: kudosLinkTab === 'SprintA' ? '#0052CC' : '#6B778C', borderBottom: kudosLinkTab === 'SprintA' ? '2px solid #0052CC' : 'none', paddingBottom: '8px', cursor: 'pointer', marginBottom: '-9px' }">SprintA</span>
                    </div>

                    <div>
                      <h5 style="font-size: 11px; color: #6B778C; text-transform: uppercase; margin-bottom: 8px;">{{ kudosLinkTab === 'Home' ? 'Dự án trên Home' : 'Dự án của đội ngũ' }}</h5>
                      <div style="max-height: 150px; overflow-y: auto; display: flex; flex-direction: column; gap: 4px;">
                        <div v-for="item in (kudosLinkTab === 'Home' ? siteProjects : projects)" :key="item.id" @click="selectKudosLink(item)" style="display: flex; align-items: flex-start; gap: 8px; padding: 4px; cursor: pointer; border-radius: 3px; transition: background 0.1s;" onmouseover="this.style.background='#F4F5F7'" onmouseout="this.style.background='transparent'">
                          <i class="fa-solid fa-rocket" style="color: #6B778C; margin-top: 4px;"></i>
                          <div style="display: flex; flex-direction: column;">
                            <span style="font-size: 13px; color: #172B4D;">{{ item.name }}</span>
                            <span style="font-size: 11px; color: #6B778C;">{{ item.key || 'Dự án' }}</span>
                          </div>
                        </div>
                        <div v-if="(kudosLinkTab === 'Home' ? siteProjects : projects).length === 0" style="padding: 8px; font-size: 12px; color: #6B778C;">Không có dự án nào.</div>
                      </div>
                    </div>

                    <div style="display: flex; justify-content: flex-end; gap: 8px; margin-top: 8px;">
                      <button class="secondary-btn" @click="isKudosLinkDropdownOpen = false" style="height: 32px;">Hủy</button>
                      <button class="primary-btn" @click="insertKudosLink" style="height: 32px;">Chèn</button>
                    </div>
                 </div>
               </div>
             </div>

             <!-- Personalize Graphic Card -->
             <div style="width: 100%; height: 280px; border-radius: 8px; position: relative; overflow: hidden; display: flex; align-items: center; justify-content: center; box-shadow: 0 4px 12px rgba(0,0,0,0.1); cursor: pointer;" :style="{ background: selectedKudosGraphic.bg }" @click.stop="isKudosGraphicDropdownOpen = !isKudosGraphicDropdownOpen">
                <button class="secondary-btn" style="position: absolute; top: 12px; right: 12px; font-size: 12px; padding: 4px 8px; height: auto; pointer-events: none; color: white; background: rgba(255,255,255,0.2); border: none;">Cá nhân hóa</button>
                <div style="display: flex; flex-direction: column; align-items: center; justify-content: center; position: relative;">
                   <i :class="selectedKudosGraphic.icon1" :style="{ fontSize: '40px', color: selectedKudosGraphic.c1, position: 'absolute', right: '-40px', top: '-20px', transform: 'rotate(-15deg)' }"></i>
                   <i :class="selectedKudosGraphic.icon2" :style="{ fontSize: '100px', color: selectedKudosGraphic.c2, filter: 'drop-shadow(0 10px 10px rgba(0,0,0,0.2))' }"></i>
                </div>
                
                <!-- Graphic Picker Dropdown -->
                <div v-if="isKudosGraphicDropdownOpen" @click.stop class="dropdown-menu" style="position: absolute; top: 44px; right: 12px; z-index: 10; background: white; border-radius: 3px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); border: 1px solid #DFE1E6; padding: 12px; width: 320px; display: grid; grid-template-columns: repeat(4, 1fr); gap: 8px; max-height: 250px; overflow-y: auto;">
                   <div v-for="(g, index) in kudosGraphics" :key="index" @click="selectKudosGraphic(g)" style="width: 60px; height: 60px; border-radius: 4px; display: flex; align-items: center; justify-content: center; cursor: pointer; position: relative; overflow: hidden;" :style="{ background: g.bg }">
                      <i :class="g.icon2" :style="{ fontSize: '24px', color: g.c2 }"></i>
                   </div>
                </div>
             </div>

             <!-- Action Buttons -->
             <div style="display: flex; justify-content: flex-end; gap: 12px; margin-top: 8px;">
                <button class="secondary-btn" style="height: 36px; padding: 0 16px; font-size: 14px; font-weight: 500;" @click="isGiveKudosOpen = false">Hủy</button>
                <button class="primary-btn" :disabled="!kudosText" style="height: 36px; padding: 0 16px; font-size: 14px; font-weight: 500;" @click="submitKudos">Khen ngợi</button>
             </div>
          </div>
       </div>

    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted } from 'vue'
import { usePeopleStore } from '@/store/usePeopleStore'
import { useTeamStore } from '@/store/useTeamStore'
import { useHomeProjectStore } from '@/store/useHomeProjectStore'
import { useSiteStore } from '@/store/useSiteStore'
import UserAvatar from '@/components/common/UserAvatar.vue'
import ProjectPageToolbar from '@/components/common/ProjectPageToolbar.vue'
import ToolbarFilterMenu from '@/components/common/ToolbarFilterMenu.vue'
import DropdownFilter from '@/components/common/DropdownFilter.vue'

const peopleStore = usePeopleStore()
const teamStore = useTeamStore()
const projectStore = useHomeProjectStore()
const siteStore = useSiteStore()

onMounted(async () => {
  await teamStore.initializeRealtime()
  if (siteStore.sites.length === 0) {
    await siteStore.fetchSites()
  }
  if (!peopleStore.users || peopleStore.users.length === 0) {
    await peopleStore.fetchPeople()
  }
  if (!teamStore.allTeams || teamStore.allTeams.length === 0) {
    await teamStore.fetchAllTeams()
  }
  await projectStore.fetchProjects()
  await teamStore.fetchRecentKudos()
  
  if (!kudosTargetId.value) {
    const firstTeam = teamStore.allTeams[0]
    const firstPerson = peopleStore.users[0]
    if (firstTeam) {
      selectKudosTarget('team', firstTeam)
    } else if (firstPerson) {
      selectKudosTarget('user', firstPerson)
    }
  }
})

const sanitizeHtml = (value) => {
    return value;
}

// Kudos Variables
const isGiveKudosOpen = ref(false)
const kudosText = ref('')
const isKudosLinkDropdownOpen = ref(false)
const isKudosTargetDropdownOpen = ref(false)
const isKudosEmojiDropdownOpen = ref(false)
const kudosLinkSearch = ref('')
const kudosLinkDisplay = ref('')
const kudosLinkTab = ref('Home')
const kudosEmojiSearch = ref('')
const kudosSearch = ref('')
const filters = ref({
  sender: ''
})

const allKudos = computed(() => teamStore.kudos || [])
const senderOptions = computed(() => {
  return Array.from(new Set(allKudos.value.map(k => k.senderName || k.sender).filter(Boolean))).sort()
})

const activeFilterCount = computed(() => Object.values(filters.value).filter(Boolean).length)
const activeFilterItems = computed(() => [
  filters.value.sender ? { key: 'sender', label: 'Người gửi', icon: 'fa-regular fa-user', value: filters.value.sender } : null
].filter(Boolean))

const matchesFilterSearch = (label, search) => !search || String(label || '').toLowerCase().includes(search)

const clearFilters = () => {
  filters.value = {
    sender: ''
  }
}

const removeFilter = (key) => {
  if (Object.prototype.hasOwnProperty.call(filters.value, key)) {
    filters.value[key] = ''
  }
}

const isKudosGraphicDropdownOpen = ref(false)
const kudosGraphics = [
  { bg: '#0052CC', c1: '#FF8F73', c2: '#FF5630', icon1: 'fa-solid fa-fish-fins', icon2: 'fa-solid fa-box-open' },
  { bg: '#00875A', c1: '#FFC400', c2: '#FFAB00', icon1: 'fa-solid fa-star', icon2: 'fa-solid fa-trophy' },
  { bg: '#FF5630', c1: '#00B8D9', c2: '#008DA6', icon1: 'fa-solid fa-bolt', icon2: 'fa-solid fa-medal' },
  { bg: '#6554C0', c1: '#FF7452', c2: '#FF5630', icon1: 'fa-solid fa-heart', icon2: 'fa-solid fa-gem' },
  { bg: '#36B37E', c1: '#0052CC', c2: '#FFC400', icon1: 'fa-solid fa-thumbs-up', icon2: 'fa-solid fa-check-circle' },
  { bg: '#FFAB00', c1: '#6554C0', c2: '#FF5630', icon1: 'fa-solid fa-crown', icon2: 'fa-solid fa-award' },
  { bg: '#00B8D9', c1: '#36B37E', c2: '#FF8F73', icon1: 'fa-solid fa-lightbulb', icon2: 'fa-solid fa-rocket' },
  { bg: '#172B4D', c1: '#00B8D9', c2: '#0052CC', icon1: 'fa-solid fa-handshake', icon2: 'fa-solid fa-hand-holding-heart' }
]
const selectedKudosGraphic = ref(kudosGraphics[0])

const selectKudosGraphic = (g) => {
  selectedKudosGraphic.value = g
  isKudosGraphicDropdownOpen.value = false
}

const allEmojis = ['😀','😃','😄','😁','😆','😅','😂','🤣','😊','😇','🙂','🙃','😉','😌','😍','🥰','😘','😗','😙','😚','😋','😛','😝','😜','🤪','🤨','🧐','🤓','😎','🤩','🥳','😏','😒','😞','😔','😟','😕','🙁','☹️','😣','😖','😫','😩','🥺','😢','😭','😤','😠','😡','🤬','🤯','😳','🥵','🥶','😱','😨','😰','😥','😓','🤗','🤔','🤭','🤫','🤥','😶','😐','😑','😬','🙄','😯','😦','😧','😮','😲','🥱','😴','🤤','😪','😵','🤐','🥴','🤢','🤮','🤧','😷','🤒','🤕','🤑','🤠','😈','👿','👹','👺','🤡','💩','👻','💀','☠️','👽','👾','🤖','🎃','😺','😸','😹','😻','😼','😽','🙀','😿','😾','🙈','🙉','🙊','💥','💫','💦','💨','🐵','🐒','🦍','🦧','🐶','🐕','🦮','🐕‍🦺','🐩','🐺','🦊','🦝','🐱','🐈','🦁','🐯','🐅','🐆','🐴','🐎','🦄','🦓','🦌','🐮','🐂','🐃','🐄','🐷','🐖','🐗','🐽','🐏','🐑','🐐','🐪','🐫','🦙','🦒','🐘','🦏','🦛','🐭','🐁','🐀','🐹','🐰','🐇','🐿️','🦔','🦇','🐻','🐨','🐼','🦥','🦦','🦨','🦘','🦡','🐾','🦃','🐔','🐓','🐣','🐤','🐥','🐦','🐧','🕊️','🦅','🦆','🦢','🦉','🦩','🦚','🦜','🐸','🐊','🐢','🦎','🐍','🐲','🐉','🦕','🦖','🐳','🐋','🐬','🐟','🐠','🐡','🦈','🐙','🐚','🐌','🦋','🐛','🐜','🐝','🐞','🦗','🕷️','🕸️','🦂','🦟','🦠','💐','🌸','💮','🏵️','🌹','🥀','🌺','🌻','🌼','🌷','🌱','🌲','🌳','🌴','🌵','🌾','🌿','☘️','🍀','🍁','🍂','🍃','🍇','🍈','🍉','🍊','🍋','🍌','🍍','🥭','🍎','🍏','🍐','🍑','🍒','🍓','🥝','🍅','🥥','🥑','🍆','🥔','🥕','🌽','🌶️','🥒','🥬','🥦','🧄','🧅','🍄','🥜','🌰','🍞','🥐','🥖','🥨','🥯','🥞','🧇','🧀','🍖','🍗','🥩','🥓','🍔','🍟','🍕','🌭','🥪','🌮','🌯','🥙','🧆','🥚','🍳','🥘','🍲','🥣','🥗','🍿','🧈','🧂','🥫','🍱','🍘','🍙','🍚','🍛','🍜','🍝','🍠','🍢','🍣','🍤','🍥','🥮','🍡','🥟','🥠','🥡','🦀','🦞','🦐','🦑','🦪','🍦','🍧','🍨','🍩','🍪','🎂','🍰','🧁','🥧','🍫','🍬','🍭','🍮','🍯','🍼','🥛','☕','🍵','🍶','🍾','🍷','🍸','🍹','🍺','🍻','🥂','🥃','🥤','🧃','🧉','🧊','🥢','🍽️','🍴','🥄','🔪','🏺','🎉','👍','🚀','❤️','🔥','👏','🙌','💯','💪','✨','🌟']
const filteredKudosEmojis = computed(() => {
  if (!kudosEmojiSearch.value) return allEmojis
  return allEmojis.slice(0, 10)
})

const filteredKudos = computed(() => {
  const query = kudosSearch.value.trim().toLowerCase()
  let list = teamStore.kudos || []
  
  if (query) {
    list = list.filter((kudo) => `${kudo.senderName || kudo.sender || ''} ${kudo.senderEmail || ''} ${kudo.message || ''}`.toLowerCase().includes(query))
  }
  
  if (filters.value.sender) {
    list = list.filter((kudo) => (kudo.senderName || kudo.sender) === filters.value.sender)
  }
  
  return list
})

const isIconClass = (icon) => typeof icon === 'string' && icon.includes('fa-')

const kudosEditorRef = ref(null)

const kudosTargetType = ref('team')
const kudosTargetName = ref('Đội ngũ của bạn')
const kudosTargetAvatar = ref('Đ')
const kudosTargetId = ref(null)
const kudosTargetData = ref(null)

const kudosTargetUsers = computed(() => {
  return peopleStore.users || []
})

const siteProjects = computed(() => projectStore.projects || []) 
const projects = computed(() => projectStore.projects || []) 

const selectKudosTarget = (type, item) => {
  kudosTargetType.value = type
  kudosTargetName.value = item.name || item.fullName || item.email
  kudosTargetAvatar.value = item.initials || (kudosTargetName.value ? kudosTargetName.value.substring(0, 2).toUpperCase() : 'T')
  kudosTargetData.value = item
  kudosTargetId.value = item.id
  isKudosTargetDropdownOpen.value = false
}

const insertEmoji = (emoji) => {
  kudosText.value = (kudosText.value || '') + emoji
  if (kudosEditorRef.value) kudosEditorRef.value.innerHTML = kudosText.value
  isKudosEmojiDropdownOpen.value = false
}

const selectKudosLink = (item) => {
  kudosLinkSearch.value = item.name || item.title
  kudosLinkDisplay.value = item.name || item.title
}

const insertKudosLink = () => {
  const text = kudosLinkDisplay.value || kudosLinkSearch.value
  if (text) {
    const linkHtml = `<a href="/home/projects" style="color: #0052CC; text-decoration: none; font-weight: 500;" contenteditable="false">${text}</a>&nbsp;`
    kudosText.value = (kudosText.value || '') + ' ' + linkHtml
    if (kudosEditorRef.value) kudosEditorRef.value.innerHTML = kudosText.value
    isKudosLinkDropdownOpen.value = false
  }
}

const submitKudos = async () => {
  isGiveKudosOpen.value = false
  
  let finalMessage = kudosEditorRef.value?.innerHTML || kudosText.value

  const payload = {
    message: finalMessage,
    icon: selectedKudosGraphic.value?.icon2 || 'fa-solid fa-box-open'
  }
  
  if (kudosTargetType.value === 'team') {
    payload.departmentId = kudosTargetData.value?.id || kudosTargetId.value
  } else {
    payload.receiverId = kudosTargetData.value?.id || kudosTargetId.value
  }

  try {
    await teamStore.sendKudos(payload)
    kudosText.value = ''
    if (kudosEditorRef.value) kudosEditorRef.value.innerHTML = ''
  } catch (error) {
    console.error('Failed to submit kudos', error)
  }
}
</script>

<style scoped>
.team-kudos-container {
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;
  width: 100%;
}

.kudos-section {
  width: 100%;
}

.teams-content-panel {
  background: transparent;
  border: 0;
  border-radius: 0;
  padding: 0;
  box-shadow: none;
}

.section-header {
  margin: 0 0 16px;
}

.section-header h2 {
  color: #172B4D;
  font-size: 18px;
  font-weight: 750;
  line-height: 1.25;
  margin: 0;
}

.section-header p {
  color: #5E6C84;
  font-size: 13px;
  line-height: 1.45;
  margin: 4px 0 0;
}

.kudos-summary {
  color: #172B4D;
  font-size: 14px;
  font-weight: 700;
  margin: 24px 0 14px;
}

.kudos-empty-state {
  margin-top: 30px;
  
  min-height: 280px;
  text-align: center;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border: 1px dashed #C7D2E2;
  border-radius: 8px;
  padding: 36px 20px;
}

.kudos-illustration {
  margin-bottom: 24px;
}

.star-medal-illustration {
  position: relative;
  width: 80px;
  height: 80px;
  background-color: #FFC400;
  border-radius: 50%;
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 2;
  box-shadow: 0 4px 8px rgba(0,0,0,0.1);
  margin: 0 auto;
}

.star-medal-illustration i {
  font-size: 40px;
  color: #172B4D;
}

.ribbon {
  position: absolute;
  width: 20px;
  height: 40px;
  background-color: #0052CC;
  z-index: -1;
  top: 60px;
}

.ribbon-left {
  left: 10px;
  transform: rotate(30deg);
  clip-path: polygon(0 0, 100% 0, 100% 100%, 50% 80%, 0 100%);
}

.ribbon-right {
  right: 10px;
  transform: rotate(-30deg);
  clip-path: polygon(0 0, 100% 0, 100% 100%, 50% 80%, 0 100%);
}

.kudos-empty-state h2,
.kudos-empty-state h3 {
  font-size: 20px;
  font-weight: 700;
  color: #172B4D;
  margin: 0 0 8px 0;
}

.kudos-empty-state p {
  font-size: 14px;
  color: #5E6C84;
  line-height: 1.5;
  margin: 0 0 24px 0;
  max-width: 460px;
}

.kudos-feed {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 24px;
  margin-top: 30px;
}

.kudo-card {
  background: #ffffff;
  border: 1px solid #DFE1E6;
  border-radius: 8px;
  padding: 16px;
  min-height: 148px;
}

@media (max-width: 1280px) {
  .kudos-feed {
    grid-template-columns: repeat(3, minmax(0, 1fr));
  }
}

@media (max-width: 900px) {
  .kudos-feed {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 560px) {
  .kudos-feed {
    grid-template-columns: 1fr;
  }
}

.kudo-card-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.kudo-sender {
  font-weight: 700;
  color: #172B4D;
}

.kudo-date {
  color: #6B778C;
  font-size: 12px;
  margin-top: 2px;
}

.kudo-message {
  color: #172B4D;
  font-size: 14px;
  line-height: 1.5;
  margin-bottom: 12px;
}

.kudo-icon {
  color: #0C66E4;
  font-size: 24px;
  line-height: 1;
}

.primary-btn {
  background-color: #0052CC;
  color: white;
  border: none;
  padding: 8px 16px;
  border-radius: 3px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: background-color 0.2s;
}

.primary-btn:hover {
  background-color: #0047B3;
}
</style>
