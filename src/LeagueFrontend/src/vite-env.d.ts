/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_API_BASE_URL?: string
  readonly VITE_BLOB_BASE_URL?: string
  readonly VITE_BLOB_CONTAINER_NAME?: string
}

interface Window {
  __APP_CONFIG__?: {
    apiBaseUrl?: string
    blobBaseUrl?: string
    blobContainerName?: string
  }
}
