#!/bin/bash
# =============================================================================
# Docker Build & Push Script
# Usage: ./build.sh [service] [--no-push]
#
# Examples:
#   ./build.sh api          # Build + Push API
#   ./build.sh dashboard    # Build + Push Dashboard
#   ./build.sh all          # Build + Push both
#   ./build.sh api --no-push # Build only, no push
# =============================================================================

set -e

# --- Configuration (edit these for other repos) ---
REGISTRY="192.168.2.143:5050"
PROJECT="gereon/coffee"
DOCKER="${DOCKER:-podman}"

declare -A SERVICES=(
  [api]="CoffeeApi:coffee-api"
  [dashboard]="coffee-dashboard:coffee-dashboard"
)
# Format: [name]="build_context:image_name"
# -------------------------------------------------

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GIT_COMMIT="$(git -C "$SCRIPT_DIR" rev-parse --short HEAD 2>/dev/null || echo 'unknown')"
SERVICE="${1:-all}"
NO_PUSH=false
[[ "$2" == "--no-push" || "$3" == "--no-push" ]] && NO_PUSH=true

build_and_push() {
  local name="$1"
  local config="${SERVICES[$name]}"

  if [[ -z "$config" ]]; then
    echo -e "${RED}Unknown service: $name${NC}"
    echo "Available: ${!SERVICES[*]}"
    exit 1
  fi

  local build_context="${config%%:*}"
  local image_name="${config##*:}"
  local full_image="${REGISTRY}/${PROJECT}/${image_name}"
  local timestamp
  timestamp=$(date +%Y%m%d-%H%M%S)

  echo -e "${YELLOW}━━━ Building ${name} ━━━${NC}"
  echo "  Context:  ${build_context}"
  echo "  Image:    ${full_image}"
  echo ""

  $DOCKER build \
    --build-arg BUILD_COMMIT="${GIT_COMMIT}" \
    -t "${full_image}:latest" \
    -t "${full_image}:${timestamp}" \
    "${SCRIPT_DIR}/${build_context}"

  echo -e "${GREEN}Build OK${NC}"

  if [[ "$NO_PUSH" == false ]]; then
    echo -e "${YELLOW}Pushing...${NC}"
    $DOCKER push "${full_image}:latest"
    $DOCKER push "${full_image}:${timestamp}"
    echo -e "${GREEN}Pushed: ${full_image}:latest${NC}"
    echo -e "${GREEN}Pushed: ${full_image}:${timestamp}${NC}"
  else
    echo -e "${YELLOW}Skipped push (--no-push)${NC}"
  fi

  echo ""
}

echo ""
echo "======================================="
echo " Docker Build & Push"
echo " Registry: ${REGISTRY}/${PROJECT}"
echo "======================================="
echo ""

if [[ "$SERVICE" == "all" ]]; then
  for svc in "${!SERVICES[@]}"; do
    build_and_push "$svc"
  done
else
  build_and_push "$SERVICE"
fi

echo -e "${GREEN}Done.${NC}"
