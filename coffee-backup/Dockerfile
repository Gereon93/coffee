# Coffee Analytics Hub — SQLite backup sidecar
FROM alpine:3.21

RUN apk add --no-cache sqlite

COPY backup.sh /usr/local/bin/backup.sh
COPY entrypoint.sh /usr/local/bin/entrypoint.sh

RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/entrypoint.sh

ENTRYPOINT ["entrypoint.sh"]
