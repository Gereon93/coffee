#!/bin/sh
# Coffee Analytics Hub — automated SQLite backup via sqlite3 .backup
# Designed to run as a sidecar container; can also be invoked directly.
set -eu
set -o pipefail

load_config() {
  if [ -f /etc/backup.env ]; then
    # shellcheck disable=SC1091
    . /etc/backup.env
  fi
  SOURCE="${BACKUP_SOURCE:-/app/data/coffee.db}"
  DEST_DIR="${BACKUP_DIR:-/backup}"
  RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
  PREFIX="${BACKUP_PREFIX:-coffee}"
  TIMEOUT_MS="${BACKUP_TIMEOUT_MS:-30000}"
}

log() {
  printf '%s %s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" "$*"
}

require_source_database() {
  mkdir -p "$DEST_DIR"
  if [ ! -f "$SOURCE" ]; then
    log "ERROR source database not found: $SOURCE"
    exit 1
  fi
}

create_online_sqlite_backup() {
  source_path=$1
  dest_path=$2
  timeout_ms=$3
  tmp_path="${dest_path}.tmp.$$"

  trap 'rm -f "$tmp_path" 2>/dev/null || true' EXIT

  log "INFO starting backup $source_path -> $dest_path"

  sqlite3 "$source_path" <<EOF
.timeout ${timeout_ms}
.backup '${tmp_path}'
EOF

  mv "$tmp_path" "$dest_path"
  trap - EXIT
}

verify_backup_integrity() {
  backup_path=$1
  if ! sqlite3 "$backup_path" "PRAGMA integrity_check;" | grep -qx "ok"; then
    log "ERROR integrity check failed for $backup_path"
    rm -f "$backup_path"
    exit 1
  fi
}

validate_retention_days() {
  retention_days=$1

  if [ -z "$retention_days" ]; then
    log "ERROR invalid BACKUP_RETENTION_DAYS: (empty)"
    exit 1
  fi

  case "$retention_days" in
    -1) ;;
    *[!0-9]*)
      log "ERROR invalid BACKUP_RETENTION_DAYS: $retention_days"
      exit 1
      ;;
  esac
}

flush_filesystem_buffers() {
  if ! sync; then
    log "ERROR filesystem sync failed"
    exit 1
  fi
}

apply_retention_policy() {
  dest_dir=$1
  prefix=$2
  retention_days=$3

  if [ "$retention_days" = "-1" ]; then
    return
  fi

  stale_files=$(find "$dest_dir" -type f -name "${prefix}-*.db" -mtime +"$retention_days")
  if [ -n "$stale_files" ]; then
    count=$(printf '%s\n' "$stale_files" | wc -l | tr -d ' ')
    find "$dest_dir" -type f -name "${prefix}-*.db" -mtime +"$retention_days" -delete
    log "INFO removed $count backup(s) older than $retention_days day(s)"
  fi
}

main() {
  load_config
  validate_retention_days "$RETENTION_DAYS"
  require_source_database

  timestamp=$(date -u +'%Y%m%d-%H%M%S')
  dest="${DEST_DIR}/${PREFIX}-${timestamp}.db"

  create_online_sqlite_backup "$SOURCE" "$dest" "$TIMEOUT_MS"
  verify_backup_integrity "$dest"
  flush_filesystem_buffers
  log "OK backup created $dest"
  apply_retention_policy "$DEST_DIR" "$PREFIX" "$RETENTION_DAYS"
}

main "$@"
