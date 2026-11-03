#!/usr/bin/env bash
# Isolated nopCommerce verification harness.
# Subcommands: launch | doctor | search | cleanup
set -euo pipefail

SCRIPT_DIR=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
REPO_ROOT=$(cd "$SCRIPT_DIR/../../../.." && pwd)
WEB_SRC="$REPO_ROOT/src/Presentation/Nop.Web"
DLL="$WEB_SRC/bin/Debug/net10.0/Nop.Web.dll"
CURRENT_LINK=/tmp/nop-verify-current
DB_PASSWORD=${NOP_VERIFY_DB_PASSWORD:-nopCommerce_db_password}
DB_USER=${NOP_VERIFY_DB_USER:-nopcommerce}
ADMIN_EMAIL=${NOP_VERIFY_ADMIN_EMAIL:-verify-admin@example.com}
ADMIN_PASSWORD=${NOP_VERIFY_ADMIN_PASSWORD:-Verify_123}

die() { echo "verify-nop: $*" >&2; exit 1; }

require_tools() {
  command -v dotnet >/dev/null || die "dotnet is not on PATH"
  command -v psql >/dev/null || die "psql is not on PATH"
  command -v curl >/dev/null || die "curl is not on PATH"
  sudo -u postgres pg_isready -q || die "PostgreSQL is not accepting connections"
  [ -f "$DLL" ] || die "missing $DLL; build with: dotnet build src/NopCommerce.sln"
}

run_id_from_env_or_current() {
  if [ -n "${NOP_VERIFY_RUN_ID:-}" ]; then
    echo "$NOP_VERIFY_RUN_ID"
    return
  fi
  [ -L "$CURRENT_LINK" ] || die "no active run; launch one or set NOP_VERIFY_RUN_ID"
  basename "$(readlink -f "$CURRENT_LINK")" | sed 's/^nop-verify-//'
}

root_for() {
  echo "/tmp/nop-verify-$1"
}

load_state() {
  local run_id root
  run_id=$(run_id_from_env_or_current)
  root=$(root_for "$run_id")
  [ -f "$root/state.env" ] || die "missing $root/state.env"
  # shellcheck disable=SC1090
  source "$root/state.env"
}

