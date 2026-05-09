# Database

Inventory Studio uses PostgreSQL with Entity Framework Core.

## Why PostgreSQL

PostgreSQL is reliable, relational, and supports advanced search features. It is a good fit because the project has many relationships: users own inventories, inventories contain items, inventories have tags, users can receive access, and users can like or discuss items.

## EF Core

EF Core maps C# classes to database tables and keeps database changes in migrations. Migrations make the schema explainable and repeatable.

## Main entities

- `ApplicationUser`: Identity user plus profile and activity fields.
- `Inventory`: inventory metadata, owner, visibility, category, and status options.
- `InventoryItem`: item data stored in typed slots.
- `InventoryField`: custom field definition for an inventory.
- `CustomIdElement`: one component of the generated Custom ID pattern.
- `Tag`: reusable tag.
- `InventoryTag`: many-to-many relation between inventories and tags.
- `InventoryAccess`: explicit access granted to a user.
- `DiscussionMessage`: chat message for an inventory.
- `ItemLike`: user like for an item.
- `UserLoginActivity`: login activity used by the admin dashboard.

## Custom fields storage

The project does not store all item data as one unstructured JSON document. It uses typed item columns:

- `Text1` to `Text3`
- `LongText1` to `LongText3`
- `Number1` to `Number3`
- `Link1` to `Link3`
- `Bool1` to `Bool3`

`InventoryField.FieldKey` maps a custom field to one of these slots. This keeps validation, display, search, and export easier to understand.

## Important indexes

The database defines indexes for:

- inventory owner and update date;
- unique Custom ID per inventory;
- unique explicit access per user and inventory;
- unique like per user and item;
- unique normalized tag name;
- login activity by user and date.

## PostgreSQL configuration

Local configuration should use `appsettings.Development.json`, user secrets, or environment variables. Production should use environment variables or hosting secrets. Real secrets must not be committed.
