#!/bin/bash
# Runs on the EC2 server. The API pipeline installs it as /opt/ac/deploy.sh on every run.
#
#   deploy.sh api <image-uri>      swap the API container to this image
#   deploy.sh web <image-uri>      swap the frontend container to this image
#   deploy.sh migrate <file.sql>…  run idempotent SQL scripts against the db container
#
# The images each service runs are kept in .deploy-images and written to
# docker-compose.images.yml, an override next to the docker-compose.yml that
# setup.sh created. Nothing in docker-compose.yml or .env is touched.
# If the new container does not answer within ~2 minutes, the previous image
# is put back and the script exits non-zero, which fails the pipeline.
set -euo pipefail

ROOT="${AC_ROOT:-/opt/ac}"
STATE=.deploy-images
OVERRIDE=docker-compose.images.yml
cd "$ROOT"

compose() {
  if [ -f "$OVERRIDE" ]; then
    docker compose -f docker-compose.yml -f "$OVERRIDE" "$@"
  else
    docker compose -f docker-compose.yml "$@"
  fi
}

write_override() {
  local svc img # local: plain names here would overwrite the caller's $service / $image
  {
    echo "# Written by deploy.sh: the image each service runs. Do not edit by hand."
    if [ -s "$STATE" ]; then
      echo "services:"
      while IFS='=' read -r svc img; do
        if [ -n "$svc" ]; then printf '  %s:\n    image: %s\n' "$svc" "$img"; fi
      done < "$STATE"
    else
      echo "services: {}"
    fi
  } > "$OVERRIDE"
}

set_image() { # set_image <service> [image]   (no image = back to the one in docker-compose.yml)
  touch "$STATE"
  {
    grep -v "^$1=" "$STATE" || true
    if [ -n "${2:-}" ]; then echo "$1=$2"; fi
  } > "$STATE.tmp"
  mv "$STATE.tmp" "$STATE"
  write_override
}

sa_password() { grep '^SA_PASSWORD=' .env | cut -d= -f2-; }

migrate() {
  local file
  for file in "$@"; do
    echo "Applying $(basename "$file")"
    compose cp "$file" db:/tmp/migration.sql
    compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$(sa_password)" -i /tmp/migration.sql
  done
}

healthy() { # ask through the caddy container, which can reach api:8080 and web:3000
  local url="$1"
  for _ in $(seq 1 30); do
    if compose exec -T caddy wget -q -O /dev/null "$url" >/dev/null 2>&1; then
      return 0
    fi
    sleep 4
  done
  return 1
}

deploy() {
  local service="$1" image="$2" url previous
  case "$service" in
    api) url="http://api:8080/swagger/index.html" ;;
    web) url="http://web:3000/" ;;
    *) echo "Unknown service '$service' (use api or web)" >&2; exit 2 ;;
  esac

  local registry="${image%%/*}"
  local region
  region="$(echo "$registry" | cut -d. -f4)"
  aws ecr get-login-password --region "$region" | docker login --username AWS --password-stdin "$registry" >/dev/null

  previous="$(grep "^$service=" "$STATE" 2>/dev/null | cut -d= -f2- || true)"
  docker pull -q "$image"
  set_image "$service" "$image"
  compose up -d --no-deps "$service"

  if healthy "$url"; then
    echo "Deployed $service: $image"
    docker image prune -af --filter "until=168h" >/dev/null 2>&1 || true
    return 0
  fi

  echo "The new $service did not respond at $url, rolling back." >&2
  compose logs --tail 80 "$service" >&2 || true
  set_image "$service" "$previous"
  compose up -d --no-deps "$service"
  echo "Rolled $service back to ${previous:-the image in docker-compose.yml}." >&2
  return 1
}

case "${1:-}" in
  api | web) deploy "$1" "${2:?image uri required}" ;;
  migrate) shift; migrate "$@" ;;
  *) echo "usage: deploy.sh api|web <image-uri> | deploy.sh migrate <file.sql>..." >&2; exit 2 ;;
esac
