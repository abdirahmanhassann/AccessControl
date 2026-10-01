#!/bin/bash
# Runs on the server as root. Usage: setup.sh <bucket>
set -euxo pipefail
BUCKET="$1"; ROOT=/opt/ac
mkdir -p $ROOT && cd $ROOT
if ! command -v docker >/dev/null; then
  dnf install -y docker python3 tar gzip
  systemctl enable --now docker
  mkdir -p /usr/local/lib/docker/cli-plugins
  curl -fsSL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-x86_64 -o /usr/local/lib/docker/cli-plugins/docker-compose
  chmod +x /usr/local/lib/docker/cli-plugins/docker-compose
fi
if [ ! -f /swapfile ]; then fallocate -l 2G /swapfile && chmod 600 /swapfile && mkswap /swapfile && swapon /swapfile && echo '/swapfile none swap sw 0 0' >> /etc/fstab; fi
aws s3 cp s3://$BUCKET/src.tgz /tmp/src.tgz
rm -rf AccessControl AccessControlFE && tar -xzf /tmp/src.tgz -C $ROOT
cp -r $ROOT/AccessControl/deploy/. $ROOT/
cp api.Dockerfile AccessControl/Dockerfile
cp web.Dockerfile AccessControlFE/Dockerfile
# Twilio settings from launchSettings.json ("http" profile)
python3 - <<'PY'
import json,re
t=open('/opt/ac/AccessControl/AccessControl/Properties/launchSettings.json',encoding='utf-8-sig').read()
t=re.sub(r'^\s*//.*$','',t,flags=re.M)
env=json.loads(t)['profiles']['http']['environmentVariables']
with open('/opt/ac/api.env','w') as f:
    for k,v in env.items():
        if k.startswith('TWILIO_'): f.write(f'{k}={v}\n')
PY
chmod 600 api.env .env
set -a; . ./.env; set +a
docker build -t ac-api AccessControl
docker build --build-arg VITE_API_BASE_URL="https://$API_HOST" -t ac-web AccessControlFE
docker compose up -d --remove-orphans
# Load schema once
if [ ! -f $ROOT/.db_loaded ]; then
  for i in $(seq 1 60); do docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P "$(grep SA_PASSWORD .env | cut -d= -f2-)" -Q "SELECT 1" && break; sleep 5; done
  docker compose cp AccessControl.cloud.sql db:/tmp/schema.sql
  docker compose exec -T db /opt/mssql-tools18/bin/sqlcmd -C -b -S localhost -U sa -P "$(grep SA_PASSWORD .env | cut -d= -f2-)" -i /tmp/schema.sql
  touch $ROOT/.db_loaded
  docker compose restart api
fi
docker compose ps
