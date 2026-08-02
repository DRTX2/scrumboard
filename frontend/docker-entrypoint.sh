#!/bin/sh
set -eu

: "${API_BASE_URL:=/api}"
: "${HUB_URL:=/hubs/boards}"
: "${APP_PORT:=8080}"
export API_BASE_URL HUB_URL APP_PORT

envsubst '${API_BASE_URL} ${HUB_URL}' \
  < /usr/share/nginx/html/assets/app-config.template.json \
  > /usr/share/nginx/html/assets/app-config.json
envsubst '${APP_PORT}' < /etc/nginx/nginx.conf.template > /tmp/nginx.conf

exec nginx -c /tmp/nginx.conf -g 'daemon off;'
