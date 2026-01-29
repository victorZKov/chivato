# ==========================================
# Chivato UI - Vite React
# ==========================================

# Build stage
FROM node:22-alpine AS build
WORKDIR /app

# Build arguments for Vite environment variables
ARG VITE_ENTRA_CLIENT_ID
ARG VITE_ENTRA_TENANT_ID
ARG VITE_API_URL=http://localhost:7071/api
ARG VITE_REDIRECT_URI=http://localhost:5280/

# Set environment variables for build
ENV VITE_ENTRA_CLIENT_ID=${VITE_ENTRA_CLIENT_ID}
ENV VITE_ENTRA_TENANT_ID=${VITE_ENTRA_TENANT_ID}
ENV VITE_API_URL=${VITE_API_URL}
ENV VITE_REDIRECT_URI=${VITE_REDIRECT_URI}

# Copy package files
COPY package*.json ./
RUN npm ci

# Copy source and build
COPY . .
RUN npm run build

# Production stage - Nginx
FROM nginx:alpine AS production
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]

# Development stage
FROM node:22-alpine AS development
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
EXPOSE 5280
CMD ["npm", "run", "dev", "--", "--host", "0.0.0.0"]
