# Taskline — Task Management System

Taskline is a .NET full-stack project for the Cohort 10 assignment by Fasiha Tariq. It uses ASP.NET Core Web API, ASP.NET Core Identity, EF Core with SQL Server, and a React frontend.

- Members see tasks they created or were assigned. They can manage their own tasks and change the status of tasks assigned to them.
- Administrators see and manage all tasks, assign work across accounts, and see dashboard totals for the entire workspace.
- Tasks support pending, in-progress, and completed statuses, three priorities, categories, due dates, and search. Search covers the title, description, and category.
- The API produces structured Serilog request and activity logs. Error responses use Problem Details. Task updates and deletion require the current version to protect concurrent edits.

## Run locally

Install the **.NET 10 SDK**, **Node.js 24**, and **SQL Server 2022 or later**. On Windows you can use SQL Server Express LocalDB. The development connection string already targets `(localdb)\MSSQLLocalDB` and creates a database named `FasihaTaskManagement`. [Microsoft LocalDB setup](https://learn.microsoft.com/en-us/sql/database-engine/configure-windows/sql-server-express-localdb)

From the repository root in PowerShell, apply the migrations and start the API.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet tool restore
dotnet ef database update --project src/TaskManagement.Api
dotnet run --project src/TaskManagement.Api
```

To use a different SQL Server, set `ConnectionStrings__Tasks` in the shell before applying migrations and starting the API, or save the connection string under `ConnectionStrings:Tasks` with ASP.NET Core User Secrets. The application reads the same key in every environment.

Open a second terminal from the repository root.

```powershell
cd client
npm.cmd ci
npm.cmd run dev
```

Open **http://localhost:5173** and create an account. Vite proxies `/api` to `http://localhost:5147`. If the API uses another address, set `TASK_API_URL` in the Vite terminal before starting the client. On macOS or Linux, use `npm` in place of `npm.cmd`.

For an HTTPS deployment, run `npm ci` and `npm run build` inside `client`. Serve `client/dist` with an `index.html` fallback for client routes and route `/api/*` to the API on the same origin. Configure the API to see the request as HTTPS for secure cookies. Provide the production database connection and apply migrations as part of deployment.

### Provision an administrator

Public registration creates member accounts. Run this command after applying the database migrations, with a distinct email for the administrator. The command creates the account once and exits.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:AdminBootstrap__Email = 'admin@example.com'
$env:AdminBootstrap__DisplayName = 'Taskline Admin'
$password = Read-Host 'Administrator password' -AsSecureString
$env:AdminBootstrap__Password = [Net.NetworkCredential]::new('', $password).Password
try {
    dotnet run --project src/TaskManagement.Api -- --seed-admin
} finally {
    [Environment]::SetEnvironmentVariable('AdminBootstrap__Password', $null, 'Process')
}
```

Use at least 10 characters with a lowercase letter and a digit. Sign in through the same screen as a member after provisioning.

## API

A browser or API client must retain cookies. Before each POST, PUT, PATCH, or DELETE, call `GET /api/account/csrf` and send the returned `token` in the `X-CSRF-TOKEN` header. Fetch a new token after signing in or signing out.

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/health` | API process health |
| GET | `/api/account/csrf` | Get an antiforgery token |
| POST | `/api/account/register` | Register with `displayName`, `email`, and `password` |
| POST | `/api/account/login` | Start a session with `email` and `password` |
| GET | `/api/account/me` | Get the signed-in user's profile and roles |
| POST | `/api/account/logout` | End the session |
| GET | `/api/account/assignees?search=...` | Look up accounts for assignment as an administrator |
| GET | `/api/tasks` | List visible tasks with optional `search`, `status`, `priority`, `category`, `page`, and `pageSize` |
| GET | `/api/tasks/counts` | Count visible tasks by status |
| GET | `/api/tasks/{id}` | Read task details and the current `version` |
| POST | `/api/tasks` | Create a task |
| PUT | `/api/tasks/{id}` | Replace editable fields using the current `version` |
| PATCH | `/api/tasks/{id}/status` | Change `status` using the current `version` |
| DELETE | `/api/tasks/{id}?version=...` | Delete a task using the current `version` |

Task fields are `title`, `description`, `category`, `status`, `priority`, `dueDate`, and `assigneeId`. Use `yyyy-MM-dd` for due dates, `Pending`, `InProgress`, or `Completed` for status, and `Low`, `Medium`, or `High` for priority. A stale version returns HTTP 409.

## Tests and code quality

Run the xUnit tests from the repository root.

```powershell
dotnet test TaskManagement.slnx
```

The tests use isolated SQLite databases by default. To exercise the SQL Server migrations and the same API tests against SQL Server, set `TASKMANAGEMENT_TEST_SQL` to a SQL Server connection string with permission to create and drop disposable test databases. The suite generates a separate database name for each API test fixture. GitHub Actions builds React and runs the API tests with both providers.

The SonarQube workflow analyzes C# and JavaScript/JSX, imports OpenCover coverage, and waits for the quality gate. By default it uses an isolated SonarQube Community Build instance on the runner. To keep analysis history on your own SonarQube instance, create a project there and set these repository variables and secret.

| GitHub setting | Type | Value |
|---|---|---|
| `SONAR_HOST_URL` | Variable | URL reachable from the runner |
| `SONAR_PROJECT_KEY` | Variable | SonarQube project key |
| `SONAR_TOKEN` | Secret | Token with Execute Analysis permission |

For an existing SonarQube project, set `SONAR_HOST_URL`, `SONAR_PROJECT_KEY`, and `SONAR_PROFILE_TOKEN` in your shell. Run `node scripts/configure-sonar.mjs` once with a token allowed to administer quality profiles and the project. It extends Sonar way for C# and JavaScript with a cognitive complexity threshold of 18. Run `./scripts/scan-sonar.ps1` locally with `SONAR_HOST_URL`, `SONAR_PROJECT_KEY`, and `SONAR_TOKEN` to analyze the project. [SonarScanner for .NET setup](https://docs.sonarsource.com/sonarqube-community-build/analyzing-source-code/scanners/dotnet/installing)
