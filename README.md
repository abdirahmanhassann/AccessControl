# AccessControl – Site Access Management API

AccessControl manages who can enter which rooms on a site. A worker scans the QR code on a room's door to request access. The manager assigned to that location approves or rejects the request, and both sides get SMS updates along the way. Every action is recorded in an audit trail.

This repository contains the **ASP.NET Core Web API**. The frontend lives in a separate repository.

## Features

- **QR code access requests.** Each room has a QR code. Scanning it starts a request with the reason, type of work and expected time window.
- **Manager approvals.** Requests go to a manager assigned to that location, and only assigned managers can approve or reject them.
- **Site hierarchy and mappings.** Sites contain work areas, which contain rooms. Departments and managers are mapped to rooms, and workers are mapped to sites.
- **Blackout access windows.** No requests can be made for a work area while one is active.
- **SMS notifications and OTP verification** through Twilio.
- **Access photos.** Images can be uploaded against requests.
- **Keyman room checks.** When a worker signs out of a room, the Keyman is pinged to check the room. They approve the room leave, or reject it with a reason. Keymen otherwise have the same access as managers.
- **Audit trail.** Every request, approval and rejection is logged.
- **Accounts.** Token-based login, password reset by SMS code, and admin endpoints for managing sites, rooms, workers and managers.

## Tech stack

- C# / .NET 10, ASP.NET Core Web API
- SQL Server (T-SQL stored procedures)
- Twilio (SMS and Verify)
- Swagger / OpenAPI

## Project structure

```
AccessControl/
├── Controllers/   HTTP endpoints
├── Processor/     Business logic (requests, approvals, mappings, notifications)
├── Database/      Data access (DatabaseLogic) and SQL scripts
├── Helpers/       Twilio and API helpers
├── Security/      Token checks and password encryption
└── Model/         Request and response models
deploy/            Docker files and scripts from a personal test deployment
```

## Running locally

**Prerequisites:** .NET 10 SDK and SQL Server (Express is fine).

1. **Create the database.** Run `Database/DBSCRIPT.txt`, then `Mappings.sql`, `SiteQr.sql`, `Keyman.sql` and `UsersInsert.sql`. `Keyman.sql` is safe to run again on an existing database.
2. **Set the connection string.** Either edit `ConnectionStrings:AccessControlDB` in `appsettings.json`, or override it in an `appsettings.Local.json` file next to it. That file is git-ignored.
3. **Set the secrets with .NET user secrets.** They are never stored in the repository.

   ```bash
   cd AccessControl
   dotnet user-secrets set "Security:EncryptionKey" "<a long random string>"
   dotnet user-secrets set "TWILIO_ACCOUNT_SID" "<your Twilio account SID>"
   dotnet user-secrets set "TWILIO_AUTH_TOKEN" "<your Twilio auth token>"
   dotnet user-secrets set "TWILIO_FROM_NUMBER" "<your Twilio number>"
   dotnet user-secrets set "TWILIO_VERIFY_SERVICE_SID" "<your Verify service SID>"
   ```

   To run without sending texts, set `"Twilio": false` in `appsettings.json`.
4. **Run it.**

   ```bash
   dotnet run
   ```

   Swagger UI is then available at `/swagger`.
