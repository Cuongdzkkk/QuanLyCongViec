const enabledValues = new Set(['1', 'true', 'yes', 'on'])
const configuredValue = import.meta.env.VITE_PROJECT_ACCESS_RESTRICTIONS_ENABLED

export const projectAccessRestrictionsEnabled = configuredValue == null
  ? false
  : enabledValues.has(String(configuredValue).trim().toLowerCase())
