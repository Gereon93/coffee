#!/bin/sh
# Coffee Analytics Hub — automated SQLite backup via sqlite3 .backup
# Designed to run as a sidecar container; can also be invoked directly.
set -eu

SOURCE="${BACKUP_SOURCE:-/app/data/coffee.db}"
DEST_DIR="${BACKUP_DIR:-/backup}"
RETENTION_DAYS="${BACKUP_RETENTION_DAYS:-14}"
PREFIX="${BACKUP_PREFIX:-coffee}"
TIMEOUT_MS="${BACKUP_TIMEOUT_MS:-30000}"

log() {
  printf '%s %s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')" "$*"
}

mkdir -p "$DEST_DIR"

if [ ! -f "$SOURCE" ]; then
  log "ERROR source database not found: $SOURCE"
  exit 1
fi

timestamp=$(date -u +'%Y%m%d-%H%M%S')
dest="${DEST_DIR}/${PREFIX}-${timestamp}.db"
tmp="${dest}.tmp.$$"

log "INFO starting backup $SOURCE -> $dest"

# Use sqlite3 .backup, which creates a consistent online copy.
# A busy timeout lets the backup wait for a short write lock.
sqlite3 "$SOURCE" <<EOF
.timeout ${TIMEOUT_MS}
.backup '${tmp}'
EOF

mv "$tmp" "$dest"

if ! sqlite3 "$dest" "PRAGMA integrity_check;" | grep -qx "ok"; then
  log "ERROR integrity check failed for $dest"
  rm -f "$dest"
  exit 1
fi

# fsync the directory entry to reduce the chance of a half-written file on power loss.
sync "$dest" 2>/dev/null || true

log "OK backup created $dest"

# Retention: keep at most BACKUP_RETENTION_DAYS daily backups.
# -mtime is based on file modification time (UTC day boundaries on the host FS).
if [ "$RETENTION_DAYS" -ge 0 ]; then
  count=$(find "$DEST_DIR" -type f -name "${PREFIX}-*.db" -mtime +"$RETENTION_DAYS" | wc -l | tr -d ' ')
  if [ "$count" -gt 0 ]; then
    find "$DEST_DIR" -type f -name "${PREFIX}-*.db" -mtime +"$RETENTION_DAYS" -delete
    log "INFO removed $count backup(s) older than $RETENTION_DAYS day(s)"
  fi
fi
