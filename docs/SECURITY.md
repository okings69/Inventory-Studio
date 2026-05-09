# Security

Inventory Studio uses ASP.NET Core Identity, role-based authorization, and business permissions.

## Roles

- `User`: default role for normal users.
- `Admin`: full access to administration and global override.

## Inventory permissions

The central rules live in `AccessService`:

- `CanManage = Admin or Owner`
- `CanWrite = CanManage or explicit access`
- `CanRead = public inventory or CanManage or explicit access`

## User types

- Visitor: can read only public inventories.
- Normal user without access: cannot read private inventories.
- Shared user: can read and write items, but cannot manage settings, fields, access, Custom ID, or delete the inventory.
- Owner: full control of owned inventories.
- Admin: full override.

## Protected areas

- Controllers use `[Authorize]` for authenticated-only actions.
- Admin controller uses `[Authorize(Roles = "Admin")]`.
- POST actions use `[ValidateAntiForgeryToken]`.
- UI buttons are hidden or disabled based on permissions, but server-side checks remain the real protection.

## Chat rule

Discussion/chat follows this rule:

```text
authenticated user + CanRead(inventory)
```

Users who cannot read an inventory cannot join its SignalR group or post messages.

## OAuth secrets

Google and Facebook secrets must be configured through user-secrets or environment variables:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "..."
dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
dotnet user-secrets set "Authentication:Facebook:AppId" "..."
dotnet user-secrets set "Authentication:Facebook:AppSecret" "..."
```

Do not commit real OAuth secrets.

## Remaining security work

- Add broader integration tests for authenticated users with custom test authentication.
- Add audit logs for important writes and admin actions.
- Use HTTPS in production.

## Production deployment security

For Render production:

- keep `ASPNETCORE_ENVIRONMENT=Production`;
- keep OAuth, Cloudinary, admin seed, and database credentials in Render environment variables;
- do not commit real secrets to `appsettings.json`, `appsettings.example.json`, or `render.yaml`;
- use Render PostgreSQL private connection string through the Blueprint;
- on Render Free, use `Database__MigrateOnStartup=true` because `preDeployCommand` is not supported;
- on paid/scaled production, prefer `Database__MigrateOnStartup=false` and run migrations before deployment;
- update OAuth callback URLs to the deployed HTTPS domain.

The app also enables forwarded headers, secure cookies, HSTS, HTTPS redirection, and basic security headers in production.
