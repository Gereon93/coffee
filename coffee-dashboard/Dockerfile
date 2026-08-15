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

# The entrypoint renders /etc/nginx/templates/*.template with envsubst at start.
# The filter limits substitution to API_KEY so nginx's own $host / $remote_addr
# survive untouched.
COPY nginx.conf.template /etc/nginx/templates/default.conf.template
ENV NGINX_ENVSUBST_FILTER="^API_KEY$"

# Shared secret the API expects on its write endpoints. Empty => the API
# answers 401 on writes; reads are unaffected.
ENV API_KEY=""

EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
