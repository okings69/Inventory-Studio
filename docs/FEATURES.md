# Features

## Inventories

Users can create inventories with title, description, category, tags, image URL, visibility, and status options. Inventories can be public or private.

## Custom fields

Owners can configure custom fields per inventory. Supported types are:

- single-line text;
- multi-line text;
- number;
- link;
- boolean.

Fields support descriptions, `ShowInTable`, and `SortOrder`.

## Items

Items are created inside an inventory and use the inventory's custom field definitions. The form changes depending on the configured fields.

## Custom IDs

Custom IDs are generated from ordered elements:

- fixed text;
- random 20-bit value;
- random 32-bit value;
- random 6 digits;
- random 9 digits;
- GUID;
- date/time;
- sequence.

## Tags

Inventories support multiple tags. Tag suggestions are available through:

- `/Tags/Autocomplete`
- `/Tags/Suggest`

Both endpoints return the same JSON format.

## Search

Search uses PostgreSQL-oriented full-text queries and then applies permission filtering. Private inventories are not exposed to unauthorized users.

## Discussion

Each inventory has a discussion area. SignalR is used for real-time delivery. A user must be authenticated and have `CanRead` permission.

## Likes

Authenticated users can like or unlike items. The database prevents duplicate likes by the same user on the same item.

## Export CSV

CSV export is available for users with `CanRead` permission on the inventory.

## Admin

Admin users can supervise accounts, block users, toggle admin role, delete safe users, and see online status and login activity.
