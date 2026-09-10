#!/bin/sh
# FakeVeresiye backup: consistent SQLite snapshot -> gzip -> age-encrypt -> email + local copy.
#
# Requires on the host:  sqlite3  gzip  age  curl   (+ msmtp if FV_MAIL_TO is set)
#   Debian/Ubuntu:  sudo apt install sqlite3 age msmtp curl
#
# Configure with environment variables (the systemd unit sets them):
#   FV_DB              path to the live database        (default ./data/fakeveresiye.db)
#   FV_BACKUP_DIR      where local copies are kept      (default /var/backups/fakeveresiye)
#   FV_KEEP            how many local copies to retain  (default 14)
#   FV_AGE_RECIPIENT   age public key, "age1..."        (required)
#   FV_MAIL_TO         email address for the offsite copy   (optional; needs msmtp configured)
#   FV_HEALTHCHECK_URL pinged on success (dead-man's switch) (optional)
set -eu

DB="${FV_DB:-./data/fakeveresiye.db}"
DEST="${FV_BACKUP_DIR:-/var/backups/fakeveresiye}"
KEEP="${FV_KEEP:-14}"
AGE_RECIPIENT="${FV_AGE_RECIPIENT:-}"
MAIL_TO="${FV_MAIL_TO:-}"
HEALTHCHECK_URL="${FV_HEALTHCHECK_URL:-}"

[ -f "$DB" ]            || { echo "backup: database not found: $DB" >&2; exit 1; }
[ -n "$AGE_RECIPIENT" ] || { echo "backup: FV_AGE_RECIPIENT is not set" >&2; exit 1; }

mkdir -p "$DEST"
stamp=$(date +%Y%m%d-%H%M%S)
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

# 1. Consistent online snapshot. Uses SQLite's backup API, so it is safe while the app is
#    running and correct whether the DB is in rollback-journal or WAL mode.
sqlite3 "$DB" ".backup '$work/db'"

# 2. Refuse to ship a corrupt snapshot.
if [ "$(sqlite3 "$work/db" 'PRAGMA integrity_check;')" != "ok" ]; then
    echo "backup: integrity_check failed" >&2
    exit 1
fi

# 3. Compress, then encrypt — the data is personal (names, phones, balances), so it must
#    never sit in a mailbox or a backup folder in the clear.
gzip -9 "$work/db"
name="fakeveresiye-$stamp.db.gz.age"
out="$DEST/$name"
age -r "$AGE_RECIPIENT" -o "$out" "$work/db.gz"

# 4. Email the encrypted file as an attachment (the offsite copy).
if [ -n "$MAIL_TO" ]; then
    boundary="fv$stamp$$"
    {
        printf 'To: %s\n' "$MAIL_TO"
        printf 'Subject: FakeVeresiye yedek %s\n' "$stamp"
        printf 'MIME-Version: 1.0\n'
        printf 'Content-Type: multipart/mixed; boundary="%s"\n\n' "$boundary"
        printf -- '--%s\n' "$boundary"
        printf 'Content-Type: text/plain; charset=utf-8\n\n'
        printf 'FakeVeresiye veritabani yedegi ektedir (age ile sifreli).\n\n'
        printf -- '--%s\n' "$boundary"
        printf 'Content-Type: application/octet-stream; name="%s"\n' "$name"
        printf 'Content-Transfer-Encoding: base64\n'
        printf 'Content-Disposition: attachment; filename="%s"\n\n' "$name"
        base64 < "$out" | fold -w 76
        printf -- '\n--%s--\n' "$boundary"
    } | msmtp "$MAIL_TO"
fi

# 5. Rotate local copies.
ls -1t "$DEST"/fakeveresiye-*.db.gz.age 2>/dev/null | tail -n +"$((KEEP + 1))" | xargs -r rm -f

# 6. Dead-man's switch: tell a monitor the backup ran. If it stops, the monitor alerts you.
[ -n "$HEALTHCHECK_URL" ] && curl -fsS -m 10 "$HEALTHCHECK_URL" >/dev/null 2>&1 || true

echo "backup ok: $out"
