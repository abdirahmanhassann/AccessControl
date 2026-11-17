# AccessControl – deploy pipeline

```
push to master (API) / main (frontend)
   └▶ GitHub Actions "Build and release": builds the Docker image
        └▶ if it builds, moves the `deploy` branch to that commit
             └▶ the EC2 server checks GitHub every 2 minutes (ac-autodeploy.timer)
                  └▶ builds that commit, runs deploy/migrations.txt (API only),
                     swaps the container, checks it answers, rolls back if not
```

- **No AWS keys in GitHub.** The server pulls from GitHub with two read-only deploy keys
  (one per repo, *Settings → Deploy keys*). GitHub Actions only needs its built-in token.
  (The account's AWS policy blocks GitHub's OIDC sign-in, so the push-based design in `deploy/aws` can't be used.)
- **Nothing else on the server changes:** `.env`, `api.env`, `docker-compose.yml`, the database and photos are left alone.
- A commit that fails to build, fails a database script or doesn't answer is **not retried**; push a fix.
- **Database changes:** add an idempotent `.sql` file and list it in `deploy/migrations.txt`.

## On the server

| What | Where |
|---|---|
| Deploy log | `/var/log/ac-autodeploy.log` (also `journalctl -u ac-autodeploy`) |
| What's live | `/opt/ac/.autodeploy/api.deployed`, `web.deployed` (commit SHAs) |
| Code checkouts | `/opt/ac/src/AccessControl`, `/opt/ac/src/AccessControlFE` |
| Pause deploys | `systemctl stop ac-autodeploy.timer` (`start` to resume) |
| Manual roll back | `docker tag ac-api:previous ac-api && cd /opt/ac && docker compose up -d --no-deps api` |
