# syntax=docker/dockerfile:1

FROM node:22-alpine AS build
WORKDIR /app

COPY package.json package-lock.json ./
RUN npm ci

COPY . ./
RUN npm run build

FROM nginx:1.29-alpine AS final
WORKDIR /usr/share/nginx/html

COPY docker/nginx/default.conf /etc/nginx/conf.d/default.conf
COPY docker/entrypoint/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --from=build /app/dist ./

RUN chmod +x /docker-entrypoint.d/40-runtime-config.sh

ENV API_BASE_URL=http://localhost:8080
ENV BLOB_BASE_URL=
ENV BLOB_CONTAINER_NAME=agentdata

EXPOSE 80
