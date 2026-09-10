#!/bin/sh
# FakeVeresiye backup: consistent SQLite snapshot -> gzip -> age-encrypt -> copy to a local
# (ideally external / second) drive, keeping the last N. No network required.
#
# Requires on the host:  sqlite3  age  gzip   (+ curl if FV_HEALTHCHECK_URL is set)
#   Debian/Ubuntu:  sudo apt install sqlite3 age
#
# Configure via environment variables (the systemd unit sets them):
#   FV_DB              path to the live database            (default ./data/fakeveresiye.db)
#   FV_BACKUP_DIR      where encrypted backups are written  (default /var/backups/fakeveresiye)
#                      -> point this at your backup drive, e.g. /mnt/backup/fakeveresiye
#   FV_KEEP           how many backups to retain            (default 30)
#   FV_AGE_RECIPIENT  age public key, "age1..."             (required)
#   FV_REQUIRE_MOUNT  if set, abort unless this path is a mountpoint (guards an unplugged drive)
#   FV_HEALTHCHECK_URL pinged on success (dead-man's switch) (optional)
set -eu

DB="${FV_DB:-./data/fakeveresiye.db}"
DEST="${FV_BACKUP_DIR:-/var/backups/fakeveresiye}"
KEEP="${FV_KEEP:-30}"
AGE_RECIPIENT="${FV_AGE_RECIPIENT:-}"
REQUIRE_MOUNT="${FV_REQUIRE_MOUNT:-}"
HEALTHCHECK_URL="${FV_HEALTHCHECK_URL:-}"

is_mounted() {
    if command -v mountpoint >/dev/null 2>&1; then
        mountpoint -q "$1"
    else
        grep -q "[[:space:]]$1[[:space:]]" /proc/self/mounts 2>/dev/null
    fi
}

[ -f "$DB" ]            || { echo "backup: database not found: $DB" >&2; exit 1; }
[ -n "$AGE_RECIPIENT" ] || { echo "backup: FV_AGE_RECIPIENT is not set" >&2; exit 1; }

if [ -n "$REQUIRE_MOUNT" ] && ! is_mounted "$REQUIRE_MOUNT"; then
    echo "backup: $REQUIRE_MOUNT is not mounted -- is the backup drive plugged in?" >&2
    exit 1
fi

mkdir -p "$DEST"
stamp=$(date +%Y%m%d-%H%M%S)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# 1. Consistent online snapshot. Uses SQLite's backup API, so it is safe while the app is
#    running and correct whether the DB is in rollback-journal or WAL mode.
sqlite3 "$DB" ".backup '$work/db'"

# 2. Refuse to keep a corrupt snapshot.
if [ "$(sqlite3 "$work/db" 'PRAGMA integrity_check;')" != "ok" ]; then
    echo "backup: integrity_check failed" >&2
    exit 1
fi

# 3. Compress, then encrypt -- the data is personal (names, phones, balances), so it must
#    never sit on a drive in the clear.
gzip -9 "$work/db"
out="$DEST/fakeveresiye-$stamp.db.gz.age"
age -r "$AGE_RECIPIENT" -o "$out" "$work/db.gz"
sync "$out" 2>/dev/null || sync   # flush to the (possibly removable) drive

# 4. Rotate.
ls -1t "$DEST"/fakeveresiye-*.db.gz.age 2>/dev/null | tail -n +"$((KEEP + 1))" | xargs -r rm -f

# 5. Dead-man's switch (optional): a monitor alerts you if this stops running.
[ -n "$HEALTHCHECK_URL" ] && curl -fsS -m 10 "$HEALTHCHECK_URL" >/dev/null 2>&1 || true

echo "backup ok: $out ($(du -h "$out" | cut -f1))"
