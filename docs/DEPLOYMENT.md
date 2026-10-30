# Deploying the API to a Windows Server (IIS)

The workflow in `.github/workflows/deploy.yml` tests every push and pull request. A push to `main` (or a manual run on
`main`) then publishes the API self-contained for `win-x64` and sends it to IIS with Web Deploy.

```
pull request  ->  build + test (Linux)
push to main  ->  build + test  ->  publish  ->  write appsettings.Production.json  ->  Web Deploy  ->  health check
```

Migrations run when the app starts, so the first request after a deploy applies any new schema changes.

## One-time setup on the server

1. **IIS** with the *Management Service* role feature (`Web-Mgmt-Service`).
2. **ASP.NET Core Hosting Bundle for .NET 10.** Needed even though the app is self-contained, because it installs the
   ASP.NET Core Module that IIS uses to start the app. Restart IIS after installing (`iisreset`).
3. **Web Deploy 3.6 or later** (the "hosting server" install, which includes the Web Deploy handler).
4. **Management Service**: open *IIS Manager > server > Management Service*, enable remote connections, choose
   *Windows credentials or IIS Manager credentials*, start the service, and set it to start automatically.
5. **Firewall**: allow inbound TCP **8172** (Web Deploy) from GitHub's runners, and 443 for the API itself. GitHub
   runner addresses change, so most teams leave 8172 open to the internet behind a strong password, or use a
   self-hosted runner on the server's network instead.
6. **Site**: create an IIS site named `LearnBridge` (or set the `IIS_SITE` variable to your name). Set its application
   pool to **No Managed Code**. Add an HTTPS binding with a certificate for the API's host name.
7. **Deploy user**: create a Windows user for deployments, then in *IIS Manager > the site > IIS Manager Permissions*
   allow it, and give it **Modify** rights on the site's folder. (Web Deploy needs the rights to write files and to
   take the site offline with `app_offline.htm`.)
8. **Application pool identity** (`IIS AppPool\<pool name>`): **Read & execute** on the site folder and **Modify** on a
   `logs` subfolder if you turn on stdout logging in `web.config`.
9. **SQL Server**: create the database and a SQL login for the app. The app creates and updates tables itself at
   startup, so the login needs permission to change the schema (the `db_owner` role on this database is simplest).
   Make sure the server can reach SQL Server on its port.

## GitHub setup

*Settings > Environments > New environment* named **production**. Add required reviewers if you want a manual
approval before each deploy.

*Settings > Secrets and variables > Actions*:

| Name | Kind | Value |
| --- | --- | --- |
| `PROD_CONNECTION_STRING` | secret | SQL Server connection string for the database above |
| `PROD_JWT_SIGNING_KEY` | secret | A long random string, at least 32 characters. Never reuse the development key |
| `PROD_CLAUDE_API_KEY` | secret | The Claude API key. It lives only on the server |
| `DEPLOY_USER` | secret | The deploy user from step 7 (with the domain or machine name if needed) |
| `DEPLOY_PASSWORD` | secret | Its password |
| `DEPLOY_SERVER` | variable | `host-or-ip:8172`. Defaults to the address already in the old workflow |
| `IIS_SITE` | variable | IIS site name. Defaults to `LearnBridge` |
| `HEALTH_URL` | variable | e.g. `https://api.example.com/api/health`. Turns on the post-deploy health check |

The workflow stops with a clear message if a required secret is empty.

## CORS

`LearnBridge.Api/appsettings.json` lists the browser origins allowed to call the API (`Cors:AllowedOrigins`). Make sure
the address the Angular app is served from is in that list before the first deploy.

## First deployment

1. Do the server and GitHub setup above.
2. Run the workflow by hand: *Actions > Test and deploy to IIS > Run workflow* on `main`.
3. Watch the **Deploy to IIS** step. A permissions or connection error here means step 4, 5 or 7 above.
4. Open `https://<your host>/api/health`. The first request starts the app and applies migrations, so allow up to a
   minute. You should see `{"status":"healthy", ...}`.
5. If you get HTTP 500.30 or 502.5, the app failed to start. Look at the Windows *Event Viewer > Application* log, or
   set `stdoutLogEnabled="true"` in `web.config` temporarily with a `logs` folder the pool can write to. The usual
   causes are a wrong connection string, a SQL login without schema rights, or the Hosting Bundle missing.

## Known limits

- `-allowUntrusted` is set because Web Deploy's Management Service usually has a self-signed certificate. Install a
  trusted certificate on it and remove the flag to stop trusting any certificate.
- Web Deploy replaces the site's files each time, apart from the `logs` folder. Do not keep anything else on the
  server inside the site folder.
- A bad migration will fail the app's start. Keep a database backup before releases that change the schema.
