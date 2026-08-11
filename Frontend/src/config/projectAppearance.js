const category = (id, label, icons) => ({ id, label, icons })

export const PROJECT_ICON_CATEGORIES = [
  category('frequent', 'Frequently used', [
    'folder-fill', 'briefcase-fill', 'kanban-fill', 'rocket-takeoff-fill', 'collection-fill',
    'clipboard-check-fill', 'calendar3', 'graph-up-arrow', 'people-fill', 'stars'
  ]),
  category('business', 'Business', [
    'briefcase', 'briefcase-fill', 'building', 'building-fill', 'bank', 'shop', 'diagram-3',
    'person-workspace', 'people', 'people-fill', 'clipboard-data', 'clipboard-check', 'calendar3', 'flag-fill'
  ]),
  category('development', 'Development', [
    'code-slash', 'terminal', 'cpu', 'database', 'cloud', 'cloud-check', 'git', 'github',
    'bug', 'braces', 'window-stack', 'hdd-stack', 'router', 'ethernet', 'plugin', 'boxes'
  ]),
  category('analytics', 'Analytics', [
    'graph-up', 'graph-up-arrow', 'bar-chart', 'bar-chart-fill', 'pie-chart', 'pie-chart-fill',
    'speedometer2', 'activity', 'clipboard-data', 'table', 'bullseye', 'trophy', 'award', 'check2-circle'
  ]),
  category('design', 'Design', [
    'palette', 'palette-fill', 'brush', 'pen', 'pencil', 'bezier2', 'bounding-box-circles',
    'camera', 'image', 'images', 'layers', 'magic', 'stars', 'vector-pen'
  ]),
  category('system', 'System', [
    'gear', 'gear-fill', 'tools', 'wrench-adjustable', 'shield-lock', 'shield-check', 'key',
    'lock', 'sliders', 'toggles', 'lightning', 'lightning-fill', 'server', 'motherboard', 'inboxes-fill'
  ]),
  category('general', 'General', [
    'folder', 'folder-fill', 'collection', 'journal', 'journal-text', 'book', 'bookmark-fill',
    'globe', 'geo-alt-fill', 'megaphone', 'chat-square-text', 'envelope-fill', 'heart-fill',
    'star-fill', 'rocket', 'rocket-takeoff-fill', 'lightbulb-fill', 'box-seam-fill'
  ])
]

export const PROJECT_ICONS = Array.from(new Set(PROJECT_ICON_CATEGORIES.flatMap(group => group.icons)))

const background = (id, label, value, tone = 'dark') => ({ id, label, value, tone })
const backgroundGroup = (id, label, themes) => ({ id, label, themes })

