# CourseInventory

ASP.NET Core MVC inventory management application for a course project.

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

Build command:

```bash
dotnet publish CourseInventory.Web/CourseInventory.Web.csproj -c Release -o out
```

Start command:

```bash
dotnet out/CourseInventory.Web.dll
```

Run migrations before first production use:

```bash
dotnet ef database update --project CourseInventory.Web
```
