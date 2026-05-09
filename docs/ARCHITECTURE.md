# Inventory Studio Architecture

Inventory Studio uses a classic ASP.NET Core MVC structure with a service layer for business rules.

## Main folders

- `Models`: EF Core entities such as `Inventory`, `InventoryItem`, `InventoryField`, `Tag`, `InventoryAccess`, `DiscussionMessage`, and `ApplicationUser`.
- `ViewModels`: objects prepared for specific pages, so views do not need to receive raw database entities only.
- `Controllers`: HTTP entry points. They validate requests, check permissions, call services, and return views or redirects.
- `Services`: business logic such as access checks, search, tags, stats, custom IDs, items, discussions, and user activity.
- `Views`: Razor pages that render the UI.
- `wwwroot`: static CSS, JavaScript, Bootstrap, and client-side scripts.
- `Hubs`: SignalR hubs for real-time features.
- `Data`: `ApplicationDbContext` and seed logic.

## Request flow

1. The browser sends a request to a controller action.
2. The controller loads the current user when needed.
3. The controller calls a service for business logic.
4. The service uses `ApplicationDbContext` to read or write PostgreSQL data.
5. The controller returns a ViewModel to a Razor view.
6. The view renders HTML and hides or shows actions based on permissions.

## Program.cs

`Program.cs` configures:

- PostgreSQL through EF Core and Npgsql.
- ASP.NET Core Identity.
- Google and Facebook external login.
- MVC controllers and views.
- SignalR.
- application services.
- localization.
- authentication and authorization middleware.

`UseAuthentication()` must run before `UseAuthorization()` because the app must know who the user is before checking what the user can do.