export const PROJECT_BACKGROUND_GROUPS = [
  backgroundGroup('professional', 'Professional', [
    background('corporate-blue', 'Deep navy', 'linear-gradient(145deg, rgba(255,255,255,.045), rgba(15,23,42,.08)), #183153'),
    background('ocean', 'Ocean blue', 'linear-gradient(145deg, rgba(255,255,255,.055), rgba(8,47,73,.08)), #17647a'),
    background('muted-teal', 'Muted teal', 'linear-gradient(145deg, rgba(255,255,255,.05), rgba(15,23,42,.07)), #2d6663'),
    background('slate', 'Executive slate', 'linear-gradient(145deg, rgba(255,255,255,.045), rgba(15,23,42,.08)), #3d4b5f')
  ]),
  backgroundGroup('creative', 'Creative', [
    background('burgundy', 'Burgundy', 'linear-gradient(145deg, rgba(255,255,255,.045), rgba(69,10,10,.07)), #7a3345'),
    background('warm-amber', 'Warm amber', 'linear-gradient(145deg, rgba(255,255,255,.07), rgba(69,26,3,.06)), #a86825'),
    background('soft-purple', 'Lavender', 'linear-gradient(145deg, rgba(255,255,255,.06), rgba(30,27,75,.06)), #81739e'),
    background('rose', 'Dusty rose', 'linear-gradient(145deg, rgba(255,255,255,.055), rgba(76,5,25,.06)), #a45668')
  ]),
  backgroundGroup('nature', 'Nature', [
    background('forest', 'Forest', 'linear-gradient(145deg, rgba(255,255,255,.045), rgba(5,46,22,.08)), #315c45'),
    background('emerald', 'Eucalyptus', 'linear-gradient(145deg, rgba(255,255,255,.055), rgba(6,78,59,.07)), #3f7566'),
    background('sage', 'Soft sage', 'linear-gradient(145deg, rgba(255,255,255,.07), rgba(30,41,59,.05)), #788873'),
    background('coastal', 'Coastal blue', 'linear-gradient(145deg, rgba(255,255,255,.06), rgba(15,23,42,.06)), #527587')
  ]),
  backgroundGroup('minimal', 'Minimal', [
    background('minimal-white', 'Warm white', 'linear-gradient(145deg, rgba(255,255,255,.72), rgba(226,232,240,.28)), #f5f4f1', 'light'),
    background('neutral', 'Cool grey', 'linear-gradient(145deg, rgba(255,255,255,.5), rgba(203,213,225,.22)), #dfe4ea', 'light'),
    background('soft-blue', 'Mist blue', 'linear-gradient(145deg, rgba(255,255,255,.48), rgba(186,230,253,.18)), #dbe7ef', 'light'),
    background('indigo', 'Graphite', 'linear-gradient(145deg, rgba(255,255,255,.035), rgba(2,6,23,.08)), #293241')
  ])
]

export const PROJECT_BACKGROUNDS = PROJECT_BACKGROUND_GROUPS.flatMap(group => group.themes)

const LEGACY_BACKGROUND_ALIASES = {
  'aurora-purple': 'soft-purple',
  'royal-purple': 'soft-purple',
  cyber: 'muted-teal',
  'dark-professional': 'corporate-blue',
  sunset: 'warm-amber',
  orange: 'warm-amber',
  'teal-mesh': 'muted-teal',
  'violet-mesh': 'soft-purple',
  blueprint: 'corporate-blue',
  'soft-emerald': 'sage',
  ice: 'soft-blue',
  'warm-paper': 'warm-amber',
  'rose-mist': 'rose'
}

export const DEFAULT_PROJECT_ICON = 'folder-fill'
export const DEFAULT_PROJECT_BACKGROUND = 'corporate-blue'

const legacyIconMap = {
  '🚀': 'rocket-takeoff-fill',
  '⚡': 'lightning-fill',
  '💡': 'lightbulb-fill',
  '🔥': 'activity',
  '🎯': 'bullseye',
  '📦': 'box-seam-fill',
  '🛠️': 'tools',
  '📌': 'pin-angle-fill',
  '🌱': 'flower1',
  '🏁': 'flag-fill',
  '😎': 'folder-fill',
  '👇': 'folder-fill'
}

export const normalizeProjectIcon = (icon) => {
  const value = String(icon || '').trim().replace(/^bi-/, '')
  if (PROJECT_ICONS.includes(value)) return value
  return legacyIconMap[value] || DEFAULT_PROJECT_ICON
}

export const getProjectBackground = (backgroundId) => {
  const value = String(backgroundId || '').trim()
  const normalizedValue = LEGACY_BACKGROUND_ALIASES[value] || value
  const preset = PROJECT_BACKGROUNDS.find(item => item.id === normalizedValue)
  if (preset) return preset

  if (value && (value.includes('gradient(') || value.startsWith('#') || value.startsWith('url('))) {
    return { id: value, label: 'Custom', value, tone: 'dark' }
  }

  return PROJECT_BACKGROUNDS.find(item => item.id === DEFAULT_PROJECT_BACKGROUND)
}

export const getProjectBackgroundStyle = (backgroundId) => getProjectBackground(backgroundId).value
export const getProjectIconColor = (backgroundId) => getProjectBackground(backgroundId).tone === 'light' ? '#0f172a' : '#ffffff'
