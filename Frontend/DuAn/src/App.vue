<script setup>
import { 
  CheckCircle, 
  Layout, 
  Users, 
  TrendingUp, 
  Play, 
  ArrowRight,
  Zap,
  Clock,
  Github,
  Twitter,
  Instagram,
  Facebook
} from 'lucide-vue-next'

import dashboardPreview from './assets/task_management_dashboard_preview_1773375713763.png'
import focusImage from './assets/modern_desk_setup_product_design_1773375736492.png'

const features = [
  {
    title: 'Task Management',
    desc: 'Keep every project on track with easy-to-use task tools.',
    icon: CheckCircle,
    color: '#0061ff',
    bg: '#eff6ff'
  },
  {
    title: 'Kanban Boards',
    desc: 'Visualize your workflow and boost output in seconds.',
    icon: Layout,
    color: '#a855f7',
    bg: '#f5f3ff'
  },
  {
    title: 'Team Collaboration',
    desc: 'Chat, comment and collaborate in real-time with teams.',
    icon: Users,
    color: '#0ea5e9',
    bg: '#f0f9ff'
  },
  {
    title: 'Progress Tracking',
    desc: 'Get deatiled insights with robust analytics and reports.',
    icon: TrendingUp,
    color: '#3b82f6',
    bg: '#eff6ff'
  }
]

const kanbanColumns = [
  {
    title: 'TO DO',
    count: 3,
    items: [
      { title: 'Design System Update', tag: 'UI/UX', tagType: 'primary' },
      { title: 'Market Research', tag: 'Analysis', tagType: 'info' }
    ]
  },
  {
    title: 'IN PROGRESS',
    count: 2,
    items: [
      { title: 'API Integration', tag: 'Backend', tagType: 'warning' }
    ]
  },
  {
    title: 'REVIEW',
    count: 1,
    items: [
      { title: 'Prototype V2.0', tag: 'Design', tagType: 'success' }
    ]
  },
  {
    title: 'DONE',
    count: 4,
    items: [
      { title: 'User Authentication', tag: 'Core', tagType: 'danger' }
    ]
  }
]
</script>

