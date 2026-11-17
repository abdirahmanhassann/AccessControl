# AccessControl – AWS pipelines

Pushing to `master` (this repo) or `main` (AccessControlFE) builds the app on GitHub and deploys it to the
EC2 server that `deploy/setup.sh` set up (`/opt/ac`, Docker Compose: `db`, `api`, `web`, `caddy`).

```
GitHub Actions ──build──▶ ECR (ac-api / ac-web) ──▶ SSM Run Command on the EC2 server
                                                     └▶ /opt/ac/deploy.sh: pull, swap one container,
                                                        check it answers, roll back if it doesn't
```

- **No keys in GitHub.** The workflows sign in to AWS with GitHub's OIDC token and assume a role that can
  only push to the two ECR repositories and send commands to this one instance.
- **No SSH.** Commands reach the server through SSM, so port 22 can stay closed.
- **The API deploy runs the database scripts first**, in the order listed in `deploy/aws/migrations.txt`
  (currently `Keyman.sql`). Every script there must be safe to run again.
- **The frontend deploy runs the end-to-end tests first**, so a failing test stops the deploy.
- **Rollback.** If the new container doesn't answer within about 2 minutes, the server goes back to the
  previous image and the run fails.
- **Nothing else on the server changes.** `.env`, `api.env`, `docker-compose.yml`, the database volume and
  uploaded photos are left alone. The image each service runs is recorded in `/opt/ac/.deploy-images` and
  `/opt/ac/docker-compose.images.yml`.

## One-time setup

1. **Make sure SSM can reach the server.** In the EC2 console, open the instance and check
   *Security → IAM role*. If it has no role, use *Actions → Security → Modify IAM role* to attach one. If it
   has a role, make sure that role has the AWS-managed policy **AmazonSSMManagedInstanceCore**. After a few
   minutes the instance should appear under *Systems Manager → Fleet Manager*.
2. **Create the AWS side of the pipeline** with `pipeline-setup.yaml`: in *CloudFormation → Create stack*,
   upload the file. Enter the instance ID and the role name from step 1. Set *CreateGitHubOidcProvider* to
   `false` only if *IAM → Identity providers* already lists `token.actions.githubusercontent.com`.
   The stack creates the `ac-api` and `ac-web` ECR repositories, the GitHub deploy role, and gives the
   server's role permission to pull from ECR.
3. **Set repository variables** in both GitHub repos under *Settings → Secrets and variables → Actions →
   Variables*:

   | Variable | Value | Repos |
   |---|---|---|
   | `AWS_DEPLOY_ROLE_ARN` | the stack's `DeployRoleArn` output | both |
   | `AWS_INSTANCE_ID` | the stack's `InstanceId` output | both |
   | `AWS_REGION` | only if the server is not in `eu-west-2` | both |
   | `VITE_API_BASE_URL` | `https://<API_HOST from /opt/ac/.env>` | AccessControlFE |

4. **Add the workflow files**, then commit and push each repo:
   - `deploy/aws/workflows/AccessControl-deploy-aws.yml` → `.github/workflows/deploy-aws.yml` in this repo
   - `deploy/aws/workflows/AccessControlFE-deploy-aws.yml` → `.github/workflows/deploy-aws.yml` in
     AccessControlFE (its `Dockerfile` and `.dockerignore` are already there)
5. **Run the API pipeline first** (*Actions → Deploy API to AWS → Run workflow*). It installs
   `/opt/ac/deploy.sh` on the server, which the frontend pipeline uses.

## Day to day

- **Logs** for a deploy are in the workflow run under *Server output*. On the server, use
  `cd /opt/ac && docker compose -f docker-compose.yml -f docker-compose.images.yml logs api`.
- **Roll back by hand:** revert the commit and push, or on the server run
  `/opt/ac/deploy.sh api <account>.dkr.ecr.eu-west-2.amazonaws.com/ac-api:<older commit sha>`
  (every image is tagged with the commit it was built from).
- **New database change:** add an idempotent `.sql` file and list it in `deploy/aws/migrations.txt`.
- ECR keeps the last 15 images of each repository. The server deletes local images older than a week.
