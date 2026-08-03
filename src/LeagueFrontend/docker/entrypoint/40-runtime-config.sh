#!/bin/sh
set -eu

# Prefer runtime env names; fall back to VITE_* for convenience.
api_base_url="${API_BASE_URL:-${VITE_API_BASE_URL:-}}"
blob_base_url="${BLOB_BASE_URL:-${VITE_BLOB_BASE_URL:-}}"
blob_container_name="${BLOB_CONTAINER_NAME:-${VITE_BLOB_CONTAINER_NAME:-agentdata}}"

escaped_api_base_url=$(printf '%s' "$api_base_url" | sed 's/[\\"]/\\&/g')
escaped_blob_base_url=$(printf '%s' "$blob_base_url" | sed 's/[\\"]/\\&/g')
escaped_blob_container_name=$(printf '%s' "$blob_container_name" | sed 's/[\\"]/\\&/g')

cat <<EOF >/usr/share/nginx/html/app-config.js
window.__APP_CONFIG__ = {
  apiBaseUrl: "${escaped_api_base_url}",
  blobBaseUrl: "${escaped_blob_base_url}",
  blobContainerName: "${escaped_blob_container_name}",
};
EOF
