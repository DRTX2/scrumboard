#!/usr/bin/env bash
set -euo pipefail

if [ "$#" -ne 1 ] || { [ "$1" != "staging" ] && [ "$1" != "production" ]; }; then
  echo "Usage: $0 <staging|production>" >&2
  exit 2
fi

repository="${GITHUB_REPOSITORY:-DRTX2/scrumboard}"
deployment_environment="$1"
required=(
  DATABASE_CONNECTION_STRING
  JWT_SIGNING_KEY
  PASSWORD_PEPPER
  BOOTSTRAP_ADMIN_EMAIL
  BOOTSTRAP_ADMIN_PASSWORD
)

for name in "${required[@]}"; do
  if [ -z "${!name:-}" ]; then
    echo "$name is required." >&2
    exit 1
  fi
  printf '%s' "${!name}" | gh secret set "$name" --repo "$repository" --env "$deployment_environment"
done

echo "Application secrets configured for $deployment_environment."
