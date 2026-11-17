# AccessControl – AWS deployment

One EC2 server (Stockholm, eu-north-1) running four Docker containers:

- `db`    – SQL Server 2025 Express, data in a Docker volume
- `api`   – the ASP.NET Core API (this repo)
- `web`   – the AccessControlFE frontend (built for Node instead of Vercel)
- `caddy` – HTTPS (free Let's Encrypt certificates) in front of both

Files:
- `AccessControl.cloud.sql` – schema + stored procedures, with the local-PC parts removed
  (file paths, the `abdi` login). Loaded once on first deploy.
- `api.Dockerfile`, `web.Dockerfile`, `docker-compose.yml`, `Caddyfile`
- `setup.sh` – run on the server: installs Docker, unpacks the code, builds and starts everything.

Twilio settings are read on the server from `Properties/launchSettings.json` ("http" profile).

## Updates after the first setup

`setup.sh` is only for building the server the first time. After that, every push to `master` (API) or
`main` (frontend) deploys automatically. See [`autodeploy/README.md`](autodeploy/README.md).
