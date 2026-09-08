#!/bin/sh
# Entrypoint for the coffee-backup sidecar.
# Writes a cron job from BACKUP_CRON, runs an optional one-shot backup, then
# tails the log so `docker logs` can see it.
set -eu

BACKUP_CRON="${BACKUP_CRON:-0 3 * * *}"
RUN_ON_START="${BACKUP_RUN_ON_START:-false}"

# Persist environment for the cron job (busybox crond does not inherit env).
{
  echo "BACKUP_SOURCE='${BACKUP_SOURCE:-/app/data/coffee.db}'"
  echo "BACKUP_DIR='${BACKUP_DIR:-/backup}'"
  echo "BACKUP_RETENTION_DAYS='${BACKUP_RETENTION_DAYS:-14}'"
  echo "BACKUP_PREFIX='${BACKUP_PREFIX:-coffee}'"
  echo "BACKUP_TIMEOUT_MS='${BACKUP_TIMEOUT_MS:-30000}'"
} >/etc/backup.env

mkdir -p /var/log
touch /var/log/backup.log

# One-shot backup on container start makes it easy to verify the setup manually.
if [ "$RUN_ON_START" = "true" ]; then
  /usr/local/bin/backup.sh >> /var/log/backup.log 2>&1 || true
fi

# Install the cron schedule. Output is appended to /var/log/backup.log so
# `docker logs` can follow it via the tail at the bottom of this script.
printf '%s /usr/local/bin/backup.sh >> /var/log/backup.log 2>&1\n' "$BACKUP_CRON" | crontab -

# Start crond in the background; tail the log in the foreground so the
# container stays alive and `docker logs` shows backup output.
crond -b -l 2
exec tail -F /var/log/backup.log
