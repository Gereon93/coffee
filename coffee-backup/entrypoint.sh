#!/bin/sh
# Entrypoint for the coffee-backup sidecar.
set -eu

if [ "$#" -gt 0 ]; then
  exec "$@"
fi

BACKUP_CRON="${BACKUP_CRON:-0 3 * * *}"
RUN_ON_START="${BACKUP_RUN_ON_START:-false}"

write_backup_env_file() {
  {
    echo "BACKUP_SOURCE='${BACKUP_SOURCE:-/app/data/coffee.db}'"
    echo "BACKUP_DIR='${BACKUP_DIR:-/backup}'"
    echo "BACKUP_RETENTION_DAYS='${BACKUP_RETENTION_DAYS:-14}'"
    echo "BACKUP_PREFIX='${BACKUP_PREFIX:-coffee}'"
    echo "BACKUP_TIMEOUT_MS='${BACKUP_TIMEOUT_MS:-30000}'"
  } >/etc/backup.env
}

install_backup_cron_job() {
  cron_schedule=$1
  if ! printf '%s /usr/local/bin/backup.sh >> /var/log/backup.log 2>&1\n' "$cron_schedule" | crontab -; then
    echo "ERROR failed to install cron schedule: $cron_schedule" >&2
    exit 1
  fi
  if ! crontab -l >/dev/null 2>&1; then
    echo "ERROR cron schedule missing after install: $cron_schedule" >&2
    exit 1
  fi
}

write_backup_env_file

mkdir -p /var/log
touch /var/log/backup.log

if [ "$RUN_ON_START" = "true" ]; then
  /usr/local/bin/backup.sh >> /var/log/backup.log 2>&1 || true
fi

install_backup_cron_job "$BACKUP_CRON"

crond -b -l 2
exec tail -F /var/log/backup.log