<template>
  <div class="landing-page">
    <!-- Navbar -->
    <header class="navbar">
      <div class="container nav-content">
        <div class="logo">
          <div class="logo-icon"></div>
          <span>TaskFlow</span>
        </div>
        <nav class="nav-links">
          <a href="#">Product</a>
          <a href="#">Solutions</a>
          <a href="#">Pricing</a>
          <a href="#">Resources</a>
        </nav>
        <div class="nav-actions">
          <el-button link>Login</el-button>
          <el-button type="primary" round>Sign Up</el-button>
        </div>
      </div>
    </header>

    <!-- Hero Section -->
    <section class="hero-section text-center section-padding">
      <div class="container animate-fade-up">
        <div class="new-badge">
          <span>NEW</span> What's new in TaskFlow v3.0?
        </div>
        <h1 class="hero-title">
          Organize Work. Manage Projects.<br />
          <span class="text-primary">Move Faster.</span>
        </h1>
        <p class="hero-sub">
          The all-in-one task management platform for teams that want to ship<br />
          better products without the chaos.
        </p>
        <div class="hero-btns">
          <el-button type="primary" size="large" round class="btn-cta">Start Free</el-button>
          <el-button size="large" round class="btn-secondary">
            <template #icon><Play :size="16" /></template>
            Watch Demo
          </el-button>
        </div>

        <div class="dashboard-preview">
          <img :src="dashboardPreview" alt="Dashboard Preview" />
        </div>
      </div>
    </section>

    <!-- Features Grid -->
    <section class="features-section section-padding">
      <div class="container text-center">
        <h2 class="section-title">Everything you need in one place</h2>
        <p class="section-sub">Streamline your workflow with enterprise-grade tools.</p>
        
        <el-row :gutter="32" class="feature-grid">
          <el-col :xs="24" :sm="12" :md="6" v-for="f in features" :key="f.title">
            <div class="feature-card">
              <div class="icon-wrap" :style="{ backgroundColor: f.bg }">
                <component :is="f.icon" :size="24" :color="f.color" />
              </div>
              <h3>{{ f.title }}</h3>
              <p>{{ f.desc }}</p>
            </div>
          </el-col>
        </el-row>
      </div>
    </section>

    <!-- Pipeline Section -->
    <section class="pipeline-section section-padding bg-light">
      <div class="container">
        <div class="pipeline-header">
          <h2>Streamline Your Pipeline</h2>
          <div class="header-action">
            <a href="#" class="view-all">View all templates <ArrowRight :size="16" /></a>
          </div>
        </div>
        
        <el-row :gutter="24" class="kanban-wrapper">
          <el-col :md="6" v-for="col in kanbanColumns" :key="col.title">
            <div class="kanban-col">
              <div class="col-head">
                <span>{{ col.title }}</span>
                <span class="counter">{{ col.count }}</span>
              </div>
              <div class="tasks">
                <div v-for="task in col.items" :key="task.title" class="task-card">
                  <h4>{{ task.title }}</h4>
                  <el-tag :type="task.tagType" size="small">{{ task.tag }}</el-tag>
                </div>
              </div>
            </div>
          </el-col>
        </el-row>
      </div>
    </section>

    <!-- Focus Section -->
    <section class="focus-section section-padding">
      <div class="container">
        <el-row align="middle" :gutter="80">
          <el-col :md="12">
            <div class="focus-content">
              <h2 class="section-title text-left">Designed for Focus</h2>
              <p class="section-sub text-left">Cut through the noise with tools built for deep work and high-impact productivity.</p>
              
              <div class="focus-item">
                <div class="item-icon"><Zap :size="20" color="#0061ff" /></div>
                <div class="item-text">
                  <h4>Personalized Home</h4>
                  <p>A central dashboard for your daily tasks, upcoming deadlines and notifications.</p>
                </div>
              </div>
              <div class="focus-item">
                <div class="item-icon"><Clock :size="20" color="#0061ff" /></div>
                <div class="item-text">
                  <h4>Filter by priority</h4>
                  <p>Smart filtering allows you to focus strictly on what matters most right now.</p>
                </div>
              </div>
            </div>
          </el-col>
          <el-col :md="12">
            <div class="focus-image">
              <img :src="focusImage" alt="Focus Focus" />
            </div>
          </el-col>
        </el-row>
      </div>
    </section>

    <!-- CTA Section -->
    <section class="cta-section container">
      <div class="cta-card">
        <h2>Start Managing Your Work<br />More Efficiently</h2>
        <p>Join over 10,000+ teams who have optimized their workflow with TaskFlow. No credit card required.</p>
        <el-button type="primary" size="large" class="cta-primary-btn">Create Free Account</el-button>
        <p class="small-note">Free for teams up to 5 users</p>
      </div>
    </section>

    <!-- Footer -->
    <footer class="footer">
      <div class="container">
        <el-row :gutter="40">
          <el-col :md="8">
            <div class="logo white">
              <div class="logo-icon white"></div>
              <span>TaskFlow</span>
            </div>
            <p class="footer-about">
              Empowering teams to ship high-quality products faster through intelligent task management and seamless collaboration.
            </p>
            <div class="socials">
              <Twitter :size="20" />
              <Github :size="20" />
              <Instagram :size="20" />
              <Facebook :size="20" />
            </div>
          </el-col>
          <el-col :md="4">
            <h4>Product</h4>
            <ul>
              <li><a href="#">Features</a></li>
              <li><a href="#">Integrations</a></li>
              <li><a href="#">Templates</a></li>
              <li><a href="#">Changelog</a></li>
            </ul>
          </el-col>
          <el-col :md="4">
            <h4>Company</h4>
            <ul>
              <li><a href="#">About Us</a></li>
              <li><a href="#">Careers</a></li>
              <li><a href="#">Blog</a></li>
              <li><a href="#">Press</a></li>
            </ul>
          </el-col>
          <el-col :md="4">
            <h4>Support</h4>
            <ul>
              <li><a href="#">Help Center</a></li>
              <li><a href="#">Status</a></li>
              <li><a href="#">API Docs</a></li>
              <li><a href="#">Security</a></li>
            </ul>
          </el-col>
        </el-row>
        <div class="footer-bottom">
          <p>© 2024 TaskFlow Technology Inc. All rights reserved.</p>
          <div class="bottom-links">
            <a href="#">Privacy Policy</a>
            <a href="#">Terms of Service</a>
            <a href="#">Cookies</a>
          </div>
        </div>
      </div>
    </footer>
  </div>
</template>

<style scoped>
.landing-page {
  overflow-x: hidden;
}

/* Navbar */
.navbar {
  height: 80px;
  display: flex;
  align-items: center;
  position: sticky;
  top: 0;
  background: rgba(255, 255, 255, 0.9);
  backdrop-filter: blur(10px);
  z-index: 1000;
  border-bottom: 1px solid #f0f0f0;
}

.navbar .container {
  max-width: 1440px;
  width: 100%;
}

.nav-content {
  display: flex;
  align-items: center;
  width: 100%;
}

.logo {
  display: flex;
  align-items: center;
  gap: 10px;
  font-weight: 800;
  font-size: 20px;
  color: #1a1a1a;
  flex-shrink: 0;
}

.logo-icon {
  width: 32px;
  height: 32px;
  background: var(--el-color-primary);
  border-radius: 8px;
}

.nav-links {
  display: flex;
  gap: 32px;
  margin-left: auto;
  margin-right: 48px;
}

.nav-links a {
  text-decoration: none;
  color: #666;
  font-weight: 500;
  font-size: 15px;
  transition: color 0.3s;
}

.nav-links a:hover {
  color: var(--el-color-primary);
}

/* Hero Section */
.hero-section {
  background: var(--bg-gradient);
  padding-bottom: 0;
}

.new-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 6px 16px;
  background: #f0f7ff;
  border-radius: 100px;
  font-size: 13px;
  font-weight: 600;
  color: var(--el-color-primary);
  margin-bottom: 24px;
}

.new-badge span {
  background: var(--el-color-primary);
  color: white;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
}

