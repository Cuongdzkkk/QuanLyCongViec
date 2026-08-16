export const slugifySpaceName = (value) => {
  const normalized = `${value || 'space'}`
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .toLowerCase()
    .trim()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')

  return normalized || 'space'
}

export const getSpaceRouteId = (spaceOrId) => {
  if (spaceOrId && typeof spaceOrId === 'object') {
    return spaceOrId.id || spaceOrId.Id || spaceOrId.projectId || spaceOrId.ProjectId || ''
  }

  return `${spaceOrId || ''}`
}

export const getSpaceRouteName = (spaceOrId) => {
  if (spaceOrId && typeof spaceOrId === 'object') {
    return spaceOrId.name || spaceOrId.Name || spaceOrId.key || spaceOrId.Key || ''
  }

  return ''
}

export const buildSpacePath = (spaceOrId, childPath = 'work-items') => {
  const id = getSpaceRouteId(spaceOrId)
  if (!id) return '/spaces'

  const slug = slugifySpaceName(getSpaceRouteName(spaceOrId))
  const suffix = `${childPath || ''}`.replace(/^\/+/, '')
  return `/space/${slug}/${id}${suffix ? `/${suffix}` : ''}`
}