prepare_site() {
  local site="$ROOT/site"
  mkdir -p "$site" "$site/Logs"
  local path base
  shopt -s nullglob dotglob
  for path in "$WEB_SRC"/* "$WEB_SRC"/.[!.]*; do
    base=$(basename "$path")
    case "$base" in
      App_Data|wwwroot|bin|obj|node_modules|Logs|logs) continue ;;
    esac
    ln -s "$path" "$site/$base"
  done
  shopt -u dotglob
  ln -s "$WEB_SRC/bin" "$site/bin"

  mkdir -p "$site/App_Data"
  cp -a "$WEB_SRC/App_Data/." "$site/App_Data/"
  rm -f "$site/App_Data/appsettings.json" \
        "$site/App_Data/installedPlugins.json" \
        "$site/App_Data/plugins.json" \
        "$site/App_Data/dataSettings.json"
  rm -rf "$site/App_Data/DataProtectionKeys"
  mkdir -p "$site/App_Data/DataProtectionKeys" "$site/App_Data/TempUploads"

  mkdir -p "$site/wwwroot/images"
  for path in "$WEB_SRC/wwwroot"/*; do
    base=$(basename "$path")
    if [ "$base" = "images" ]; then
      continue
    fi
    ln -s "$path" "$site/wwwroot/$base"
  done
  for path in "$WEB_SRC/wwwroot/images"/*; do
    base=$(basename "$path")
    case "$base" in
      thumbs|uploaded)
        mkdir -p "$site/wwwroot/images/$base"
        ;;
      *)
        ln -s "$path" "$site/wwwroot/images/$base"
        ;;
    esac
  done
}

port_in_use() {
  python3 - "$1" <<'PY'
import socket, sys
sock = socket.socket()
sock.settimeout(0.2)
try:
    code = sock.connect_ex(("127.0.0.1", int(sys.argv[1])))
finally:
    sock.close()
sys.exit(0 if code == 0 else 1)
PY
}

pick_port() {
  local port=5210
  while port_in_use "$port"; do
    port=$((port + 1))
    [ "$port" -lt 5400 ] || die "no free port in 5210-5399"
  done
  [ "$port" -ne 5000 ] || die "refusing the shared dev port 5000"
  echo "$port"
}

start_web() {
  local with_settings=$1
  if [ -f "$ROOT/pid" ]; then
    local old
    old=$(cat "$ROOT/pid" || true)
    if [ -n "$old" ] && kill -0 "$old" 2>/dev/null; then
      kill -- -"$old" 2>/dev/null || kill "$old" 2>/dev/null || true
      sleep 1
    fi
  fi
  unset ConnectionStrings__ConnectionString ConnectionStrings__DataProvider || true
  if [ "$with_settings" = "yes" ] && [ -f "$ROOT/site/App_Data/appsettings.json" ]; then
    :
  elif [ "$with_settings" = "yes" ]; then
    die "install did not write App_Data/appsettings.json"
  fi
  (
    export ASPNETCORE_CONTENTROOT="$ROOT/site"
    export ASPNETCORE_ENVIRONMENT=Development
    export ASPNETCORE_URLS="http://127.0.0.1:$PORT"
    export DOTNET_CLI_TELEMETRY_OPTOUT=1
    export DOTNET_NOLOGO=1
    export DOTNET_ROOT=${DOTNET_ROOT:-/usr/share/dotnet}
    cd "$ROOT/site"
    exec setsid dotnet "$DLL"
  ) >>"$ROOT/web.log" 2>&1 &
  echo $! >"$ROOT/pid"
}

wait_for_body() {
  local url=$1 needle=$2 tries=${3:-60}
  local i body
  for i in $(seq 1 "$tries"); do
    body=$(curl -sS -L --max-time 10 "$url" || true)
    if printf '%s' "$body" | grep -q "$needle"; then
      printf '%s' "$body"
      return 0
    fi
    sleep 2
  done
  return 1
}

install_store() {
  local body token
  body=$(wait_for_body "http://127.0.0.1:$PORT/install" "nopCommerce installation" 45) \
    || die "installer did not become ready; see $ROOT/web.log"
  printf '%s' "$body" >"$EVIDENCE/install-page.html"
  curl -sS -c "$ROOT/cookies" -b "$ROOT/cookies" -o "$ROOT/install.html" \
    "http://127.0.0.1:$PORT/install"
  token=$(python3 - "$ROOT/install.html" <<'PY'
import re, sys
html = open(sys.argv[1], encoding="utf-8", errors="replace").read()
m = re.search(r'name="__RequestVerificationToken" type="hidden" value="([^"]+)"', html)
if not m:
    raise SystemExit("missing antiforgery token")
print(m.group(1))
PY
)
  curl -sS -c "$ROOT/cookies" -b "$ROOT/cookies" \
    -o "$EVIDENCE/install-result.html" \
    -w "%{http_code}" \
    --max-time 300 \
    -H "Content-Type: application/x-www-form-urlencoded" \
    --data-urlencode "__RequestVerificationToken=$token" \
    --data-urlencode "AdminEmail=$ADMIN_EMAIL" \
    --data-urlencode "AdminPassword=$ADMIN_PASSWORD" \
    --data-urlencode "ConfirmPassword=$ADMIN_PASSWORD" \
    --data-urlencode "Country=US-en-US" \
    --data-urlencode "InstallSampleData=true" \
    --data-urlencode "SubscribeNewsletters=false" \
    --data-urlencode "DataProvider=3" \
    --data-urlencode "CreateDatabaseIfNotExists=false" \
    --data-urlencode "ConnectionStringRaw=false" \
    --data-urlencode "ServerName=localhost" \
    --data-urlencode "DatabaseName=$DB_NAME" \
    --data-urlencode "IntegratedSecurity=false" \
    --data-urlencode "Username=$DB_USER" \
    --data-urlencode "Password=$DB_PASSWORD" \
    --data-urlencode "UseCustomCollation=false" \
    "http://127.0.0.1:$PORT/install" >"$ROOT/install.code"
  if grep -q "validation-summary-errors" "$EVIDENCE/install-result.html"; then
    python3 - "$EVIDENCE/install-result.html" <<'PY' >&2
import re, sys
html = open(sys.argv[1], encoding="utf-8", errors="replace").read()
m = re.search(r'validation-summary-errors.*?<ul>(.*?)</ul>', html, re.S)
text = re.sub('<[^>]+>', ' ', m.group(1) if m else html[:500])
print("install failed:", " ".join(text.split()))
PY
    exit 1
  fi
  grep -q "window.location.replace" "$EVIDENCE/install-result.html" \
    || die "install response did not request an application restart"
  curl -sS -o /dev/null --max-time 20 "http://127.0.0.1:$PORT/install/restartapplication" || true
  local i pid
  pid=$(cat "$ROOT/pid")
  for i in $(seq 1 30); do
    kill -0 "$pid" 2>/dev/null || break
    sleep 1
  done
}

cmd_launch() {
  require_tools
  RUN_ID=${NOP_VERIFY_RUN_ID:-$(date -u +%Y%m%d%H%M%S)}
  case "$RUN_ID" in
    *[!0-9a-zA-Z]*) die "RUN_ID must be alphanumeric" ;;
  esac
  ROOT=$(root_for "$RUN_ID")
  DB_NAME="nopverify_${RUN_ID}"
  [ "$DB_NAME" != "nopcommerce" ] || die "refusing the shared nopcommerce database"
  EVIDENCE=${NOP_VERIFY_EVIDENCE:-/opt/cursor/artifacts/verify-nopcommerce/$RUN_ID}
  mkdir -p "$ROOT" "$EVIDENCE"
  PORT=$(pick_port)
  sudo -u postgres psql -v ON_ERROR_STOP=1 -d postgres <<SQL
DO \$\$
BEGIN
  IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = '${DB_USER}') THEN
    CREATE ROLE ${DB_USER} LOGIN PASSWORD '${DB_PASSWORD}' CREATEDB;
  END IF;
END
\$\$;
SELECT 'CREATE DATABASE ${DB_NAME} OWNER ${DB_USER}'
WHERE NOT EXISTS (SELECT FROM pg_database WHERE datname = '${DB_NAME}')\\gexec
SQL
  sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 \
    -c "CREATE EXTENSION IF NOT EXISTS citext; CREATE EXTENSION IF NOT EXISTS pgcrypto;"
  prepare_site
  cat >"$ROOT/state.env" <<EOF
RUN_ID=$RUN_ID
ROOT=$ROOT
PORT=$PORT
DB_NAME=$DB_NAME
DB_USER=$DB_USER
EVIDENCE=$EVIDENCE
ADMIN_EMAIL=$ADMIN_EMAIL
URL=http://127.0.0.1:$PORT
EOF
  ln -sfn "$ROOT" "$CURRENT_LINK"
  : >"$ROOT/web.log"
  start_web no
  install_store
  start_web yes
  wait_for_body "http://127.0.0.1:$PORT/" "Home page title" 45 >"$EVIDENCE/home.html" \
    || die "store homepage did not become ready; see $ROOT/web.log"
  echo "http://127.0.0.1:$PORT" >"$ROOT/url"
  cat "$ROOT/state.env"
  echo "ready http://127.0.0.1:$PORT"
}

cmd_doctor() {
  load_state
  local pid code title
  pid=$(cat "$ROOT/pid")
  kill -0 "$pid" 2>/dev/null || die "web pid $pid is not running"
  port_in_use "$PORT" || die "port $PORT is not listening"
  code=$(curl -sS -o "$ROOT/doctor.html" -w "%{http_code}" -L --max-time 20 "$URL/")
  [ "$code" = "200" ] || die "homepage HTTP $code"
  title=$(python3 - "$ROOT/doctor.html" <<'PY'
import re, sys
html = open(sys.argv[1], encoding="utf-8", errors="replace").read()
m = re.search(r"<title>([^<]+)</title>", html)
print(m.group(1) if m else "")
PY
)
  printf '%s' "$title" | grep -q "Home page" || die "unexpected title: $title"
  printf '%s' "$title" | grep -qi "installation" && die "instance is still the installer"
  sudo -u postgres psql -d "$DB_NAME" -tAc 'SELECT COUNT(*) FROM "Product"' >/dev/null \
    || die "Product table is missing in $DB_NAME"
  echo "doctor ok url=$URL pid=$pid db=$DB_NAME title=$title"
}

cmd_search() {
  load_state
  local query=${1:-MacBook}
  local dir="$EVIDENCE/catalog-search"
  mkdir -p "$dir"
  local code
  code=$(curl -sS -D "$dir/headers.txt" -o "$dir/response.html" -w "%{http_code}" -L --max-time 30 \
    "$URL/search?q=$(python3 -c 'import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1]))' "$query")")
  [ "$code" = "200" ] || die "search HTTP $code"
  grep -q "Apple MacBook Pro" "$dir/response.html" || die "search results omit Apple MacBook Pro"
  grep -q 'id="q"' "$dir/response.html" || die "search page is missing the q field"
  code=$(curl -sS -o "$dir/empty.html" -w "%{http_code}" -L --max-time 30 \
    "$URL/search?q=zzznomatchverify")
  [ "$code" = "200" ] || die "empty search HTTP $code"
  grep -q "No products were found that matched your criteria." "$dir/empty.html" \
    || die "empty search did not show the no-results message"
  local chrome=""
  if [ -x /opt/google/chrome/chrome ]; then
    chrome=/opt/google/chrome/chrome
  else
    for candidate in google-chrome google-chrome-stable chromium chromium-browser; do
      if command -v "$candidate" >/dev/null; then
        chrome=$candidate
        break
      fi
    done
  fi
  if [ -n "$chrome" ]; then
    # Call the Chrome binary directly. The google-chrome wrapper on this image
    # injects the desktop profile and can hang a headless screenshot.
    timeout 30 "$chrome" --headless=new --disable-gpu --no-sandbox \
      --user-data-dir="$ROOT/chrome-profile" \
      --window-size=1280,900 \
      --screenshot="$dir/page.png" \
      "$URL/search?q=$(python3 -c 'import urllib.parse,sys; print(urllib.parse.quote(sys.argv[1]))' "$query")" \
      >"$dir/chrome.log" 2>&1 \
      || die "Chrome screenshot failed; see $dir/chrome.log"
    [ -s "$dir/page.png" ] || die "Chrome did not write $dir/page.png"
  fi
  {
    echo "feature=catalog-search"
    echo "entry=GET /search?q=$query"
    echo "url=$URL/search?q=$query"
    echo "http=$code"
    echo "matched=Apple MacBook Pro"
    echo "empty=zzznomatchverify"
  } >"$dir/proof.txt"
  echo "search ok evidence=$dir"
}

cmd_cleanup() {
  load_state
  local pid
  if [ -f "$ROOT/pid" ]; then
    pid=$(cat "$ROOT/pid" || true)
    if [ -n "${pid:-}" ]; then
      kill -- -"$pid" 2>/dev/null || kill "$pid" 2>/dev/null || true
      sleep 1
      kill -9 -- -"$pid" 2>/dev/null || kill -9 "$pid" 2>/dev/null || true
    fi
  fi
  if [ -n "${DB_NAME:-}" ] && [ "$DB_NAME" != "nopcommerce" ]; then
    sudo -u postgres psql -d postgres -v ON_ERROR_STOP=1 \
      -c "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();" \
      -c "DROP DATABASE IF EXISTS \"${DB_NAME}\";" >/dev/null
  fi
  if [ -L "$CURRENT_LINK" ] && [ "$(readlink -f "$CURRENT_LINK")" = "$ROOT" ]; then
    rm -f "$CURRENT_LINK"
  fi
  rm -rf "$ROOT"
  echo "cleaned run=$RUN_ID evidence_kept=$EVIDENCE"
}

usage() {
  echo "usage: verify-nop.sh launch|doctor|search [query]|cleanup" >&2
  exit 2
}

main() {
  case "${1:-}" in
    launch) cmd_launch ;;
    doctor) cmd_doctor ;;
    search) shift; cmd_search "${1:-MacBook}" ;;
    cleanup) cmd_cleanup ;;
    *) usage ;;
  esac
}

main "$@"
