# Coffee Dashboard - React/Vite
FROM node:22-alpine AS build
WORKDIR /app

ARG BUILD_COMMIT=unknown
ENV BUILD_COMMIT=$BUILD_COMMIT

ARG VITE_SENTRY_DSN=""
ARG VITE_SENTRY_ENVIRONMENT=""
ARG VITE_SENTRY_RELEASE=""
ENV VITE_SENTRY_DSN=$VITE_SENTRY_DSN
ENV VITE_SENTRY_ENVIRONMENT=$VITE_SENTRY_ENVIRONMENT
ENV VITE_SENTRY_RELEASE=$VITE_SENTRY_RELEASE

COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npm run build

# Serve with nginx
FROM nginx:alpine
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
