import { ADMIN_USER_DIRECTORY_ROLES, SYSTEM_ADMIN_ROLES } from '@/utils/permissions'

const adminMeta = {
  requiresSystemAdminAccess: true,
  requiredRoles: SYSTEM_ADMIN_ROLES
}

export default [
  {
    path: '/admin',
    redirect: '/admin/system/general-configuration',
    meta: { requiresSystemAdminAccess: true }
  },
  {
    path: '/admin/audit-log',
    name: 'AdminAuditLog',
    component: () => import('../views/admin/AuditLog.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/billing',
    name: 'AdminBilling',
    component: () => import('../views/admin/BillingManagement.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/billing/payments',
    name: 'AdminBillingPayments',
    component: () => import('../views/admin/BillingManagement.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/users',
    name: 'AdminUserManagement',
    component: () => import('../views/admin/UserManagement.vue'),
    meta: {
      requiredRoles: ADMIN_USER_DIRECTORY_ROLES
    }
  },
  {
    path: '/admin/roles',
    name: 'AdminRoleManagement',
    component: () => import('../views/admin/RoleManagement.vue'),
    meta: {
      requiredRoles: ADMIN_USER_DIRECTORY_ROLES
    }
  },
  {
    path: '/admin/organization/profile',
    name: 'AdminOrgProfile',
    component: () => import('../views/admin/OrganizationProfile.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/organization/contact',
    name: 'AdminOrgContact',
    component: () => import('../views/admin/OrganizationContact.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/configuration',
    name: 'AdminConfiguration',
    component: () => import('../views/admin/Configuration.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/system/general-configuration',
    name: 'AdminSystemGeneralConfiguration',
    component: () => import('../views/admin/system/GeneralConfiguration.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/system/info',
    name: 'AdminSystemInfo',
    component: () => import('../views/admin/system/SystemInfo.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/instance/general',
    redirect: '/admin/system/general-configuration'
  },
  {
    path: '/admin/instance/authentication',
    name: 'AdminInstanceAuthentication',
    component: () => import('../views/admin/instance/AuthenticationManagement.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/instance/email',
    name: 'AdminInstanceEmail',
    component: () => import('../views/admin/instance/EmailManagement.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/customization',
    redirect: '/admin/configuration',
    meta: { requiresSystemAdminAccess: true }
  },
  {
    path: '/settings',
    redirect: '/admin/system/general-configuration',
    meta: { requiresSystemAdminAccess: true }
  },
  {
    path: '/admin/security/2fa',
    name: 'AdminSecurity2FA',
    component: () => import('../views/admin/security/TwoFactorAuth.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/security/password',
    name: 'AdminSecurityPassword',
    component: () => import('../views/admin/security/ChangePassword.vue'),
    meta: adminMeta
  },
  {
    path: '/admin/security/ip-whitelist',
    name: 'AdminSecurityIpWhitelist',
    component: () => import('../views/admin/security/IpWhitelist.vue'),
    meta: adminMeta
  }
]
