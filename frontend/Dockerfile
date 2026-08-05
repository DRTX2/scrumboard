FROM node:20-alpine AS build
WORKDIR /app
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginxinc/nginx-unprivileged:1.29-alpine
LABEL org.opencontainers.image.source="https://github.com/DRTX2/scrumboard"
USER root
RUN apk upgrade --no-cache && apk add --no-cache gettext
COPY --from=build --chown=101:101 /app/dist/scrum-board/browser /usr/share/nginx/html
COPY --chown=101:101 nginx.conf.template /etc/nginx/nginx.conf.template
COPY --chown=101:101 docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod 755 /docker-entrypoint.sh
USER 101
EXPOSE 8080
ENTRYPOINT ["/docker-entrypoint.sh"]
