# AccessControl – Azure Windows VM deployment

Pushing to `master` (this repo) or `main` (AccessControlFE) builds the app on GitHub and deploys it to the VM.
If the new build doesn't respond, the previous one is put back automatically.

```
GitHub (build on ubuntu) ──artifact──▶ runner on the VM ──▶ C:\apps\api  → service ACApi  (127.0.0.1:5000)
                                                        └─▶ C:\apps\web  → service ACWeb  (127.0.0.1:3000)
                                       Caddy (service ACCaddy) serves both over HTTPS on 443 / 8443
```

The deploy job runs on a GitHub runner installed **on the VM**, so you need no inbound deploy port,
no SSH/WinRM and no Azure credentials in GitHub.

## One-time setup

1. **Create the VM.** In the Azure portal choose Windows Server 2025 Datacenter, size B2s (or B2ms if
   SQL Server feels slow) and region UK South. Under *Networking → Public IP → DNS name label* pick a name,
   e.g. `accesscontrol-demo`. That gives you `accesscontrol-demo.uksouth.cloudapp.azure.com`.
2. **Open the ports** in the VM's network security group: 80, 443 and 8443 from anywhere.
   Allow RDP (3389) only from your own IP.
3. **Install SQL Server Express** on the VM (Microsoft's installer, Basic install). Use SSMS to run
   `deploy/AccessControl.cloud.sql`. Then create a SQL login (e.g. `accesscontrol`) with access to the
   `AccessControl` database, and turn on *SQL Server and Windows Authentication mode*.
4. **Run the setup script.** Copy `setup-vm.ps1` to the VM and run it in an admin PowerShell window:
   ```powershell
   powershell -ExecutionPolicy Bypass -File setup-vm.ps1 -AppHost accesscontrol-demo.uksouth.cloudapp.azure.com
   ```
   Then fill in `C:\apps\api\appsettings.Local.json` with the connection string, encryption key and Twilio values.
5. **Register a GitHub runner for each repo.** Repeat this once for AccessControl and once for AccessControlFE:
   on GitHub go to *Settings → Actions → Runners → New self-hosted runner → Windows*. Run its download commands
   in its own folder (`C:\actions-runner\api` and `C:\actions-runner\web`), then configure it like this:
   ```powershell
   .\config.cmd --url https://github.com/abdirahmanhassann/<REPO> --token <TOKEN FROM THAT PAGE> `
     --labels accesscontrol-vm --runasservice --windowslogonaccount "NT AUTHORITY\SYSTEM" --unattended
   ```
   The runner runs as SYSTEM so it can stop and start the app services.
6. **In AccessControlFE**, add the repository variable `VITE_API_BASE_URL` under
   *Settings → Secrets and variables → Actions → Variables*. Set it to
   `https://accesscontrol-demo.uksouth.cloudapp.azure.com:8443`.
7. **Run each workflow once**: *Actions → Deploy … → Run workflow*. After that, every push deploys.

## Good to know

- **What the pipelines don't touch:** `appsettings.Local.json` (secrets) and `C:\apps\api\wwwroot` (uploaded
  photos) are never overwritten. Database changes (new stored procedures and so on) still need to be run by hand.
- **Logs** are in `C:\apps\logs`. You can also restart a service with `Restart-Service ACApi`
  (or `ACWeb`, `ACCaddy`).
- **Public repos:** if either repo is public, go to *Settings → Actions → General* and require approval
  for workflows from outside contributors, so nobody else's pull request can run code on your VM.
- **Using your own domain:** run the setup script again with `-AppHost app.yourdomain.com -ApiHost api.yourdomain.com`,
  then update `VITE_API_BASE_URL` and `PublicApiBaseUrl` to match.
