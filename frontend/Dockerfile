# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM node:26-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

# ---- Runtime stage (non-root nginx serves the static SPA) ----
# Unprivileged image: runs as a non-root user and listens on 8080, so it works
# both in Docker and under OpenShift's arbitrary-UID model (can't bind :80).
FROM nginxinc/nginx-unprivileged:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/frontend/browser /usr/share/nginx/html
# Make the non-root user explicit (the base image defaults to it) so an accidental
# regression to root is visible in review.
USER 101
EXPOSE 8080

# Container liveness via the existing /healthz endpoint (BusyBox wget ships in the image).
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD wget -q -O /dev/null http://127.0.0.1:8080/healthz || exit 1
