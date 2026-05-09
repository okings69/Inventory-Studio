# Inventory Studio

Inventory Studio is an ASP.NET Core MVC course project for managing customizable inventories. Users can create inventories, define custom fields, add items, share access, search data, discuss around an inventory, like items, and export CSV files.

## Stack

- ASP.NET Core MVC / C#
- Entity Framework Core
- PostgreSQL
- ASP.NET Core Identity with Admin/User roles
- Bootstrap 5
- SignalR discussion per inventory
- Markdig Markdown rendering
- Cloudinary-ready image upload service
- PostgreSQL full-text search queries
- xUnit test project for service and security checks

## Documentation

- [Architecture](docs/ARCHITECTURE.md)
- [Database](docs/DATABASE.md)
- [Security](docs/SECURITY.md)
- [Features](docs/FEATURES.md)
- [Testing](docs/TESTING.md)
- [Render deployment](docs/DEPLOY_RENDER.md)
- [Production notes](docs/PRODUCTION.md)

## Run locally

The repository is safe to publish: local secrets are not meant to be committed.

For local development, create your own `CourseInventory.Web/appsettings.Development.json` or use environment variables.

If you already have PostgreSQL installed, open pgAdmin, connect as your administrator user, and run a setup script with your own credentials:

```sql
CREATE USER course_inventory WITH PASSWORD 'YOUR_LOCAL_PASSWORD';
CREATE DATABASE course_inventory OWNER course_inventory;
GRANT ALL PRIVILEGES ON DATABASE course_inventory TO course_inventory;
```

The same script is available in `database.local-setup.sql`.

If the user already exists but the password is wrong, run:

```sql
ALTER USER course_inventory WITH PASSWORD 'YOUR_LOCAL_PASSWORD';
GRANT ALL PRIVILEGES ON DATABASE course_inventory TO course_inventory;
```

The same reset script is available in `database.reset-password.sql`.

If you prefer Docker, a compose file is included. It maps PostgreSQL to port `5433`, so change `appsettings.Development.json` to `Port=5433` before using it, then start the bundled database:

```powershell
cd D:\Camp\COURSE_PROJECT
docker compose up -d
```

Then run the app:

```powershell
cd D:\Camp\COURSE_PROJECT\CourseInventory.Web
$env:DOTNET_CLI_HOME='D:\Camp\COURSE_PROJECT\.dotnet'
$env:NUGET_PACKAGES='D:\Camp\COURSE_PROJECT\.nuget\packages'
dotnet restore
dotnet run
```

Run tests:

```powershell
cd D:\Camp\COURSE_PROJECT
$env:DOTNET_CLI_HOME='D:\Camp\COURSE_PROJECT\.dotnet'
$env:NUGET_PACKAGES='D:\Camp\COURSE_PROJECT\.nuget\packages'
$env:APPDATA='D:\Camp\COURSE_PROJECT\.appdata'
$env:USERPROFILE='D:\Camp\COURSE_PROJECT'
dotnet test CourseInventory.Tests\CourseInventory.Tests.csproj
```

For social login in local development, keep secrets out of Git and use user-secrets:

```powershell
cd D:\Camp\COURSE_PROJECT\CourseInventory.Web
dotnet user-secrets set "Authentication:Google:ClientId" "YOUR_GOOGLE_CLIENT_ID"
dotnet user-secrets set "Authentication:Google:ClientSecret" "YOUR_GOOGLE_CLIENT_SECRET"
dotnet user-secrets set "Authentication:Facebook:AppId" "YOUR_FACEBOOK_APP_ID"
dotnet user-secrets set "Authentication:Facebook:AppSecret" "YOUR_FACEBOOK_APP_SECRET"
```

Then restart the app. The callback URIs must also match the provider configuration:

```text
http://localhost:5158/signin-google
http://localhost:5158/signin-facebook
```

Or use the included helper:

```powershell
cd D:\Camp\COURSE_PROJECT
.\run-local.ps1
```

If your PostgreSQL password is different, do not paste the raw connection string into committed files. Assign it to the ASP.NET Core environment variable:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=course_inventory;Username=course_inventory;Password=YOUR_LOCAL_PASSWORD"
dotnet run
```

There is also an editable example script:

```powershell
.\run-local-with-connection.example.ps1
```

`dotnet run` applies EF Core migrations automatically on startup in development.

If you want to seed an admin account on first run, provide it explicitly through environment variables:

```powershell
$env:SeedAdmin__Email = "admin@example.com"
$env:SeedAdmin__Password = "ChangeThisPassword123!"
dotnet run
```

## Database

For local development, create `appsettings.Development.json` yourself if your PostgreSQL user, password, or port differs.

For production, set `ConnectionStrings__DefaultConnection` and the external provider keys through environment variables or your hosting platform secrets.

## Access rules

- Inventory discussion/chat follows this rule: **authenticated user + `CanRead(inventory)`**.
- Visitors can read only public inventories.
- Item creation/editing still requires `CanWrite`.
- Inventory settings, access control, custom ID management, field management, and deletes still require `CanManage`.

For Render, use a managed PostgreSQL database and configure:

```text
ConnectionStrings__DefaultConnection
Authentication__Google__ClientId
Authentication__Google__ClientSecret
Authentication__Facebook__AppId
Authentication__Facebook__AppSecret
Cloudinary__CloudName
Cloudinary__ApiKey
Cloudinary__ApiSecret
```

## Render

Inventory Studio is prepared for Render as a Docker Web Service with Render PostgreSQL.

Recommended deployment:

1. Push this repository to GitHub.
2. Create a Render Blueprint from `render.yaml`.
3. Fill the `sync: false` environment variables in Render.
4. Let Render build the Docker image and run the pre-deploy migration command.

The Docker container listens on Render's `$PORT`.

Manual Docker build:

```powershell
docker build -t inventory-studio .
```

Manual Docker run with a local PostgreSQL connection string:

```powershell
docker run --rm -p 8080:8080 `
  -e PORT=8080 `
  -e ASPNETCORE_ENVIRONMENT=Production `
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=course_inventory;Username=course_inventory;Password=YOUR_LOCAL_PASSWORD" `
  inventory-studio
```

Run migrations manually inside the container/app:

```bash
dotnet CourseInventory.Web.dll --migrate
```

Full deployment details are in [docs/DEPLOY_RENDER.md](docs/DEPLOY_RENDER.md).
