FROM node:22-alpine AS build
WORKDIR /app
COPY frontend/omnix-dashboard/package.json frontend/omnix-dashboard/package-lock.json ./
RUN npm ci
COPY frontend/omnix-dashboard/ ./
RUN npm run build

FROM nginx:1.27-alpine
COPY deployment/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist/omnix-dashboard/browser /usr/share/nginx/html
