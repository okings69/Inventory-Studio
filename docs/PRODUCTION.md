# Production Notes

Inventory Studio is prepared for cloud deployment with Docker, Render Web Services, and Render PostgreSQL.

## Runtime

The production container uses:

- .NET SDK image for build.
- ASP.NET Runtime image for execution.
- Release publish output.
- `$PORT` binding through `ASPNETCORE_URLS`.

The final command is:

```bash
ASPNETCORE_URLS=http://0.0.0.0:${PORT:-8080} dotnet CourseInventory.Web.dll
```

## Database

Production must provide either:

- `ConnectionStrings__DefaultConnection`
- or `DATABASE_URL`

Render injects a PostgreSQL URL. `Program.cs` converts `postgresql://user:password@host:port/database` into an Npgsql connection string.

## Migrations

Paid Render services can run migrations before traffic reaches the new app version:

```bash
dotnet CourseInventory.Web.dll --migrate
```

Render Free does not support `preDeployCommand`, so this project uses startup migrations on Free through:

```text
Database__MigrateOnStartup=true
```

For future paid/scaled production, set `Database__MigrateOnStartup=false` and run `dotnet CourseInventory.Web.dll --migrate` manually or as a pre-deploy command.

## Reverse proxy

Render terminates HTTPS before forwarding requests to the container. The app enables forwarded headers so ASP.NET Core correctly understands the original scheme and client IP.

## Security headers

The app sets lightweight production-safe headers:

- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `Referrer-Policy: strict-origin-when-cross-origin`
- `Permissions-Policy: camera=(), microphone=(), geolocation=()`

## Cookies

Application cookies are:

- HttpOnly;
- SameSite=Lax;
- Secure in production.

## Uploads

Render's filesystem is ephemeral. Do not rely on local disk for permanent uploads. Inventory Studio uses Cloudinary configuration for cloud-safe image uploads.

## Logs

ASP.NET Core logs to stdout/stderr, which Render captures automatically. Sensitive values must not be logged.

## Future production work

- Add audit logs.
- Add admin pagination everywhere.
- Add health endpoint with database check.
- Add API rate limiting if REST endpoints are introduced.
- Add structured logging and monitoring.
