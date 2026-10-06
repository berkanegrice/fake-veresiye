# FakeVeresiye

A browser-based **veresiye** (customer credit / debt ledger) that imports real
**Veresiye 5** `.exa` backups. Built with ASP.NET Core 10 (controllers), EF Core / SQLite,
ClosedXML + QuestPDF, and a React 19 + TypeScript + Vite front end.

The `.exa` reader reconstructs the embedded SQLite `frm1.edb` from its zlib chunks, reads
the `CariKart` (customers) and `Data` (ledger) tables, and decodes the legacy Turkish
**Windows-1254** text correctly — the columns are read as BLOBs and decoded from code
page 1254, so characters like **Ç Ğ İ Ö Ş Ü** survive intact.

## Layout

```
FakeVeresiye.slnx
Dockerfile  compose.yaml    single-container deployment (API + SPA)
deploy/                     host backup job: backup.sh + systemd service/timer
src/
  FakeVeresiye.Api/      ASP.NET Core API + host for the built SPA
    Controllers/         Customers, Transactions, Import, Reports
    Dtos/                request/response contracts
    Services/Import/      Veresiye5BackupReader, BackupImportService, Excel import, PreviewStore
    Services/Statements/  StatementService + Excel/PDF exporters
    Models/  Data/
    fakeveresiye.db      seeded SQLite database (125 customers, 9,232 transactions)
  FakeVeresiye.Web/       React SPA
    src/api/  src/i18n/  src/components/  src/features/{customers,transactions,import,reports}
tests/
  FakeVeresiye.Api.Tests/  xUnit: backup decoding, statement math, export smoke tests
```

The two projects are separate. In **development** they run as two servers and the Vite dev
server proxies `/api` to the API. In **production** `dotnet publish` builds the React app
into `wwwroot/`, so one process serves both the API and the SPA.

## Requirements

- .NET SDK 10
- Node.js **20.19+ or 22.12+** (Vite 7) — for `npm run dev` / `npm run build` / `dotnet publish`.
  `dotnet run` on its own needs no Node.

## Run (development — two servers)

```
# terminal 1 — API, no Node involved
dotnet run --project src/FakeVeresiye.Api                 # http://localhost:5080

# terminal 2 — Vite dev server (needs Node 20.19+/22.12+)
npm --prefix src/FakeVeresiye.Web install
npm --prefix src/FakeVeresiye.Web run dev                 # http://localhost:5173
```

Open **http://localhost:5173**. The React app calls `/api/...` with relative URLs; Vite
forwards those to `:5080` (`server.proxy` in `vite.config.ts`). Both sides hot-reload.

To run both from one terminal, use any task runner, e.g.:

```
npx concurrently "dotnet run --project src/FakeVeresiye.Api" \
                 "npm --prefix src/FakeVeresiye.Web run dev"
```

## Publish (single self-contained app)

```
dotnet publish src/FakeVeresiye.Api -c Release -o publish
```

The `PublishSpa` MSBuild target runs `npm ci && npm run build` and copies `dist/` into
`wwwroot/`. Run the published `FakeVeresiye.Api` with no Vite process — `UseStaticFiles`
serves the bundle and `MapFallbackToFile` sends deep links to `index.html`. Same relative
`/api` URLs, now same-origin. (`-p:SkipSpaBuild=true` skips the npm step when the SPA was
already built and copied into `wwwroot/`, as the Docker build does.)

## Deploy

> **The app has no authentication.** Every endpoint is open and the data is personal (names,
> phone numbers, balances). Do **not** put it on the public internet without an auth layer in
> front — e.g. Cloudflare Access / a Cloudflare Tunnel, or a private network such as Tailscale.
> TLS is required too (any of the above provide it).

### Run on Linux (Docker Compose)

The `Dockerfile` builds the SPA (Node stage) and publishes the API (SDK stage) and needs no
.NET or Node on the host — just Docker Engine + the Compose plugin.

```bash
mkdir -p data && sudo chown 1654:1654 data   # the image runs as uid 1654; it must own /data
sudo mkdir -p /var/log/fakeveresiye && sudo chown 1654:1654 /var/log/fakeveresiye
docker compose up -d --build
sudo systemctl enable --now docker            # start the stack after a reboot
```

- Serves on **`http://localhost:8080`** (`compose.yaml` publishes `127.0.0.1:8080` only —
  change to `8080:8080` to reach it from other LAN devices).
- `restart: unless-stopped` brings the container back after a crash or reboot.
- **SQLite lives in `./data/fakeveresiye.db` on the host** (bind mount). `.dockerignore`
  excludes `*.db`, so it starts empty and migrations build the schema on first run — then
  import your `.exa` through the wizard.
