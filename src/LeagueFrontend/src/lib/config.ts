const fallbackApiBaseUrl = 'http://localhost:8080'

function normalizeApiBaseUrl(value: string | undefined) {
  if (!value) {
    return fallbackApiBaseUrl
  }

  const trimmedValue = value.trim()
  return trimmedValue.length > 0 ? trimmedValue : fallbackApiBaseUrl
}

function firstNonEmpty(...values: (string | undefined)[]) {
  return values.find((value) => value !== undefined && value.trim().length > 0)
}

export const apiBaseUrl = normalizeApiBaseUrl(
  firstNonEmpty(window.__APP_CONFIG__?.apiBaseUrl, import.meta.env.VITE_API_BASE_URL),
)

const blobBaseUrl = firstNonEmpty(
  window.__APP_CONFIG__?.blobBaseUrl,
  import.meta.env.VITE_BLOB_BASE_URL,
)

const blobContainerName = firstNonEmpty(
  window.__APP_CONFIG__?.blobContainerName,
  import.meta.env.VITE_BLOB_CONTAINER_NAME,
) ?? 'agentdata'

export function getAgentLogoUrl(agentId: string): string | null {
  if (!blobBaseUrl) return null
  return `${blobBaseUrl.replace(/\/$/, '')}/${blobContainerName}/${encodeURIComponent(agentId)}/logo.jpg`
}

export function getAgentBootstrapUrl(agentId: string): string | null {
  if (!blobBaseUrl) return null
  return `${blobBaseUrl.replace(/\/$/, '')}/${blobContainerName}/${encodeURIComponent(agentId)}/bootstrap.md`
}
