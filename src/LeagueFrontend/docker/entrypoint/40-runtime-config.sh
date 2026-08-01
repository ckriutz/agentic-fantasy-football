#!/bin/sh
set -eu

escaped_api_base_url=$(printf '%s' "${API_BASE_URL:-}" | sed 's/[\\"]/\\&/g')

cat <<EOF >/usr/share/nginx/html/app-config.js
window.__APP_CONFIG__ = {
  apiBaseUrl: "${escaped_api_base_url}",
};
EOF
