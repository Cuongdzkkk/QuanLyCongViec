/**
 * permissionGuard.js
 * Utility for managing SprintA SME role-permission presets and client-side guards.
 */

// ────────────────────────────────────────────
// All available permission keys grouped by module
// ────────────────────────────────────────────
export const ALL_PERMISSIONS = [
  // Project
  'project.view', 'project.create', 'project.update', 'project.delete',
  // Task
  'task.view', 'task.create', 'task.update', 'task.delete', 'task.assign', 'task.changeStatus',
  // Goal
  'goal.view', 'goal.create', 'goal.update', 'goal.delete',
  // People
  'people.view', 'people.invite', 'people.updateRole', 'people.remove',
  // Reports
  'report.view', 'report.export',
  // Settings
  'setting.view', 'setting.update',
  // Permissions
  'permission.view', 'permission.update'
]

// ────────────────────────────────────────────
// Default Permission Matrix Presets
// ────────────────────────────────────────────
export function getDefaultPermissionMatrix() {
  return {
    Owner: [...ALL_PERMISSIONS],
    Admin: ALL_PERMISSIONS.filter(p => p !== 'project.delete' && p !== 'permission.update'), // Admin has almost all except dangerous deletion/permission overrides of owner
    Manager: [
      'project.view', 'project.update',
      'task.view', 'task.create', 'task.update', 'task.assign', 'task.changeStatus',
      'goal.view', 'goal.create', 'goal.update',
      'people.view',
      'report.view', 'report.export',
      'setting.view'
    ],
    Member: [
      'project.view',
      'task.view', 'task.create', 'task.update', 'task.changeStatus',
      'goal.view',
      'people.view',
      'report.view'
    ],
    Viewer: [
      'project.view',
      'task.view',
      'goal.view',
      'people.view',
      'report.view'
    ]
  }
}

// ────────────────────────────────────────────
// Normalization and Role Mapping
// ────────────────────────────────────────────
export function normalizeRole(role) {
  if (!role) return ''
  return String(role).trim().toUpperCase()
}

/**
 * Maps a backend WorkspaceRole or ProjectRole into one of the 5 SME presets:
 * - Owner
 * - Admin
 * - Manager
 * - Member
 * - Viewer
 */
export function mapProjectRoleToPreset(role) {
  const norm = normalizeRole(role)
  
  // Workspace Roles
  if (norm === 'OWNER') return 'Owner'
  if (norm === 'ADMIN' || norm === 'SYSTEM_ADMIN' || norm === 'WORKSPACE_ADMIN') return 'Admin'
  
  // Project Roles / Managers
  if (['PM', 'PO', 'PROJECT_MANAGER', 'PROJECT_LEAD', 'LEAD', 'MANAGER'].includes(norm)) {
    return 'Manager'
  }
  
  // Project Members / Devs / QAs
  if (['DEVELOPER', 'DEV', 'QA', 'TESTER', 'MEMBER', 'SM', 'SCRUM_MASTER', 'BO'].includes(norm)) {
    return 'Member'
  }
  
  // Guests / Viewers
  if (['VIEWER', 'GUEST', 'CLIENT', 'STAKEHOLDER', 'READONLY'].includes(norm)) {
    return 'Viewer'
  }
  
  return 'Member' // Default fallback
}

// ────────────────────────────────────────────
// Permission Checker Logic
// ────────────────────────────────────────────
/**
 * Check if the mapped role has a specific permission based on the matrix.
 */
export function hasPermission(permissionMatrix, userRole, permissionKey) {
  if (!userRole) return false
  const presetRole = mapProjectRoleToPreset(userRole)
  const matrix = permissionMatrix || getDefaultPermissionMatrix()
  const allowedPermissions = matrix[presetRole] || []
  
  // Owner always has all permissions
  if (presetRole === 'Owner') {
    return true
  }
  
  return allowedPermissions.includes(permissionKey)
}

// ────────────────────────────────────────────
// Primary Guards
// ────────────────────────────────────────────
export function canCreateTask(permissionMatrix, userRole) {
  return hasPermission(permissionMatrix, userRole, 'task.create')
}

export function canUpdateTask(permissionMatrix, userRole) {
  return hasPermission(permissionMatrix, userRole, 'task.update')
}

export function canAssignTask(permissionMatrix, userRole) {
  return hasPermission(permissionMatrix, userRole, 'task.assign')
}

export function canChangeTaskStatus(permissionMatrix, userRole) {
  return hasPermission(permissionMatrix, userRole, 'task.changeStatus')
}

export function hasAssigneeOnlyTaskAccess(permissionMatrix, userRole) {
  return hasPermission(permissionMatrix, userRole, 'task.assigneeOnly')
}

export function canDeleteTask(permissionMatrix, userRole) {
  return hasPermission(permissionMatrix, userRole, 'task.delete')
}
