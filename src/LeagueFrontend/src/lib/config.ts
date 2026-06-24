const fallbackApiBaseUrl = 'http://localhost:8080'

function normalizeApiBaseUrl(value: string | undefined) {
  if (!value) {
    return fallbackApiBaseUrl
  }

  const trimmedValue = value.trim()
  return trimmedValue.length > 0 ? trimmedValue : fallbackApiBaseUrl
}

export const apiBaseUrl = normalizeApiBaseUrl(
  window.__APP_CONFIG__?.apiBaseUrl ?? import.meta.env.VITE_API_BASE_URL,
)