- **The action log lives in `/var/log/fakeveresiye/` on the host** — deliberately a separate
  bind mount from `./data`, so losing or corrupting the data volume can't take the log that
  would help explain it down too. One file per day (`actions-YYYYMMDD.log`), 30 days kept.
- Update after a `git pull`: `docker compose up -d --build`.

For LAN HTTPS put Caddy in front; for remote access use Tailscale or a Cloudflare Tunnel.

### Backup & restore

`deploy/` has a host backup job (independent of the container, so it keeps running when the
app doesn't). Each run takes a consistent snapshot with SQLite's backup API, checks its
integrity, compresses and **`age`-encrypts** it (the data is PII), and writes it to a backup
drive, keeping the last 30. No network involved. Point `FV_BACKUP_DIR` at an external / second
drive and set `FV_REQUIRE_MOUNT` so a run aborts loudly if that drive isn't plugged in.

```bash
sudo apt install sqlite3 age            # + curl only if you use FV_HEALTHCHECK_URL

sudo install -D deploy/backup.sh /opt/fakeveresiye/deploy/backup.sh
sudo cp deploy/fakeveresiye-backup.{service,timer} /etc/systemd/system/

# generate the encryption key — keep the PRIVATE key OFF this machine (USB, password manager)
age-keygen -o backup-key.txt           # prints the age1... public key

# edit /etc/systemd/system/fakeveresiye-backup.service:
#   FV_DB=/opt/fakeveresiye/data/fakeveresiye.db
#   FV_BACKUP_DIR=/mnt/backup/fakeveresiye     FV_REQUIRE_MOUNT=/mnt/backup
#   FV_AGE_RECIPIENT=age1...

sudo systemctl daemon-reload
sudo systemctl start fakeveresiye-backup.service     # test one run now
sudo systemctl enable --now fakeveresiye-backup.timer
```

Restore:

```bash
docker compose down
age -d -i backup-key.txt fakeveresiye-YYYYMMDD-HHMMSS.db.gz.age | gunzip > data/fakeveresiye.db
docker compose up -d
```

The backup drive covers disk failure; for fire/theft, carry a copy off-site periodically or
add a cloud sync of `FV_BACKUP_DIR` later.

Keep a third copy on media you control (a USB the owner swaps) for a full 3-2-1 with no cloud.

## Features

- **Ledger**: server-paginated customer sidebar with Turkish-aware search; per-customer
  transaction list paginated and sortable by date / amount / type (asc or desc). Add/delete
  customers, add debt / add payment, **edit any past transaction** (amount, date, description —
  `PUT /api/transactions/{id}`), delete transactions. All data entry is in real modal dialogs.
- **Import .EXA**: 4-step wizard — preview (no writes) → validate against the DB → commit in
  one transaction. Idempotent on `(Source, ExternalId)`. Generic `.xlsx` import is also kept
  (`POST /api/import/excel`).
- **Customer statement** (`Reports` tab): search-pick a customer and a date range; see opening
  balance, per-line running balance, totals and closing balance, with debt red / payment green.
  Lines are paged (50/page) while the totals and running balances still span the whole window.
  Export to **Excel** (`statement.xlsx`) or **PDF** (`statement.pdf`) — always the full statement,
  fully Turkish and colour-coded.
- **Turkish / English**: TR is the default; toggle in the top bar (choice persisted in
  `localStorage`). Currency and dates use `tr-TR` formatting.

## API

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/api/customers?page=&pageSize=&search=` | paged list with computed balance |
| GET | `/api/customers/{id}` | detail: balance + transaction count |
| POST | `/api/customers` | create |
| DELETE | `/api/customers/{id}` | delete (cascades) |
| GET | `/api/customers/{id}/transactions?page=&pageSize=&sort=date\|amount\|type&dir=asc\|desc` | paged, sorted ledger |
| POST | `/api/customers/{id}/transactions` | add debt/payment |
| PUT | `/api/transactions/{id}` | edit amount/date/description |
| DELETE | `/api/transactions/{id}` | delete |
| POST | `/api/import/exa/preview` \| `/validate/{token}` \| `/import/{token}` | 3-phase `.exa` import |
| POST | `/api/import/excel` | generic `.xlsx` import |
| GET | `/api/reports/customers/{id}/statement?from=&to=&page=&pageSize=50` | statement JSON, one page of lines (totals & running balances span the whole window) |
| GET | `/api/reports/customers/{id}/statement.xlsx\|.pdf?from=&to=` | full statement export (Turkish, colour-coded) |

## Tests

```
dotnet test
```

## Known issue

`dotnet` reports advisory **GHSA-2m69-gcr7-jv3q** for `SQLitePCLRaw.lib.e_sqlite3 2.1.11`,
pulled transitively by `Microsoft.EntityFrameworkCore.Sqlite 10.0.0`. It will clear when EF
Core ships an updated SQLitePCLRaw.
