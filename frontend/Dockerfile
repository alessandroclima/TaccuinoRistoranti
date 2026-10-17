FROM node:22.19.0-bookworm-slim AS build
WORKDIR /app

COPY frontend/package*.json ./
RUN npm install

COPY frontend/ ./
ARG VITE_API_URL=http://backend:8080
ENV VITE_API_URL=${VITE_API_URL}
RUN npm run build

FROM nginx:1.29.2-bookworm AS runtime
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
