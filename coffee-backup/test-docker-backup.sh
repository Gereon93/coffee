#!/bin/sh
# Smoke tests for the coffee-backup Alpine image — run locally or in CI.
set -eu

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
IMAGE="coffee-backup:ci-smoke"
TMP=$(mktemp -d)
CONTAINER_ID=""

cleanup() {
  if [ -n "$CONTAINER_ID" ]; then
    docker rm -f "$CONTAINER_ID" >/dev/null 2>&1 || true
  fi
  rm -rf "$TMP"
}

trap cleanup EXIT

DATA_DIR="$TMP/data"
BACKUP_DIR="$TMP/backups"
mkdir -p "$DATA_DIR" "$BACKUP_DIR"

sqlite3 "$DATA_DIR/coffee.db" 'CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (7);'

docker build -t "$IMAGE" "$ROOT"

echo "test_docker_run_on_start_success"
CONTAINER_ID=$(docker run -d \
  -e BACKUP_RUN_ON_START=true \
  -e BACKUP_PREFIX=docker \
  -v "$DATA_DIR:/app/data:ro" \
  -v "$BACKUP_DIR:/backup" \
  "$IMAGE")

found=0
i=0
while [ "$i" -lt 30 ]; do
  if find "$BACKUP_DIR" -type f -name 'docker-*.db' | grep -q .; then
    found=1
    break
  fi
  i=$((i + 1))
  sleep 1
done

docker rm -f "$CONTAINER_ID" >/dev/null
CONTAINER_ID=""

if [ "$found" -ne 1 ]; then
  echo "FAIL: run-on-start did not create a backup via entrypoint" >&2
  exit 1
fi

backup_file=$(find "$BACKUP_DIR" -type f -name 'docker-*.db' | head -n 1)

if [ "$(sqlite3 "$backup_file" 'SELECT x FROM t;')" != "7" ]; then
  echo "FAIL: docker backup did not preserve data" >&2
  exit 1
fi

echo "test_docker_missing_source_fails_on_start"
if docker run --rm \
  -e BACKUP_RUN_ON_START=true \
  -v "$TMP/empty:/app/data:ro" \
  "$IMAGE" >/dev/null 2>&1; then
  echo "FAIL: expected startup backup failure when source is missing" >&2
  exit 1
fi

echo "test_docker_entrypoint_env_and_cron"
CONTAINER_ID=$(docker run -d \
  -e BACKUP_RUN_ON_START=false \
  -e BACKUP_RETENTION_DAYS=21 \
  -e BACKUP_CRON='12 4 * * *' \
  -v "$DATA_DIR:/app/data:ro" \
  -v "$BACKUP_DIR:/backup" \
  "$IMAGE")

sleep 2

if ! docker exec "$CONTAINER_ID" grep -q "BACKUP_RETENTION_DAYS='21'" /etc/backup.env; then
  echo "FAIL: entrypoint did not persist BACKUP_RETENTION_DAYS in /etc/backup.env" >&2
  exit 1
fi

if ! docker exec "$CONTAINER_ID" crontab -l | grep -q '12 4 \* \* \*'; then
  echo "FAIL: entrypoint did not install BACKUP_CRON schedule" >&2
  exit 1
fi

docker rm -f "$CONTAINER_ID" >/dev/null
CONTAINER_ID=""

echo "All coffee-backup docker smoke tests passed."
