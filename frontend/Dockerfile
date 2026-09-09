# syntax=docker/dockerfile:1

# Dev image only: runs the Vite dev server with hot reload (docker-compose.yml,
# `dev` profile). The full frontend/ tree is bind-mounted over this at runtime,
# so the COPY/npm ci below exists just to warm the npm cache into the image.
FROM node:24-alpine
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . .

ENV CHOKIDAR_USEPOLLING=true
EXPOSE 5173
# Invoke the vite binary directly rather than `npm run dev`: npm doesn't
# forward SIGTERM to its child process, so `docker compose down`/stop would
# otherwise wait out the full stop-grace-period timeout on every teardown.
ENTRYPOINT ["node_modules/.bin/vite", "--host", "0.0.0.0"]
