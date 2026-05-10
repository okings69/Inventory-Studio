# Deploy Inventory Studio on Render

This guide deploys Inventory Studio as a Docker Web Service connected to an existing Render PostgreSQL database.

## 1. Prerequisites

- GitHub repository connected to Render.
- `render.yaml` at the repository root.
- `Dockerfile` at the repository root.
- No real secrets committed to Git.

## 2. Render Blueprint

In Render:

1. Open **Blueprints**.
2. Click **New Blueprint Instance**.
3. Select the GitHub repository.
4. Render reads `render.yaml`.
5. Confirm the creation of `inventory-studio-web`.
6. Fill all variables marked `sync: false`.
7. For `ConnectionStrings__DefaultConnection`, paste the External Database URL from the existing Render PostgreSQL database, for example `user-management-db`.

## 3. Required Render variables

The Blueprint sets most values automatically.

Required:

- `ASPNETCORE_ENVIRONMENT=Production`
- `ConnectionStrings__DefaultConnection`: paste the existing Render PostgreSQL External Database URL manually.
- `Database__MigrateOnStartup=false`

Optional but recommended:

- `SeedAdmin__Email`
- `SeedAdmin__Password`
- `SeedAdmin__ResetPassword` set to `true` only when you need to force-reset the seeded admin password.
- `Authentication__Google__ClientId`
- `Authentication__Google__ClientSecret`
- `Authentication__Facebook__AppId`
- `Authentication__Facebook__AppSecret`
- `Cloudinary__CloudName`
- `Cloudinary__ApiKey`
- `Cloudinary__ApiSecret`

Reserved for future API work:

- `Jwt__SecretKey`
- `Jwt__Issuer`
- `Jwt__Audience`

## 4. OAuth callback URLs

After deployment, update OAuth provider callback URLs:

```text
https://YOUR-SERVICE.onrender.com/signin-google
https://YOUR-SERVICE.onrender.com/signin-facebook
```

Keep local callbacks for development:

```text
http://localhost:5158/signin-google
http://localhost:5158/signin-facebook
```

## 5. Migrations

Render Free does not support `preDeployCommand`. For the Free plan, the Blueprint sets:

```text
Database__MigrateOnStartup=true
```

This lets the app apply EF Core migrations when it starts. For a course project this is simple and acceptable.

If you reuse an existing free database such as `user-management-db`, Inventory Studio will create its own EF Core tables in that database during startup migrations.

After the first successful deployment, you can switch it to `false` and run migrations manually from a Render shell:

```bash
dotnet CourseInventory.Web.dll --migrate
```

## 6. Deploy flow

1. Push to GitHub.
2. Render auto-builds the Docker image.
3. Render starts the container.
4. The app applies migrations on startup when `Database__MigrateOnStartup=true`.
5. ASP.NET Core listens on Render's `$PORT`.

## 7. Post-deploy checks

- Open `/`.
- Open `/Account/Login`.
- Login with admin.
- Create a test inventory.
- Add an item.
- Test CSV export.
- Test search.
- Test chat on an inventory.
- Check Render logs for migration or database errors.

## 8. Admin account

The app seeds an admin account from:

```text
SeedAdmin__Email
SeedAdmin__Password
```

If the database already contains an `admin` user from a previous deployment, the seed process ensures that user is unblocked, email-confirmed, and in the `Admin` role.

If you forgot the password, temporarily set:

```text
SeedAdmin__ResetPassword=true
```

Then redeploy, log in, and set it back to:

```text
SeedAdmin__ResetPassword=false
```

## 9. Rollback

Use Render Dashboard:

1. Open the web service.
2. Go to **Deploys**.
3. Select a previous successful deploy.
4. Click **Rollback**.

If a migration changed the database schema, rollback the app carefully. Database schema rollback should be handled with a planned EF migration, not by manually deleting production data.

## 10. Frequent issues

| Problem | Cause | Fix |
|---|---|---|
| App exits on startup | Missing database connection string | Check `ConnectionStrings__DefaultConnection` |
| Blueprint sync fails with database limit | Render Free allows only one active free database | Remove the `databases:` block and reuse an existing DB |
| OAuth redirects fail | Wrong callback URL | Update Google/Meta redirect URIs |
| Images do not upload | Missing Cloudinary variables | Add Cloudinary secrets |
| Login cookie issues | HTTP/HTTPS proxy mismatch | Keep forwarded headers enabled |
| Migrations fail | DB not ready or bad connection | Retry deploy or run `--migrate` from shell |
