#!/bin/bash
# AccessControl auto-deploy. Runs on the EC2 server every 2 minutes (systemd: ac-autodeploy.timer).
#
# GitHub Actions moves the `deploy` branch of each repo once a push to master/main has built
# cleanly. This script notices the move, builds that commit into an image on the server, swaps
# the one container (api or web), checks it answers, and puts the previous image back if not.
#
# State lives in /opt/ac/.autodeploy: <service>.deployed / <service>.failed hold commit SHAs.
# A commit that failed is not retried; push a new commit to try again.
# Logs: journalctl -u ac-autodeploy   (or: tail /var/log/ac-autodeploy.log)
set -uo pipefail

ROOT=/opt/ac
SRC=$ROOT/src
STATE=$ROOT/.autodeploy
BRANCH=deploy

exec 9>/run/ac-autodeploy.lock
flock -n 9 || exit 0 # a deploy is already running

cd "$ROOT" || exit 1
mkdir -p "$STATE"
set -a; . ./.env; set +a

log() { echo "$(date '+%F %T') $*" | tee -a /var/log/ac-autodeploy.log; }
compose() { docker compose -f docker-compose.yml "$@"; }
sa_password() { grep '^SA_PASSWORD=' .env | cut -d= -f2-; }

healthy() { # ask through the caddy container, which can reach api:8080 and web:3000
  local url="$1"
  for _ in $(seq 1 30); do
    compose exec -T caddy wget -q -O /dev/null "$url" >/dev/null 2>&1 && return 0
    sleep 4
  done
  return 1
}

migrate() { # run the idempotent SQL scripts listed in deploy/migrations.txt
  local list="$SRC/AccessControl/deploy/migrations.txt" f
  [ -f "$list" ] || return 0
  # The list is read on fd 3: docker compose reads stdin, and on fd 0 it would swallow
  # the rest of the list so only the first script ran.
  while IFS= read -r f <&3; do
    f="${f%$'\r'}"
    case "$f" in '' | '#'*) continue ;; esac
    log "api: applying $f"
    compose cp "$SRC/AccessControl/$f" db:/tmp/migration.sql </dev/null || return 1
    compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -I -b -S localhost -U sa -P "$(sa_password)" \
      -i /tmp/migration.sql </dev/null || return 1
  done 3< "$list"
}

build() { # build <service> <sha>
  local svc="$1" sha="$2"
  case "$svc" in
    api)
      docker build -q -t "ac-api:$sha" -f "$SRC/AccessControl/deploy/api.Dockerfile" "$SRC/AccessControl" ;;
    web)
      local df="$SRC/AccessControlFE/Dockerfile"
      [ -f "$df" ] || df="$ROOT/web.Dockerfile"
      docker build -q -t "ac-web:$sha" -f "$df" \
        --build-arg VITE_API_BASE_URL="https://$API_HOST" "$SRC/AccessControlFE" ;;
  esac
}

self_update() { # after an API deploy, take this script from the commit just deployed
  local repo_copy="$SRC/AccessControl/deploy/autodeploy/autodeploy.sh"
  [ -f "$repo_copy" ] || return 0
  tr -d '\r' < "$repo_copy" > "$ROOT/autodeploy.sh.new" && bash -n "$ROOT/autodeploy.sh.new" || { rm -f "$ROOT/autodeploy.sh.new"; return 0; }
  if cmp -s "$ROOT/autodeploy.sh.new" "$ROOT/autodeploy.sh"; then rm -f "$ROOT/autodeploy.sh.new"
  else mv "$ROOT/autodeploy.sh.new" "$ROOT/autodeploy.sh"; log "autodeploy.sh updated from ${1:0:7}"; fi
}

deploy() { # deploy <service> <repo dir>
  local svc="$1" dir="$2" url sha current
  case "$svc" in
    api) url="http://api:8080/swagger/index.html" ;;
    web) url="http://web:3000/" ;;
  esac

  git -C "$dir" fetch -q origin "+refs/heads/$BRANCH:refs/remotes/origin/$BRANCH" 2>/dev/null || return 0 # no deploy branch yet
  sha="$(git -C "$dir" rev-parse "origin/$BRANCH")" || return 0
  current="$(cat "$STATE/$svc.deployed" 2>/dev/null || true)"
  [ "$sha" = "$current" ] && return 0
  [ "$sha" = "$(cat "$STATE/$svc.failed" 2>/dev/null || true)" ] && return 0

  log "$svc: deploying ${sha:0:7} (was ${current:0:7})"
  git -C "$dir" checkout -q -f --detach "$sha" && git -C "$dir" clean -fdq

  if ! build "$svc" "$sha" > /tmp/ac-build-$svc.log 2>&1; then
    log "$svc: build FAILED for ${sha:0:7}, nothing changed. Last lines:"
    tail -n 25 /tmp/ac-build-$svc.log | tee -a /var/log/ac-autodeploy.log
    echo "$sha" > "$STATE/$svc.failed"
    return 1
  fi

  if [ "$svc" = api ] && ! migrate; then
    log "api: database script FAILED for ${sha:0:7}, the running API was not changed."
    echo "$sha" > "$STATE/$svc.failed"
    return 1
  fi

  # Keep the running image as ac-<svc>:previous, then point ac-<svc> (what docker-compose.yml runs) at the new one.
  docker image inspect "ac-$svc" >/dev/null 2>&1 && docker tag "ac-$svc" "ac-$svc:previous"
  docker tag "ac-$svc:$sha" "ac-$svc"
  compose up -d --no-deps "$svc"

  if healthy "$url"; then
    echo "$sha" > "$STATE/$svc.deployed"
    rm -f "$STATE/$svc.failed"
    log "$svc: live on ${sha:0:7}"
    [ "$svc" = api ] && self_update "$sha"
    return 0
  fi

  log "$svc: ${sha:0:7} did not answer at $url, rolling back."
  compose logs --tail 40 "$svc" 2>&1 | tee -a /var/log/ac-autodeploy.log
  docker tag "ac-$svc:previous" "ac-$svc"
  compose up -d --no-deps "$svc"
  echo "$sha" > "$STATE/$svc.failed"
  log "$svc: rolled back to ${current:0:7}"
  return 1
}

deploy api "$SRC/AccessControl"
deploy web "$SRC/AccessControlFE"

# Housekeeping: drop images and build cache older than a week that nothing uses.
docker image prune -af --filter "until=168h" >/dev/null 2>&1 || true
docker builder prune -f --filter "until=168h" >/dev/null 2>&1 || true
exit 0
