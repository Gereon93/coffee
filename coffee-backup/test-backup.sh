#!/bin/sh
# Smoke tests for backup.sh — run locally or in CI.
set -eu

ROOT="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
BACKUP_SH="${ROOT}/backup.sh"

TMP=$(mktemp -d)
ENV_TEST_RESTORE_PATH=""
ENV_TEST_HAD_PRIOR=0

restore_system_backup_env_if_needed() {
  [ -n "$ENV_TEST_RESTORE_PATH" ] || return 0
  if [ "$ENV_TEST_HAD_PRIOR" = "1" ]; then
    if [ "$(id -u)" -eq 0 ]; then
      cp "$ENV_TEST_RESTORE_PATH" /etc/backup.env
    else
      sudo cp "$ENV_TEST_RESTORE_PATH" /etc/backup.env
    fi
  else
    if [ "$(id -u)" -eq 0 ]; then
      rm -f /etc/backup.env
    else
      sudo rm -f /etc/backup.env
    fi
  fi
  rm -f "$ENV_TEST_RESTORE_PATH"
  ENV_TEST_RESTORE_PATH=""
  ENV_TEST_HAD_PRIOR=0
}

trap 'restore_system_backup_env_if_needed; rm -rf "$TMP"' EXIT

SOURCE_DB="${TMP}/coffee.db"
BACKUP_DIR="${TMP}/backups"
CUSTOM_BACKUP_DIR="${TMP}/custom-backups"

sqlite3 "$SOURCE_DB" 'CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (42);'

set_file_mtime_days_ago() {
  file=$1
  days=$2
  if touch -d "${days} days ago" "$file" 2>/dev/null; then
    return 0
  fi
  python3 - "$file" "$days" <<'PY'
import os, sys, time
path, days = sys.argv[1], int(sys.argv[2])
when = time.time() - days * 86400
os.utime(path, (when, when))
PY
}

write_backup_env_file() {
  env_content=$1
  if [ "$(id -u)" -eq 0 ]; then
    printf '%s\n' "$env_content" >/etc/backup.env
    return
  fi
  if ! sudo -n true 2>/dev/null; then
    echo "SKIP test_env_file_sourcing (passwordless sudo required to write /etc/backup.env)"
    return 1
  fi
  printf '%s\n' "$env_content" | sudo tee /etc/backup.env >/dev/null
}

assert_eq() {
  expected=$1
  actual=$2
  message=$3
  if [ "$expected" != "$actual" ]; then
    echo "FAIL: $message (expected '$expected', got '$actual')" >&2
    exit 1
  fi
}

test_successful_backup() {
  echo "test_successful_backup"
  rm -rf "$BACKUP_DIR"
  BACKUP_SOURCE="$SOURCE_DB" BACKUP_DIR="$BACKUP_DIR" BACKUP_PREFIX=test sh "$BACKUP_SH"

  backup_file=$(find "$BACKUP_DIR" -type f -name 'test-*.db' | head -n 1)
  if [ -z "$backup_file" ]; then
    echo "FAIL: backup file not created" >&2
    exit 1
  fi

  assert_eq "42" "$(sqlite3 "$backup_file" 'SELECT x FROM t;')" "backup preserves data"
  assert_eq "ok" "$(sqlite3 "$backup_file" 'PRAGMA integrity_check;')" "backup passes integrity check"
}

test_missing_source_fails() {
  echo "test_missing_source_fails"
  if BACKUP_SOURCE="${TMP}/missing.db" BACKUP_DIR="$BACKUP_DIR" sh "$BACKUP_SH" >/dev/null 2>&1; then
    echo "FAIL: expected missing source to fail" >&2
    exit 1
  fi
}

test_env_file_sourcing() {
  echo "test_env_file_sourcing"
  rm -rf "$CUSTOM_BACKUP_DIR"
  mkdir -p "$CUSTOM_BACKUP_DIR"

  env_content="BACKUP_SOURCE='${SOURCE_DB}'
BACKUP_DIR='${CUSTOM_BACKUP_DIR}'
BACKUP_PREFIX='envtest'
BACKUP_RETENTION_DAYS='14'"

  ENV_TEST_RESTORE_PATH=$(mktemp)
  if [ -f /etc/backup.env ]; then
    cp /etc/backup.env "$ENV_TEST_RESTORE_PATH"
    ENV_TEST_HAD_PRIOR=1
  else
    ENV_TEST_HAD_PRIOR=0
  fi

  if ! write_backup_env_file "$env_content"; then
    rm -f "$ENV_TEST_RESTORE_PATH"
    ENV_TEST_RESTORE_PATH=""
    ENV_TEST_HAD_PRIOR=0
    return 0
  fi

  env -u BACKUP_SOURCE -u BACKUP_DIR -u BACKUP_PREFIX sh "$BACKUP_SH"

  backup_file=$(find "$CUSTOM_BACKUP_DIR" -type f -name 'envtest-*.db' | head -n 1)
  if [ -z "$backup_file" ]; then
    echo "FAIL: /etc/backup.env values were not applied" >&2
    exit 1
  fi

  restore_system_backup_env_if_needed
}

test_invalid_retention_fails() {
  echo "test_invalid_retention_fails"
  if BACKUP_SOURCE="$SOURCE_DB" BACKUP_DIR="$BACKUP_DIR" BACKUP_RETENTION_DAYS=oops sh "$BACKUP_SH" >/dev/null 2>&1; then
    echo "FAIL: expected invalid retention to fail" >&2
    exit 1
  fi
}

test_retention_deletes_old_backups() {
  echo "test_retention_deletes_old_backups"
  rm -rf "$BACKUP_DIR"
  mkdir -p "$BACKUP_DIR"

  stale_backup="${BACKUP_DIR}/retain-20200101-120000.db"
  cp "$SOURCE_DB" "$stale_backup"
  set_file_mtime_days_ago "$stale_backup" 20

  BACKUP_SOURCE="$SOURCE_DB" BACKUP_DIR="$BACKUP_DIR" BACKUP_PREFIX=retain BACKUP_RETENTION_DAYS=14 sh "$BACKUP_SH"

  if [ -f "$stale_backup" ]; then
    echo "FAIL: stale backup was not deleted" >&2
    exit 1
  fi

  count=$(find "$BACKUP_DIR" -type f -name 'retain-*.db' | wc -l | tr -d ' ')
  assert_eq "1" "$count" "retention keeps only recent backups"
}

test_successful_backup
test_missing_source_fails
test_invalid_retention_fails
test_env_file_sourcing
test_retention_deletes_old_backups

echo "All backup.sh smoke tests passed."