.hero-title {
  font-size: 64px;
  margin-bottom: 24px;
  color: #0d121f;
}

.hero-sub {
  font-size: 18px;
  color: #666;
  line-height: 1.6;
  margin-bottom: 40px;
}

.hero-btns {
  display: flex;
  justify-content: center;
  gap: 16px;
  margin-bottom: 60px;
}

.btn-cta {
  padding: 24px 40px;
  font-size: 16px;
  font-weight: 600;
}

.btn-secondary {
  padding: 24px 32px;
  font-size: 16px;
}

.dashboard-preview {
  margin-top: 40px;
  border-radius: 20px 20px 0 0;
  overflow: hidden;
  box-shadow: 0 -20px 60px rgba(0, 0, 0, 0.1);
  max-width: 1000px;
  margin-left: auto;
  margin-right: auto;
}

.dashboard-preview img {
  width: 100%;
  display: block;
}

/* Features */
.section-title {
  font-size: 36px;
  margin-bottom: 16px;
}

.section-sub {
  font-size: 18px;
  color: #666;
  margin-bottom: 60px;
}

.feature-grid {
  margin-top: 40px;
}

.feature-card {
  padding: 40px 24px;
  text-align: left;
  transition: transform 0.3s;
}

.feature-card:hover {
  transform: translateY(-5px);
}

.icon-wrap {
  width: 56px;
  height: 56px;
  border-radius: 16px;
  display: flex;
  align-items: center;
  justify-content: center;
  margin-bottom: 24px;
}

.feature-card h3 {
  font-size: 20px;
  margin-bottom: 12px;
}

.feature-card p {
  color: #666;
  line-height: 1.6;
}

/* Pipeline */
.bg-light {
  background-color: #f8faff;
}

.pipeline-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 48px;
}

.view-all {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--el-color-primary);
  text-decoration: none;
  font-weight: 600;
}

.kanban-col {
  height: 100%;
}

.col-head {
  display: flex;
  justify-content: space-between;
  padding: 12px 16px;
  background: white;
  border-radius: 12px;
  font-weight: 700;
  font-size: 14px;
  margin-bottom: 16px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.03);
}

.counter {
  color: #999;
}

.task-card {
  background: white;
  padding: 20px;
  border-radius: 12px;
  margin-bottom: 12px;
  box-shadow: 0 4px 12px rgba(0,0,0,0.03);
}

.task-card h4 {
  font-size: 15px;
  margin-bottom: 12px;
}

/* Focus Section */
.text-left {
  text-align: left;
}

.focus-content {
  padding-right: 40px;
}

.focus-item {
  display: flex;
  gap: 20px;
  margin-top: 32px;
}

.item-icon {
  width: 48px;
  height: 48px;
  background: #f0f7ff;
  border-radius: 12px;
  display: flex;
  align-items: center;
  justify-content: center;
  flex-shrink: 0;
}

.item-text h4 {
  font-size: 18px;
  margin-bottom: 8px;
}

.item-text p {
  color: #666;
  line-height: 1.6;
}

.focus-image img {
  width: 100%;
  border-radius: 32px;
  box-shadow: 0 24px 64px rgba(0,0,0,0.1);
}

/* CTA */
.cta-section {
  padding: 80px 24px;
}

.cta-card {
  background: var(--footer-bg);
  padding: 80px 40px;
  border-radius: 32px;
  text-align: center;
  color: white;
  position: relative;
  overflow: hidden;
}

.cta-card h2 {
  font-size: 48px;
  margin-bottom: 24px;
}

.cta-card p {
  font-size: 18px;
  opacity: 0.8;
  margin-bottom: 40px;
}

.cta-primary-btn {
  padding: 28px 48px;
  font-size: 18px;
  font-weight: 700;
}

.small-note {
  margin-top: 20px;
  font-size: 14px !important;
  opacity: 0.6 !important;
}

/* Footer */
.footer {
  background: var(--footer-bg);
  padding: 80px 0 40px;
  color: white;
}

.logo.white { color: white; }
.logo-icon.white { background: #fff; }

.footer-about {
  margin: 24px 0;
  opacity: 0.6;
  line-height: 1.6;
}

.socials {
  display: flex;
  gap: 20px;
}

.socials svg {
  opacity: 0.6;
  cursor: pointer;
  transition: opacity 0.3s;
}

.socials svg:hover { opacity: 1; }

.footer h4 {
  font-size: 18px;
  margin-bottom: 24px;
}

.footer ul {
  list-style: none;
}

.footer ul li {
  margin-bottom: 12px;
}

.footer ul li a {
  color: white;
  text-decoration: none;
  opacity: 0.6;
  transition: opacity 0.3s;
}

.footer ul li a:hover { opacity: 1; }

.footer-bottom {
  margin-top: 80px;
  padding-top: 32px;
  border-top: 1px solid rgba(255,255,255,0.1);
  display: flex;
  justify-content: space-between;
  opacity: 0.4;
  font-size: 14px;
}

.bottom-links {
  display: flex;
  gap: 24px;
}

.bottom-links a {
  color: white;
  text-decoration: none;
}
</style>
