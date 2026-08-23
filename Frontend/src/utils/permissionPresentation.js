const MODULE_LABELS = Object.freeze({
  'admin.roles': 'Roles and permissions',
  'admin.security': 'Security',
  'admin.settings': 'Administration settings',
  'admin.users': 'User management',
  ai_assistant: 'AI assistant',
  analytics: 'Analytics',
  archives: 'Archives',
  audit_log: 'Audit log',
  chat: 'Chat',
  checkin: 'Daily check-in',
  dashboard: 'Dashboard',
  drafts: 'Drafts',
  feed: 'Activity feed',
  goals: 'Goals',
  integrations: 'Integrations',
  notifications: 'Notifications',
  people: 'People',
  priority: 'Priorities',
  profile: 'Profile',
  projects: 'Projects',
  rewards: 'Rewards',
  'space.cycles': 'Project cycles',
  'space.dashboard': 'Project dashboard',
  'space.intakes': 'Intakes',
  'space.members': 'Project members',
  'space.modules': 'Project modules',
  'space.pages': 'Pages',
  'space.reports': 'Project reports',
  'space.settings': 'Project settings',
  'space.timeline': 'Timeline',
  'space.views': 'Project views',
  'space.work_items': 'Work items',
  spaces: 'Workspaces',
  starred: 'Starred items',
  stickies: 'Stickies',
  system_status: 'System status',
  teams: 'Teams',
  'teams.dashboard': 'Team dashboard',
  views: 'Saved views',
  your_work: 'Your work'
})

const ACTION_LABELS = Object.freeze({
  view: 'View',
  can_view: 'View',
  create: 'Create',
  edit: 'Edit',
  update: 'Update',
  delete: 'Delete',
  archive: 'Archive',
  restore: 'Restore',
  export: 'Export',
  import: 'Import',
  invite: 'Invite',
  remove: 'Remove',
  send: 'Send',
  post: 'Post',
  comment: 'Comment',
  like: 'Like',
  submit: 'Submit',
  review: 'Review',
  assign: 'Assign',
  attachment: 'Manage attachments',
  manage: 'Manage',
  manage_2fa: 'Manage two-factor authentication',
  manage_ip_whitelist: 'Manage IP allowlist',
  manage_channel: 'Manage channels',
  manage_members: 'Manage members',
  manage_permissions: 'Manage permissions',
  manage_roles: 'Manage roles',
  manage_settings: 'Manage settings',
  manage_metrics: 'Manage metrics',
  update_progress: 'Update progress',
  change_status: 'Change status',
  use: 'Use'
})

const DESCRIPTION_LABELS = Object.freeze({
  'admin.roles.view': 'View roles and their access settings',
  'admin.roles.can_view': 'Open the role management area',
  'admin.roles.create': 'Create a new role',
  'admin.roles.edit': 'Edit a role name or description',
  'admin.roles.delete': 'Delete a custom role',
  'admin.roles.manage_permissions': 'Grant or remove permissions from roles',
  'admin.users.view': 'View users in the administration area',
  'admin.users.invite': 'Invite users to the workspace',
  'admin.users.edit': 'Edit user information',
  'admin.users.suspend': 'Suspend or reactivate a user',
  'admin.users.delete': 'Remove a user from the system',
  'space.work_items.view': 'View work items in a project',
  'space.work_items.create': 'Create a new work item',
  'space.work_items.edit': 'Edit work item details',
  'space.work_items.delete': 'Delete a work item',
  'space.work_items.assign': 'Assign work to a team member',
  'space.work_items.change_status': 'Move a work item to another status',
  'space.work_items.assignee_only': 'Assignee-only task access',
  'space.work_items.comment': 'Add comments to work items',
  'space.work_items.attachment': 'Add or manage work item attachments'
})

function getPermissionCode(permissionOrCode) {
  return typeof permissionOrCode === 'string'
    ? permissionOrCode
    : permissionOrCode?.code || ''
}

function humanizeToken(value) {
  return String(value || '')
    .replace(/_/g, ' ')
    .replace(/\b\w/g, char => char.toUpperCase())
}

export function getPermissionModuleLabel(moduleName) {
  if (!moduleName) return 'General'
  return MODULE_LABELS[moduleName] || moduleName.split('.').map(humanizeToken).join(' - ')
}

export function getPermissionActionLabel(permissionOrCode) {
  const code = getPermissionCode(permissionOrCode)
  if (DESCRIPTION_LABELS[code]) {
    return DESCRIPTION_LABELS[code].replace(/^(View|Open|Create|Edit|Delete|Grant|Move|Add|Assign|Manage) /, '$1 ')
  }
  const action = code.split('.').pop() || ''
  return ACTION_LABELS[action] || humanizeToken(action)
}

export function getPermissionDescription(permission) {
  const code = getPermissionCode(permission)
  return DESCRIPTION_LABELS[code] || permission?.description || `${getPermissionActionLabel(permission)} this area`
}

export function getPermissionCodeLabel(permission) {
  return getPermissionCode(permission)
}
