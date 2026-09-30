# Multi-stage Dockerfile for React + Vite SPA Client
# Stage 1: Build client bundle
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies (respecting package-lock if present)
COPY package*.json ./
RUN npm install

# Build static assets
COPY . .
RUN npm run build

# Stage 2: Serve via Nginx
FROM nginx:alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
